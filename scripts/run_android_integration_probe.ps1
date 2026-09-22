param(
    [string]$UnityExe = $env:UNITY_EXE,
    [int]$TimeoutSeconds = 2400
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "UnityProject"
$versionPath = Join-Path $projectPath "ProjectSettings\ProjectVersion.txt"
$artifactRoot = Join-Path $repoRoot "artifacts\android-integration-probe"
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

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

$log = Join-Path $artifactRoot "unity-android-integration.log"
$apk = Join-Path $artifactRoot "nesbeisya-integration.apk"
Remove-Item $log, $apk -Force -ErrorAction SilentlyContinue
$env:NESBEISYA_INTEGRATION_OUTPUT = $apk

Write-Host "Android integration probe"
Write-Host "Unity: $UnityExe"
Write-Host "Project: $projectPath"
Write-Host "Output: $apk"

$args = @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", ('"' + $projectPath + '"'),
    "-executeMethod", "DontGetSidetracked.EditorTools.AndroidIntegrationBuildProbe.BuildFromCommandLine",
    "-logFile", ('"' + $log + '"')
)

$process = Start-Process -FilePath $UnityExe -ArgumentList $args -PassThru
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    try { & taskkill.exe /PID $process.Id /T /F | Out-Null } catch {}
    if (Test-Path $log) { Get-Content $log -Tail 220 }
    throw "Android integration Unity build timed out after $TimeoutSeconds seconds."
}

if ($process.ExitCode -ne 0 -or -not (Test-Path $apk)) {
    Write-Host "Android integration Unity build FAILED. exit=$($process.ExitCode)" -ForegroundColor Red
    if (Test-Path $log) { Get-Content $log -Tail 260 }
    throw "Android integration Unity build failed."
}

Write-Host "Android integration Unity build PASSED." -ForegroundColor Green
Write-Host "Inspecting resolved RuStore native AAR API..." -ForegroundColor Cyan
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "probe_rustore_native_aar.ps1") -UnityExe $UnityExe
if ($LASTEXITCODE -ne 0) {
    throw "RuStore native AAR API probe failed with exit code $LASTEXITCODE."
}

Write-Host "Android integration + native AAR probe PASSED." -ForegroundColor Green
