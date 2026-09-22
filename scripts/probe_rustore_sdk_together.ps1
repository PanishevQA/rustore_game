param(
    [string]$UnityExe = $env:UNITY_EXE,
    [int]$TimeoutSeconds = 1200
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "UnityProject"
$lockPath = Join-Path $projectPath "Packages\packages-lock.json"
$libraryPath = Join-Path $projectPath "Library"
$versionPath = Join-Path $projectPath "ProjectSettings\ProjectVersion.txt"
$artifacts = Join-Path $repoRoot "artifacts\rustore-package-probe"
$sharedReport = Join-Path $repoRoot "artifacts\agent-check\unity-serialized-validation.txt"
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

$versionLine = Get-Content $versionPath | Where-Object { $_ -match "^m_EditorVersion:" } | Select-Object -First 1
if (-not $versionLine) { throw "Could not read Unity editor version." }
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

Remove-Item $lockPath -Force -ErrorAction SilentlyContinue
if (Test-Path $libraryPath) {
    Remove-Item $libraryPath -Recurse -Force
}
Remove-Item $sharedReport -Force -ErrorAction SilentlyContinue

$log = Join-Path $artifacts "rustore-sdk-together.log"
$summaryPath = Join-Path $artifacts "together-summary.txt"
Remove-Item $log, $summaryPath -Force -ErrorAction SilentlyContinue

$args = @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", ('"' + $projectPath + '"'),
    "-executeMethod", "DontGetSidetracked.EditorTools.AgentProjectValidator.ValidateForAutomation",
    "-logFile", ('"' + $log + '"')
)

Write-Host "RuStore joint SDK probe"
Write-Host "Unity: $UnityExe"
Write-Host "Install Referrer: 10.6.1"
Write-Host "Remote Config: 10.5.1"
Write-Host "Timeout: $TimeoutSeconds seconds"

$started = [DateTime]::UtcNow
$process = Start-Process -FilePath $UnityExe -ArgumentList $args -PassThru
$timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
if ($timedOut) {
    try { & taskkill.exe /PID $process.Id /T /F | Out-Null } catch {}
    Start-Sleep -Seconds 2
}

$exitCode = if ($timedOut) { 124 } else { $process.ExitCode }
$logText = if (Test-Path $log) { [IO.File]::ReadAllText($log) } else { "" }
$compileFailurePattern = '(?im)\berror CS\d+\b|Scripts have compiler errors|Compilation failed|Failed to resolve packages|Package Manager.*Error|duplicate GUID'
$compileFailure = [Regex]::IsMatch($logText, $compileFailurePattern)

$installResolved = $false
$remoteResolved = $false
if (Test-Path $lockPath) {
    $lockText = [IO.File]::ReadAllText($lockPath)
    $installResolved = $lockText.Contains('"ru.rustore.installreferrer"') -and $lockText.Contains('"version": "10.6.1"')
    $remoteResolved = $lockText.Contains('"ru.rustore.remoteconfig"') -and $lockText.Contains('"version": "10.5.1"')
}

$reportExists = Test-Path $sharedReport
$passed = (-not $timedOut) -and $exitCode -eq 0 -and (-not $compileFailure) -and $installResolved -and $remoteResolved -and $reportExists
$duration = [int]([DateTime]::UtcNow - $started).TotalSeconds

$summary = @(
    "RuStore joint Unity package probe",
    "Generated UTC: " + [DateTime]::UtcNow.ToString("O"),
    "Unity: " + $editorVersion,
    "Install Referrer 10.6.1 exactVersion=" + $installResolved,
    "Remote Config 10.5.1 exactVersion=" + $remoteResolved,
    "TimedOut=" + $timedOut,
    "ExitCode=" + $exitCode,
    "CompileFailureOrDuplicateGuid=" + $compileFailure,
    "SerializedValidationReport=" + $reportExists,
    "DurationSeconds=" + $duration,
    "Result=" + $(if ($passed) { "PASS" } else { "FAIL" })
)
[IO.File]::WriteAllLines($summaryPath, $summary)
Get-Content $summaryPath

if (-not $passed) {
    if (Test-Path $log) { Get-Content $log -Tail 240 }
    exit 1
}

Write-Host "RuStore joint SDK probe PASSED." -ForegroundColor Green
