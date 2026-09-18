$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$unityProject = Join-Path $repoRoot "UnityProject"

if (-not (Test-Path (Join-Path $unityProject "Assets"))) {
    throw "UnityProject was not found at: $unityProject"
}

$library = Join-Path $unityProject "Library"
$lockFile = Join-Path $unityProject "Packages\packages-lock.json"

Write-Host "Resetting generated Unity package/cache state..." -ForegroundColor Cyan

if (Test-Path $library) {
    Remove-Item -Recurse -Force $library
    Write-Host "Removed UnityProject\Library"
}

if (Test-Path $lockFile) {
    Remove-Item -Force $lockFile
    Write-Host "Removed UnityProject\Packages\packages-lock.json"
}

Write-Host ""
Write-Host "Done. Open UnityProject again from Unity Hub and wait for package resolve/import to finish." -ForegroundColor Green
