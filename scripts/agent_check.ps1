param(
    [ValidateSet("Fast", "Unity", "Full", "Build", "ReleaseCandidate")]
    [string]$Mode = "Unity",
    [string]$UnityExe = $env:UNITY_EXE,
    [switch]$SkipFast
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

    $pythonTests = Get-ChildItem (Join-Path $repoRoot "scripts") -File -Filter "test_*.py" | Sort-Object Name
    foreach ($test in $pythonTests) {
        $log = Join-Path $artifactRoot ($test.BaseName + ".log")
        Invoke-PythonScript -Python $python -ScriptPath $test.FullName -LogPath $log
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


function Invoke-UnityDiagnostics {
    param(
        [switch]$IncludeReadiness,
        [switch]$IncludeBuild
    )

    $analyzer = Join-Path $repoRoot "scripts\analyze_unity_log.py"
    if (-not (Test-Path $analyzer)) {
        Write-Warning "Unity diagnostics parser is missing: $analyzer"
        return
    }

    $python = Resolve-Python
    $arguments = @()
    $arguments += $python.Prefix
    $arguments += $analyzer

    $releaseArtifacts = Join-Path $repoRoot "artifacts\release-candidate"
    $editModeLog = Join-Path $releaseArtifacts "editmode.log"
    $editModeResults = Join-Path $releaseArtifacts "editmode-results.xml"
    $playModeLog = Join-Path $releaseArtifacts "playmode.log"
    $playModeResults = Join-Path $releaseArtifacts "playmode-results.xml"
    $serializedLog = Join-Path $releaseArtifacts "serialized-validation.log"
    $readinessLog = Join-Path $releaseArtifacts "readiness.log"
    $productionBuildLog = Join-Path $releaseArtifacts "production-build.log"

    if (Test-Path $editModeLog) {
        $arguments += @("--log", $editModeLog)
    }
    if (Test-Path $playModeLog) {
        $arguments += @("--log", $playModeLog)
    }
    if (Test-Path $serializedLog) {
        $arguments += @("--log", $serializedLog)
    }
    if ($IncludeReadiness -and (Test-Path $readinessLog)) {
        $arguments += @("--log", $readinessLog)
    }
    if ($IncludeBuild -and (Test-Path $productionBuildLog)) {
        $arguments += @("--log", $productionBuildLog)
    }
    if (Test-Path $editModeResults) {
        $arguments += @("--test-results", $editModeResults)
    }
    if (Test-Path $playModeResults) {
        $arguments += @("--test-results", $playModeResults)
    }

    $jsonOut = Join-Path $artifactRoot "unity-diagnostics.json"
    $arguments += @("--json-out", $jsonOut)

    Write-Host ""
    Write-Host "[Unity failure diagnostics]" -ForegroundColor Yellow
    & $python.Executable @arguments 2>&1 | Tee-Object -FilePath (Join-Path $artifactRoot "unity-diagnostics.log")
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Unity diagnostics parser failed; inspect raw release-candidate logs."
    }
}

function Invoke-UnityChecks {
    param(
        [switch]$IncludeReadiness,
        [switch]$BuildAab,
        [switch]$BuildSmokeApk
    )

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

    if ($BuildAab) {
        $args += "-BuildAab"
    }
    if ($BuildSmokeApk) {
        $args += "-BuildSmokeApk"
    }
    if (-not $BuildAab -and -not $BuildSmokeApk -and -not $IncludeReadiness) {
        $args += "-SkipReadiness"
    }

    $name = if ($BuildAab -and $BuildSmokeApk) { "Unity matched release-candidate APK + AAB gate" } elseif ($BuildAab) { "Unity production Android build gate" } elseif ($BuildSmokeApk) { "Unity signed device-smoke build gate" } elseif ($IncludeReadiness) { "Unity + release readiness" } else { "Unity compile + tests + serialized validation" }
    $logName = if ($BuildAab -and $BuildSmokeApk) { "unity-release-candidate.log" } elseif ($BuildAab) { "unity-build.log" } elseif ($BuildSmokeApk) { "unity-device-smoke.log" } elseif ($IncludeReadiness) { "unity-full.log" } else { "unity.log" }

    try {
        Invoke-LoggedExternal -Name $name -Executable $powershell.Source -Arguments $args -LogPath (Join-Path $artifactRoot $logName)
    }
    catch {
        Invoke-UnityDiagnostics -IncludeReadiness:($IncludeReadiness -or $BuildAab -or $BuildSmokeApk) -IncludeBuild:$BuildAab
        throw
    }
}

function Invoke-SmokeArtifactVerification {
    param(
        [Parameter(Mandatory = $true)][string]$SmokeOutput
    )

    $sidecar = $SmokeOutput + ".sha256"
    if (-not (Test-Path $SmokeOutput -PathType Leaf)) {
        throw "Signed device-smoke APK is missing: $SmokeOutput"
    }
    if (-not (Test-Path $sidecar -PathType Leaf)) {
        throw "Signed device-smoke SHA-256 sidecar is missing: $sidecar"
    }

    $line = (Get-Content $sidecar | Select-Object -First 1).Trim()
    $expected = ($line -split "\s+")[0].ToLowerInvariant()
    if ($expected -notmatch "^[0-9a-f]{64}$") {
        throw "Signed device-smoke SHA-256 sidecar is malformed: $sidecar"
    }

    $actual = (Get-FileHash -Path $SmokeOutput -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected) {
        throw "Signed device-smoke APK SHA-256 mismatch. Expected $expected, got $actual."
    }

    @(
        "Signed device-smoke artifact verification: PASS",
        "Apk=$SmokeOutput",
        "Sha256=$actual"
    ) | Set-Content (Join-Path $artifactRoot "verify-device-smoke-artifact.log")
    Write-Host "Verified signed device-smoke APK SHA-256: $actual" -ForegroundColor Green
}


function Resolve-CurrentGitSha {
    $git = Get-Command git -ErrorAction SilentlyContinue
    if (-not $git) {
        throw "Git is required to bind production release metadata to the current commit."
    }

    $shaOutput = & $git.Source -c "safe.directory=$repoRoot" -C $repoRoot rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($shaOutput)) {
        throw "Could not resolve the current Git commit SHA."
    }
    return ($shaOutput | Select-Object -First 1).Trim()
}

function Invoke-ReleaseArtifactVerification {
    param(
        [Parameter(Mandatory = $true)][string]$ReleaseOutput,
        [Parameter(Mandatory = $true)][string]$ExpectedGitSha
    )

    $metadata = [IO.Path]::ChangeExtension($ReleaseOutput, ".release.json")
    $verifier = Join-Path $repoRoot "scripts\verify_release_artifact.py"
    if (-not (Test-Path $verifier)) {
        throw "Release artifact verifier is missing: $verifier"
    }

    $python = Resolve-Python
    $arguments = @()
    $arguments += $python.Prefix
    $arguments += $verifier
    $arguments += $metadata
    $arguments += @("--package", "ru.release.nesbeisya", "--git-sha", $ExpectedGitSha)

    Invoke-LoggedExternal -Name "Verify production AAB metadata and SHA-256" -Executable $python.Executable -Arguments $arguments -LogPath (Join-Path $artifactRoot "verify-release-artifact.log")
}

Write-Host "НЕ СБЕЙСЯ! agent validation" -ForegroundColor Cyan
Write-Host "Mode: $Mode"
Write-Host "Repository: $repoRoot"
Write-Host ""

if (-not $SkipFast) {
    Invoke-FastChecks
}

if ($Mode -eq "Fast") {
    if ($SkipFast) {
        throw "Mode Fast cannot be combined with -SkipFast."
    }
}
elseif ($Mode -eq "Unity") {
    Invoke-UnityChecks
}
elseif ($Mode -eq "Full") {
    Invoke-UnityChecks -IncludeReadiness
}
elseif ($Mode -eq "Build") {
    $releaseOutput = Join-Path $repoRoot "artifacts\release-candidate\android\nesbeisya-production.aab"
    $gitSha = Resolve-CurrentGitSha
    $env:NESBEISYA_RELEASE_OUTPUT = $releaseOutput
    $env:RELEASE_GIT_SHA = $gitSha
    Invoke-UnityChecks -IncludeReadiness -BuildAab
    Invoke-ReleaseArtifactVerification -ReleaseOutput $releaseOutput -ExpectedGitSha $gitSha
}
elseif ($Mode -eq "ReleaseCandidate") {
    $releaseOutput = Join-Path $repoRoot "artifacts\release-candidate\android\nesbeisya-production.aab"
    $smokeOutput = Join-Path $repoRoot "artifacts\release-candidate\android-device-smoke\nesbeisya-device-smoke.apk"
    $gitSha = Resolve-CurrentGitSha
    $env:NESBEISYA_RELEASE_OUTPUT = $releaseOutput
    $env:NESBEISYA_DEVICE_SMOKE_OUTPUT = $smokeOutput
    $env:RELEASE_GIT_SHA = $gitSha
    Invoke-UnityChecks -IncludeReadiness -BuildAab -BuildSmokeApk
    Invoke-SmokeArtifactVerification -SmokeOutput $smokeOutput
    Invoke-ReleaseArtifactVerification -ReleaseOutput $releaseOutput -ExpectedGitSha $gitSha
}

Write-Host ""
Write-Host "Agent validation passed: $Mode" -ForegroundColor Green
Write-Host "Logs: $artifactRoot"
