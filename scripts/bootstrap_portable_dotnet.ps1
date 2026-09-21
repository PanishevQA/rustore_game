param(
    [string]$Version = "8.0.425"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$installDir = Join-Path $repoRoot ".agent-tools\dotnet"
$dotnetExe = Join-Path $installDir "dotnet.exe"
$installer = Join-Path $env:RUNNER_TEMP "dotnet-install.ps1"

if (-not (Test-Path $dotnetExe)) {
    New-Item -ItemType Directory -Force -Path $installDir | Out-Null
    Write-Host "Downloading official dotnet-install.ps1..."
    Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installer
    Write-Host "Installing .NET SDK $Version to $installDir..."
    & powershell -NoProfile -ExecutionPolicy Bypass -File $installer -Version $Version -InstallDir $installDir -NoPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet-install.ps1 failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path $dotnetExe)) {
    throw ".NET executable was not created: $dotnetExe"
}

$versionText = (& $dotnetExe --version).Trim()
if ($LASTEXITCODE -ne 0 -or $versionText -ne $Version) {
    throw "Expected .NET SDK $Version, got '$versionText'."
}

Write-Host ".NET SDK ready: $dotnetExe"
Write-Host ".NET SDK $versionText"

if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_PATH)) {
    Add-Content -Path $env:GITHUB_PATH -Value $installDir
} else {
    $env:PATH = "$installDir;$env:PATH"
}

if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_ENV)) {
    Add-Content -Path $env:GITHUB_ENV -Value "DOTNET_ROOT=$installDir"
}
