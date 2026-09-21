param(
    [string]$Version = "3.13.15"
)

$ErrorActionPreference = "Stop"
$expectedSha256 = "d1f04d990aee1253d8569e8e5104e30fa9f5fa830899f14843448872d936a2cf"
$url = "https://www.python.org/ftp/python/$Version/python-$Version-embed-amd64.zip"

$repoRoot = Split-Path -Parent $PSScriptRoot
$toolRoot = Join-Path $repoRoot ".agent-tools\python-$Version"
$pythonExe = Join-Path $toolRoot "python.exe"
$archive = Join-Path $env:RUNNER_TEMP "python-$Version-embeddable-amd64.zip"

if (-not (Test-Path $pythonExe)) {
    New-Item -ItemType Directory -Force -Path $toolRoot | Out-Null

    Write-Host "Downloading portable Python $Version from python.org..."
    Invoke-WebRequest -Uri $url -OutFile $archive

    $actualSha256 = (Get-FileHash -Path $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualSha256 -ne $expectedSha256) {
        throw "Portable Python checksum mismatch. Expected $expectedSha256, got $actualSha256."
    }

    Get-ChildItem $toolRoot -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
    Expand-Archive -LiteralPath $archive -DestinationPath $toolRoot -Force
}

if (-not (Test-Path $pythonExe)) {
    throw "Portable Python executable was not created: $pythonExe"
}

$versionText = (& $pythonExe --version 2>&1).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Portable Python failed to start."
}

Write-Host "Portable Python ready: $pythonExe"
Write-Host $versionText

if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_PATH)) {
    Add-Content -Path $env:GITHUB_PATH -Value $toolRoot
} else {
    $env:PATH = "$toolRoot;$env:PATH"
}
