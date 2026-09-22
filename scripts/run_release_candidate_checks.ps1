param(
    [string]$UnityExe = $env:UNITY_EXE,
    [switch]$SkipReadiness,
    [switch]$BuildAab
)

$ErrorActionPreference = "Stop"
if ($BuildAab -and $SkipReadiness) {
    throw "BuildAab requires release readiness; do not combine -BuildAab with -SkipReadiness."
}
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
$forbiddenUnityPackage = "ru.rustore.installreferrer"
if ($manifestText -match [Regex]::Escape('"' + $forbiddenUnityPackage + '"')) {
    throw "$forbiddenUnityPackage must not be installed beside Remote Config 10.5.1 because the verified official Unity packages contain duplicate .meta GUIDs."
}

$lockPath = Join-Path $projectPath "Packages\packages-lock.json"
$libraryPath = Join-Path $projectPath "Library"
$packageCache = Join-Path $libraryPath "PackageCache"
$staleRuStoreState = $false

if (Test-Path $lockPath) {
    $lockText = Get-Content $lockPath -Raw
    if ($lockText -match [Regex]::Escape('"ru.rustore.installreferrer"')) {
        $staleRuStoreState = $true
    }
}

if (Test-Path $packageCache) {
    $staleRuStoreState = $staleRuStoreState -or [bool](
        Get-ChildItem $packageCache -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like "ru.rustore.installreferrer@*" } |
            Select-Object -First 1
    )
}

if ($staleRuStoreState) {
    Write-Host "Stale Unity Install Referrer package state detected; resetting generated Unity Library..." -ForegroundColor Yellow
    Remove-Item $lockPath -Force -ErrorAction SilentlyContinue
    if (Test-Path $libraryPath) {
        Remove-Item $libraryPath -Recurse -Force
    }
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
$playModeResults = Join-Path $artifacts "playmode-results.xml"
$playModeLog = Join-Path $artifacts "playmode.log"
$serializedLog = Join-Path $artifacts "serialized-validation.log"
$serializedReport = Join-Path $repoRoot "artifacts\agent-check\unity-serialized-validation.txt"
$productionBuildLog = Join-Path $artifacts "production-build.log"
$readinessLog = Join-Path $artifacts "readiness.log"
$readinessSource = Join-Path $projectPath "Library\NesbeisyaReleaseReadiness.txt"
$readinessCopy = Join-Path $artifacts "release-readiness.txt"

Remove-Item $testResults, $testLog, $playModeResults, $playModeLog, $serializedLog, $serializedReport, $productionBuildLog, $readinessLog, $readinessCopy -Force -ErrorAction SilentlyContinue

$totalSteps = if ($BuildAab) { 5 } elseif ($SkipReadiness) { 3 } else { 4 }
Write-Host "[1/$totalSteps] Unity compile + EditMode tests..." -ForegroundColor Yellow
$testArgs = @(
    "-batchmode",
    "-nographics",
    "-projectPath", ('"' + $projectPath + '"'),
    "-runTests",
    "-testPlatform", "EditMode",
    "-testResults", ('"' + $testResults + '"'),
    "-logFile", ('"' + $testLog + '"')
)
$testProcess = Start-Process -FilePath $unity -ArgumentList $testArgs -Wait -PassThru
$testExit = $testProcess.ExitCode
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

Write-Host ""
Write-Host "[2/$totalSteps] PlayMode startup smoke tests..." -ForegroundColor Yellow
$playModeArgs = @(
    "-batchmode",
    "-nographics",
    "-projectPath", ('"' + $projectPath + '"'),
    "-runTests",
    "-testPlatform", "PlayMode",
    "-testResults", ('"' + $playModeResults + '"'),
    "-logFile", ('"' + $playModeLog + '"')
)
$playModeProcess = Start-Process -FilePath $unity -ArgumentList $playModeArgs -Wait -PassThru
$playModeExit = $playModeProcess.ExitCode
if ($playModeExit -ne 0) {
    Write-Host "Unity PlayMode tests FAILED (exit $playModeExit)." -ForegroundColor Red
    Write-Host "Log: $playModeLog"
    if (Test-Path $playModeLog) { Get-Content $playModeLog -Tail 100 }
    exit $playModeExit
}
if (-not (Test-Path $playModeResults)) {
    throw "Unity exited successfully but did not create PlayMode test results: $playModeResults"
}
Write-Host "Unity PlayMode tests passed." -ForegroundColor Green

Write-Host ""
Write-Host "[3/$totalSteps] Validate scenes, prefabs and serialized references..." -ForegroundColor Yellow
$serializedArgs = @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", ('"' + $projectPath + '"'),
    "-executeMethod", "DontGetSidetracked.EditorTools.AgentProjectValidator.ValidateForAutomation",
    "-logFile", ('"' + $serializedLog + '"')
)
$serializedProcess = Start-Process -FilePath $unity -ArgumentList $serializedArgs -Wait -PassThru
$serializedExit = $serializedProcess.ExitCode
if ($serializedExit -ne 0) {
    Write-Host "Serialized project validation FAILED (exit $serializedExit)." -ForegroundColor Red
    Write-Host "Log: $serializedLog"
    if (Test-Path $serializedLog) { Get-Content $serializedLog -Tail 100 }
    exit $serializedExit
}
if (-not (Test-Path $serializedReport)) {
    throw "Serialized project validation completed but report file was not created: $serializedReport"
}
Write-Host "Serialized project validation passed." -ForegroundColor Green
Write-Host "Report: $serializedReport" -ForegroundColor Cyan

if (-not $SkipReadiness) {
    Write-Host ""
    Write-Host "[4/$totalSteps] Prepare Android release environment + readiness report..." -ForegroundColor Yellow
    $readinessArgs = @(
        "-batchmode",
        "-nographics",
        "-quit",
        "-projectPath", ('"' + $projectPath + '"'),
        "-executeMethod", "DontGetSidetracked.EditorTools.ReleaseReadinessReporter.Report",
        "-logFile", ('"' + $readinessLog + '"')
    )
    $readinessProcess = Start-Process -FilePath $unity -ArgumentList $readinessArgs -Wait -PassThru
    $readinessExit = $readinessProcess.ExitCode
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


if ($BuildAab) {
    Write-Host ""
    Write-Host "[5/$totalSteps] Build production Android AAB..." -ForegroundColor Yellow

    $releaseOutput = $env:NESBEISYA_RELEASE_OUTPUT
    if ([string]::IsNullOrWhiteSpace($releaseOutput)) {
        $releaseOutput = Join-Path $artifacts "android\nesbeisya-production.aab"
        $env:NESBEISYA_RELEASE_OUTPUT = $releaseOutput
    } elseif (-not [IO.Path]::IsPathRooted($releaseOutput)) {
        $releaseOutput = [IO.Path]::GetFullPath((Join-Path $repoRoot $releaseOutput))
        $env:NESBEISYA_RELEASE_OUTPUT = $releaseOutput
    }

    $releaseDirectory = Split-Path -Parent $releaseOutput
    if ($releaseDirectory) {
        New-Item -ItemType Directory -Force -Path $releaseDirectory | Out-Null
    }

    if ([string]::IsNullOrWhiteSpace($env:RELEASE_GIT_SHA) -and [string]::IsNullOrWhiteSpace($env:GITHUB_SHA)) {
        $git = Get-Command git -ErrorAction SilentlyContinue
        if ($git) {
            $resolvedSha = (& $git.Source -C $repoRoot rev-parse HEAD).Trim()
            if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($resolvedSha)) {
                $env:RELEASE_GIT_SHA = $resolvedSha
            }
        }
    }

    $buildArgs = @(
        "-batchmode",
        "-nographics",
        "-quit",
        "-projectPath", ('"' + $projectPath + '"'),
        "-executeMethod", "DontGetSidetracked.EditorTools.ProductionAndroidBuild.BuildFromCommandLine",
        "-logFile", ('"' + $productionBuildLog + '"')
    )
    $buildProcess = Start-Process -FilePath $unity -ArgumentList $buildArgs -Wait -PassThru
    $buildExit = $buildProcess.ExitCode
    if ($buildExit -ne 0) {
        Write-Host "Production Android AAB build FAILED (exit $buildExit)." -ForegroundColor Red
        Write-Host "Log: $productionBuildLog"
        if (Test-Path $productionBuildLog) { Get-Content $productionBuildLog -Tail 120 }
        exit $buildExit
    }

    $releaseMetadata = [IO.Path]::ChangeExtension($releaseOutput, ".release.json")
    if (-not (Test-Path $releaseOutput)) {
        throw "Production build completed but AAB was not created: $releaseOutput"
    }
    if (-not (Test-Path $releaseMetadata)) {
        throw "Production build completed but release metadata was not created: $releaseMetadata"
    }

    Write-Host "Production Android AAB created." -ForegroundColor Green
    Write-Host "AAB: $releaseOutput" -ForegroundColor Cyan
    Write-Host "Metadata: $releaseMetadata" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Release-candidate local checks completed." -ForegroundColor Green
Write-Host "Artifacts: $artifacts"
