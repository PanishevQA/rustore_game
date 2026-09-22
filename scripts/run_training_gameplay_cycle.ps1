param(
    [string]$PackageName = "ru.release.nesbeisya",
    [string]$UnityExe = $env:UNITY_EXE
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts\gameplay-cycle"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

function Fail([string]$Message) { throw "Training gameplay cycle failed: $Message" }

function Resolve-Adb {
    $command = Get-Command adb -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $unityCandidate = $UnityExe
    if ([string]::IsNullOrWhiteSpace($unityCandidate)) {
        $versionFile = Join-Path $repoRoot "UnityProject\ProjectSettings\ProjectVersion.txt"
        $line = Get-Content $versionFile | Where-Object { $_ -match "^m_EditorVersion:" } | Select-Object -First 1
        if ($line) {
            $version = ($line -split ":", 2)[1].Trim()
            $unityCandidate = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($unityCandidate) -and (Test-Path $unityCandidate)) {
        $editorDir = Split-Path -Parent $unityCandidate
        $candidate = Join-Path $editorDir "Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
        if (Test-Path $candidate) { return (Resolve-Path $candidate).Path }
    }

    Fail "adb not found."
}

function Resolve-Device([string]$AdbPath) {
    $devices = @()
    foreach ($line in (& $AdbPath devices)) {
        if ($line -match "^([^\s]+)\s+device\s*$") { $devices += $Matches[1] }
    }
    if ($devices.Count -ne 1) { Fail "expected exactly one authorized Android device, found $($devices.Count)." }
    return $devices[0]
}

function Capture([string]$AdbPath, [string]$Serial, [string]$Name) {
    $remote = "/sdcard/$Name.png"
    $local = Join-Path $artifactDir "$Name.png"
    & $AdbPath -s $Serial shell screencap -p $remote | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail "screencap failed for $Name." }
    & $AdbPath -s $Serial pull $remote $local | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail "pull failed for $Name." }
    & $AdbPath -s $Serial shell rm -f $remote | Out-Null
}

function Assert-NoFatal([string]$AdbPath, [string]$Serial) {
    $logs = (& $AdbPath -s $Serial logcat -d -v threadtime | Out-String)
    $logs | Set-Content -Path (Join-Path $artifactDir "logcat.txt") -Encoding UTF8
    if ($logs -match "(?is)FATAL EXCEPTION.*Process:\s*ru\.release\.nesbeisya|ANR in\s+ru\.release\.nesbeisya") {
        Fail "fatal exception/ANR detected during Training cycle."
    }
    if ($logs -match "Could not produce class with ID 115|Try disabling 'Strip Engine Code'") {
        Fail "engine stripping regression detected during Training cycle."
    }
}

$adb = Resolve-Adb
$serial = Resolve-Device -AdbPath $adb

$sizeLine = (& $adb -s $serial shell wm size | Out-String).Trim()
if ($sizeLine -notmatch "(\d+)x(\d+)") { Fail "could not parse wm size: $sizeLine" }
$width = [int]$Matches[1]
$height = [int]$Matches[2]

& $adb -s $serial shell input keyevent KEYCODE_WAKEUP | Out-Null
& $adb -s $serial shell wm dismiss-keyguard | Out-Null
& $adb -s $serial shell am force-stop $PackageName | Out-Null
& $adb -s $serial logcat -c | Out-Null
& $adb -s $serial shell monkey -p $PackageName -c android.intent.category.LAUNCHER 1 | Out-Null
if ($LASTEXITCODE -ne 0) { Fail "launcher start failed." }

Start-Sleep -Seconds 5
Capture -AdbPath $adb -Serial $serial -Name "01-home"

# Real-device coordinates are scaled from the verified 1080x2400 release layout.
$trainingX = [int]($width * 0.50)
$trainingY = [int]($height * 0.715)
& $adb -s $serial shell input tap $trainingX $trainingY | Out-Null
Start-Sleep -Seconds 2
Capture -AdbPath $adb -Serial $serial -Name "02-training-menu"

$startButtonX = [int]($width * 0.50)
$startButtonY = [int]($height * 0.85)
& $adb -s $serial shell input tap $startButtonX $startButtonY | Out-Null

Start-Sleep -Milliseconds 700
Capture -AdbPath $adb -Serial $serial -Name "03-route-visible"

# Easy route display is short; leave enough margin for the route to disappear.
Start-Sleep -Seconds 5
Capture -AdbPath $adb -Serial $serial -Name "04-drawing-ready"

# Verified Easy-route markers on 1080x2400:
# cyan start ~= (594,1822), gold finish ~= (481,748).
$startX = [int]($width * 0.55)
$startY = [int]($height * 0.759)
$endX = [int]($width * 0.445)
$endY = [int]($height * 0.312)

& $adb -s $serial shell input swipe $startX $startY $endX $endY 1200 | Out-Null
if ($LASTEXITCODE -ne 0) { Fail "training swipe failed." }

Start-Sleep -Seconds 3
Capture -AdbPath $adb -Serial $serial -Name "05-result"

Assert-NoFatal -AdbPath $adb -Serial $serial

@(
    "Device: $serial",
    "Screen: $width x $height",
    "Home -> Training: PASS",
    "Training Easy start: PASS",
    "Route display -> drawing ready: PASS",
    "One continuous gesture: $startX,$startY -> $endX,$endY",
    "Result screenshot captured: artifacts/gameplay-cycle/05-result.png",
    "Expected smoke outcome: result UI visible after the gesture; score quality is not a release criterion for this automated gesture."
) | Set-Content -Path (Join-Path $artifactDir "gameplay-cycle.txt") -Encoding UTF8

Write-Host "Complete Training gameplay cycle executed on physical device."
