param(
    [ValidateSet("Fast", "Unity", "Full")]
    [string]$Mode = "Unity",
    [string]$UnityExe = $env:UNITY_EXE
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot "artifacts\agent-check"
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

function Invoke-LoggedExternal {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$LogPath
    )

    Write-Host "[$Name]" -ForegroundColor Yellow
    & $Executable @Arguments 2>&1 | Tee-Object -FilePath $LogPath
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "$Name failed with exit code $exitCode. Log: $LogPath"
    }
}

function Resolve-Python {
    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($python) {
        return @{
            Executable = $python.Source
            Prefix = @()
        }
    }

    $py = Get-Command py -ErrorAction SilentlyContinue
    if ($py) {
        return @{
            Executable = $py.Source
            Prefix = @("-3")
        }
    }

    throw "Python 3 is required for repository validators but was not found in PATH."
}

function Invoke-PythonScript {
    param(
        [Parameter(Mandatory = $true)]$Python,
        [Parameter(Mandatory = $true)][string]$ScriptPath,
        [Parameter(Mandatory = $true)][string]$LogPath
    )

    $args = @()
    $args += $Python.Prefix
    $args += $ScriptPath

    Invoke-LoggedExternal -Name ("Python " + (Split-Path -Leaf $ScriptPath)) -Executable $Python.Executable -Arguments $args -LogPath $LogPath
}

function Invoke-FastChecks {
    $python = Resolve-Python

    $validators = Get-ChildItem (Join-Path $repoRoot "scripts") -File -Filter "validate_*.py" | Sort-Object Name

    foreach ($validator in $validators) {
        $log = Join-Path $artifactRoot ($validator.BaseName + ".log")
        Invoke-PythonScript -Python $python -ScriptPath $validator.FullName -LogPath $log
    }

    $artifactVerifierTests = Join-Path $repoRoot "scripts\test_release_artifact_verifier.py"
    if (Test-Path $artifactVerifierTests) {
        Invoke-PythonScript -Python $python -ScriptPath $artifactVerifierTests -LogPath (Join-Path $artifactRoot "test_release_artifact_verifier.log")
    }

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        throw ".NET SDK is required for pure-tests but dotnet was not found in PATH."
    }

    Invoke-LoggedExternal -Name "Pure C# tests" -Executable $dotnet.Source -Arguments @(
        "test",
        (Join-Path $repoRoot "pure-tests\PureRules.Tests.csproj"),
        "--configuration", "Release",
        "--nologo"
    ) -LogPath (Join-Path $artifactRoot "pure-csharp-tests.log")
}

function Invoke-UnityChecks {
    param([switch]$IncludeReadiness)

    $runner = Join-Path $repoRoot "scripts\run_release_candidate_checks.ps1"
    if (-not (Test-Path $runner)) {
        throw "Missing Unity release-candidate runner: $runner"
    }

    $powershell = Get-Command powershell -ErrorAction SilentlyContinue
    if (-not $powershell) {
        throw "Windows PowerShell is required to execute the Unity validation runner."
    }

    $args = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $runner
    )

    if ($UnityExe) {
        $args += @("-UnityExe", $UnityExe)
    }

    if (-not $IncludeReadiness) {
        $args += "-SkipReadiness"
    }

    $name = if ($IncludeReadiness) { "Unity + release readiness" } else { "Unity compile + EditMode tests" }
    $logName = if ($IncludeReadiness) { "unity-full.log" } else { "unity.log" }

    Invoke-LoggedExternal -Name $name -Executable $powershell.Source -Arguments $args -LogPath (Join-Path $artifactRoot $logName)
}

Write-Host "НЕ СБЕЙСЯ! agent validation" -ForegroundColor Cyan
Write-Host "Mode: $Mode"
Write-Host "Repository: $repoRoot"
Write-Host ""

Invoke-FastChecks

if ($Mode -eq "Unity") {
    Invoke-UnityChecks
}
elseif ($Mode -eq "Full") {
    Invoke-UnityChecks -IncludeReadiness
}

Write-Host ""
Write-Host "Agent validation passed: $Mode" -ForegroundColor Green
Write-Host "Logs: $artifactRoot"
