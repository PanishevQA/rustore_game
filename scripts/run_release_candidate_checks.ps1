param(
    [string]$UnityExe = $env:UNITY_EXE,
    [switch]$SkipReadiness
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "UnityProject"
$projectVersionPath = Join-Path $projectPath "ProjectSettings\ProjectVersion.txt"
$artifacts = Join-Path $repoRoot "artifacts\release-candidate"
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

# Clean stale local artifacts left by removed presentation scripts and previously resolved
# RuStore packages. They are generated/untracked state, not source files.
$orphanMeta = @(
    "Assets\Game\Presentation\CampaignHomeLayoutCoordinator.cs.meta",
    "Assets\Game\Presentation\CampaignVisualThemeCoordinator.cs.meta",
    "Assets\Game\Presentation\ExtendedUiThemeCoordinator.cs.meta",
    "Assets\Game\Presentation\HomeHeroCoordinator.cs.meta",
    "Assets\Game\Presentation\HomePolishCoordinator.cs.meta"
)
foreach ($relative in $orphanMeta) {
    $metaPath = Join-Path $projectPath $relative
    $assetPath = $metaPath.Substring(0, $metaPath.Length - 5)
    if ((Test-Path $metaPath) -and -not (Test-Path $assetPath)) {
        Remove-Item $metaPath -Force
    }
}

$manifestPath = Join-Path $projectPath "Packages\manifest.json"
$manifestText = Get-Content $manifestPath -Raw
foreach ($quarantined in @("ru.rustore.installreferrer", "ru.rustore.remoteconfig")) {
    if ($manifestText -match [Regex]::Escape('"' + $quarantined + '"')) {
        throw "$quarantined is still present in Packages/manifest.json. Run git pull on feat/mvp-foundation before release-candidate checks."
    }
}

Remove-Item (Join-Path $projectPath "Packages\packages-lock.json") -Force -ErrorAction SilentlyContinue

# Unity 6 can restore the previous resolved-package graph from generated Library state even
# after individual PackageCache folders are removed. For a release-candidate check prefer
# determinism over import speed and force a completely fresh UPM/compile state.
$libraryPath = Join-Path $projectPath "Library"
if (Test-Path $libraryPath) {
    Write-Host "Resetting generated Unity Library for a deterministic package resolve..." -ForegroundColor Yellow
    Remove-Item $libraryPath -Recurse -Force
}

if (-not (Test-Path $projectVersionPath)) {
    throw "Unity ProjectVersion.txt not found: $projectVersionPath"
}

$versionLine = Get-Content $projectVersionPath | Where-Object { $_ -match "^m_EditorVersion:" } | Select-Object -First 1
if (-not $versionLine) {
    throw "Could not read m_EditorVersion from $projectVersionPath"
}
$editorVersion = ($versionLine -split ":", 2)[1].Trim()

function Resolve-UnityExe {
    param([string]$Explicit, [string]$Version)

    if ($Explicit) {
        $candidate = [Environment]::ExpandEnvironmentVariables($Explicit)
        if (Test-Path $candidate) { return (Resolve-Path $candidate).Path }
        throw "UNITY_EXE/UnityExe does not exist: $candidate"
    }

    $candidates = @(
        "C:\Program Files\Unity\Hub\Editor\$Version\Editor\Unity.exe",
        "C:\Program Files\Unity Hub\Editor\$Version\Editor\Unity.exe",
        "$env:ProgramFiles\Unity\Hub\Editor\$Version\Editor\Unity.exe"
    ) | Select-Object -Unique

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path $candidate)) {
            return (Resolve-Path $candidate).Path
        }
    }

    $hubRoot = "C:\Program Files\Unity\Hub\Editor"
    if (Test-Path $hubRoot) {
        $installed = Get-ChildItem $hubRoot -Directory | Select-Object -ExpandProperty Name
        throw "Unity $Version was not found. Installed Hub editors: $($installed -join ', '). Set UNITY_EXE to override."
    }

    throw "Unity $Version was not found. Set UNITY_EXE to the full Unity.exe path."
}

$unity = Resolve-UnityExe -Explicit $UnityExe -Version $editorVersion
Write-Host "Unity: $unity" -ForegroundColor Cyan
Write-Host "Project: $projectPath" -ForegroundColor Cyan
Write-Host "Editor version: $editorVersion" -ForegroundColor Cyan
Write-Host ""

$testResults = Join-Path $artifacts "editmode-results.xml"
$testLog = Join-Path $artifacts "editmode.log"
$readinessLog = Join-Path $artifacts "readiness.log"
$readinessSource = Join-Path $projectPath "Library\NesbeisyaReleaseReadiness.txt"
$readinessCopy = Join-Path $artifacts "release-readiness.txt"

Remove-Item $testResults, $testLog, $readinessLog, $readinessCopy -Force -ErrorAction SilentlyContinue

Write-Host "[1/2] Unity compile + EditMode tests..." -ForegroundColor Yellow
& $unity -batchmode -nographics -projectPath $projectPath -runTests -testPlatform EditMode -testResults $testResults -logFile $testLog

$testExit = $LASTEXITCODE
if ($testExit -ne 0) {
    Write-Host "Unity EditMode tests FAILED (exit $testExit)." -ForegroundColor Red
    Write-Host "Log: $testLog"
    if (Test-Path $testLog) { Get-Content $testLog -Tail 80 }
    exit $testExit
}

if (-not (Test-Path $testResults)) {
    throw "Unity exited successfully but did not create test results: $testResults"
}
Write-Host "Unity compile + EditMode tests passed." -ForegroundColor Green

if (-not $SkipReadiness) {
    Write-Host ""
    Write-Host "[2/2] Prepare Android release environment + readiness report..." -ForegroundColor Yellow
    & $unity -batchmode -nographics -quit -projectPath $projectPath -executeMethod DontGetSidetracked.EditorTools.ReleaseReadinessReporter.Report -logFile $readinessLog

    $readinessExit = $LASTEXITCODE
    if ($readinessExit -ne 0) {
        Write-Host "Readiness command FAILED (exit $readinessExit)." -ForegroundColor Red
        Write-Host "Log: $readinessLog"
        if (Test-Path $readinessLog) { Get-Content $readinessLog -Tail 100 }
        exit $readinessExit
    }

    if (Test-Path $readinessSource) {
        Copy-Item $readinessSource $readinessCopy -Force
        Write-Host ""
        Get-Content $readinessCopy
        Write-Host ""
        Write-Host "Readiness report: $readinessCopy" -ForegroundColor Cyan
    } else {
        throw "Readiness command completed but report file was not created: $readinessSource"
    }
}

Write-Host ""
Write-Host "Release-candidate local checks completed." -ForegroundColor Green
Write-Host "Artifacts: $artifacts"
