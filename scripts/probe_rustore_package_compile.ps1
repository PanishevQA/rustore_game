param(
    [string]$UnityExe = $env:UNITY_EXE,
    [int]$TimeoutSeconds = 900
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "UnityProject"
$versionPath = Join-Path $projectPath "ProjectSettings\ProjectVersion.txt"
$artifacts = Join-Path $repoRoot "artifacts\rustore-package-probe"
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$log = Join-Path $artifacts "unity-compile.log"
$report = Join-Path $repoRoot "artifacts\agent-check\unity-serialized-validation.txt"
Remove-Item $log, $report -Force -ErrorAction SilentlyContinue

$versionLine = Get-Content $versionPath | Where-Object { $_ -match "^m_EditorVersion:" } | Select-Object -First 1
if (-not $versionLine) { throw "Could not read Unity editor version." }
$version = ($versionLine -split ":", 2)[1].Trim()

if ([string]::IsNullOrWhiteSpace($UnityExe)) {
    $candidates = @(
        "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe",
        "C:\Program Files\Unity Hub\Editor\$version\Editor\Unity.exe"
    )
    $UnityExe = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $UnityExe -or -not (Test-Path $UnityExe)) {
    throw "Unity $version was not found."
}

Write-Host "RuStore package compile probe"
Write-Host "Remote Config 10.5.1 isolated probe"
Write-Host "Unity: $UnityExe"
Write-Host "Project: $projectPath"
Write-Host "Timeout: $TimeoutSeconds seconds"

$args = @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", ('"' + $projectPath + '"'),
    "-executeMethod", "DontGetSidetracked.EditorTools.AgentProjectValidator.ValidateForAutomation",
    "-logFile", ('"' + $log + '"')
)

$process = Start-Process -FilePath $UnityExe -ArgumentList $args -PassThru
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    try { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } catch {}
    Write-Host "Unity package probe TIMED OUT after $TimeoutSeconds seconds." -ForegroundColor Red
    if (Test-Path $log) { Get-Content $log -Tail 160 }
    exit 124
}

$exitCode = $process.ExitCode
$logText = if (Test-Path $log) { Get-Content $log -Raw } else { "" }
$failurePattern = '(?im)\berror CS\d+\b|Scripts have compiler errors|Compilation failed|Package Manager.*Error|Failed to resolve packages'
$compileFailure = [Regex]::IsMatch($logText, $failurePattern)

if ($exitCode -ne 0 -or $compileFailure -or -not (Test-Path $report)) {
    Write-Host "Unity package probe FAILED. exit=$exitCode compileFailure=$compileFailure reportExists=$(Test-Path $report)" -ForegroundColor Red
    if (Test-Path $log) { Get-Content $log -Tail 220 }
    exit $(if ($exitCode -ne 0) { $exitCode } else { 1 })
}

Write-Host "Unity package probe PASSED." -ForegroundColor Green
Write-Host "Serialized validation report: $report"
