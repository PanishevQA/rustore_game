param(
    [string]$UnityExe = $env:UNITY_EXE,
    [int]$PerPackageTimeoutSeconds = 1200
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "UnityProject"
$manifestPath = Join-Path $projectPath "Packages\manifest.json"
$lockPath = Join-Path $projectPath "Packages\packages-lock.json"
$libraryPath = Join-Path $projectPath "Library"
$versionPath = Join-Path $projectPath "ProjectSettings\ProjectVersion.txt"
$artifacts = Join-Path $repoRoot "artifacts\rustore-package-probe"
$sharedReport = Join-Path $repoRoot "artifacts\agent-check\unity-serialized-validation.txt"
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

if (-not (Test-Path $manifestPath)) {
    throw "Packages/manifest.json was not found."
}

$originalManifest = [IO.File]::ReadAllText($manifestPath)
$originalLockExists = Test-Path $lockPath
$originalLock = if ($originalLockExists) { [IO.File]::ReadAllText($lockPath) } else { $null }

$versionLine = Get-Content $versionPath | Where-Object { $_ -match "^m_EditorVersion:" } | Select-Object -First 1
if (-not $versionLine) {
    throw "Could not read Unity editor version."
}
$editorVersion = ($versionLine -split ":", 2)[1].Trim()

if ([string]::IsNullOrWhiteSpace($UnityExe)) {
    $candidates = @(
        "C:\Program Files\Unity\Hub\Editor\$editorVersion\Editor\Unity.exe",
        "C:\Program Files\Unity Hub\Editor\$editorVersion\Editor\Unity.exe"
    )
    $UnityExe = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $UnityExe -or -not (Test-Path $UnityExe)) {
    throw "Unity $editorVersion was not found."
}

function Reset-UnityGeneratedState {
    Remove-Item $lockPath -Force -ErrorAction SilentlyContinue
    if (Test-Path $libraryPath) {
        Remove-Item $libraryPath -Recurse -Force
    }
    Remove-Item $sharedReport -Force -ErrorAction SilentlyContinue
}

function Restore-SourceState {
    [IO.File]::WriteAllText($manifestPath, $originalManifest)
    if ($originalLockExists) {
        [IO.File]::WriteAllText($lockPath, $originalLock)
    }
    else {
        Remove-Item $lockPath -Force -ErrorAction SilentlyContinue
    }
}

function Add-ProbePackage([string]$PackageId, [string]$Version) {
    $needle = '"ru.rustore.pay": "11.1.0",'
    if (-not $originalManifest.Contains($needle)) {
        throw "Could not find the pinned RuStore Pay entry used as the package insertion anchor."
    }

    $line = '    "' + $PackageId + '": "' + $Version + '",'
    $manifest = $originalManifest.Replace($needle, $needle + [Environment]::NewLine + $line)
    [IO.File]::WriteAllText($manifestPath, $manifest)
}

function Invoke-Probe([string]$Label, [string]$PackageId, [string]$Version) {
    Write-Host ""
    Write-Host "=== $Label ($PackageId $Version) ===" -ForegroundColor Cyan

    Restore-SourceState
    Add-ProbePackage -PackageId $PackageId -Version $Version
    Reset-UnityGeneratedState

    $slug = ($PackageId -replace '[^A-Za-z0-9._-]', '-')
    $log = Join-Path $artifacts ($slug + ".log")
    $reportCopy = Join-Path $artifacts ($slug + "-serialized-validation.txt")
    Remove-Item $log, $reportCopy -Force -ErrorAction SilentlyContinue

    $args = @(
        "-batchmode",
        "-nographics",
        "-quit",
        "-projectPath", ('"' + $projectPath + '"'),
        "-executeMethod", "DontGetSidetracked.EditorTools.AgentProjectValidator.ValidateForAutomation",
        "-logFile", ('"' + $log + '"')
    )

    $started = [DateTime]::UtcNow
    $process = Start-Process -FilePath $UnityExe -ArgumentList $args -PassThru
    $timedOut = -not $process.WaitForExit($PerPackageTimeoutSeconds * 1000)
    if ($timedOut) {
        try { & taskkill.exe /PID $process.Id /T /F | Out-Null } catch {}
        Start-Sleep -Seconds 2
    }

    $exitCode = if ($timedOut) { 124 } else { $process.ExitCode }
    $logText = if (Test-Path $log) { [IO.File]::ReadAllText($log) } else { "" }
    $compileFailurePattern = '(?im)\berror CS\d+\b|Scripts have compiler errors|Compilation failed|Failed to resolve packages|Package Manager.*Error'
    $compileFailure = [Regex]::IsMatch($logText, $compileFailurePattern)

    $resolved = $false
    if (Test-Path $lockPath) {
        $lockText = [IO.File]::ReadAllText($lockPath)
        $resolved = $lockText.Contains('"' + $PackageId + '"') -and $lockText.Contains('"version": "' + $Version + '"')
    }

    $reportExists = Test-Path $sharedReport
    if ($reportExists) {
        Copy-Item $sharedReport $reportCopy -Force
    }

    $passed = (-not $timedOut) -and $exitCode -eq 0 -and (-not $compileFailure) -and $resolved -and $reportExists
    $result = [PSCustomObject]@{
        Label = $Label
        Package = $PackageId
        Version = $Version
        Passed = $passed
        TimedOut = $timedOut
        ExitCode = $exitCode
        CompileFailure = $compileFailure
        ResolvedExactVersion = $resolved
        SerializedValidationReport = $reportExists
        DurationSeconds = [int]([DateTime]::UtcNow - $started).TotalSeconds
        Log = $log
    }

    if ($passed) {
        Write-Host "$Label PASSED." -ForegroundColor Green
    }
    else {
        Write-Host "$Label FAILED: timeout=$timedOut exit=$exitCode compileFailure=$compileFailure resolved=$resolved report=$reportExists" -ForegroundColor Red
        if (Test-Path $log) {
            Get-Content $log -Tail 180
        }
    }

    return $result
}

$results = @()
try {
    $results += Invoke-Probe -Label "Install Referrer" -PackageId "ru.rustore.installreferrer" -Version "10.6.1"
    $results += Invoke-Probe -Label "Remote Config" -PackageId "ru.rustore.remoteconfig" -Version "10.5.1"
}
finally {
    Restore-SourceState
    if (Test-Path $libraryPath) {
        Remove-Item $libraryPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$summaryPath = Join-Path $artifacts "summary.txt"
$summary = New-Object System.Collections.Generic.List[string]
$summary.Add("RuStore Unity package probes")
$summary.Add("Generated UTC: " + [DateTime]::UtcNow.ToString("O"))
$summary.Add("Unity: " + $editorVersion)
$summary.Add("")
foreach ($result in $results) {
    $summary.Add(
        ("{0} {1}: {2}; timeout={3}; exit={4}; compileFailure={5}; exactVersion={6}; serializedReport={7}; durationSec={8}" -f
            $result.Package,
            $result.Version,
            $(if ($result.Passed) { "PASS" } else { "FAIL" }),
            $result.TimedOut,
            $result.ExitCode,
            $result.CompileFailure,
            $result.ResolvedExactVersion,
            $result.SerializedValidationReport,
            $result.DurationSeconds)
    )
}
[IO.File]::WriteAllLines($summaryPath, $summary)
Get-Content $summaryPath

if (($results | Where-Object { -not $_.Passed }).Count -gt 0) {
    exit 1
}

Write-Host "All RuStore package probes PASSED." -ForegroundColor Green
