param(
    [string]$PackageName = "ru.release.nesbeisya",
    [string]$UnityExe = $env:UNITY_EXE
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts\gameplay-device-smoke"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

function Fail([string]$Message) { throw "Gameplay device smoke finish failed: $Message" }

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

$adb = Resolve-Adb
$serial = Resolve-Device -AdbPath $adb

& $adb -s $serial shell input keyevent KEYCODE_WAKEUP | Out-Null
& $adb -s $serial shell wm dismiss-keyguard | Out-Null
& $adb -s $serial shell monkey -p $PackageName -c android.intent.category.LAUNCHER 1 | Out-Null
Start-Sleep -Seconds 1
Capture -AdbPath $adb -Serial $serial -Name "06-before-gesture"

# Coordinates come from the immediately preceding real-device prepare screenshot
# (1080x2400): cyan start marker -> gold end marker. A straight stroke is deliberate:
# this smoke validates round completion/result UX, not player skill/score quality.
$startX = 594
$startY = 1822
$endX = 481
$endY = 748
& $adb -s $serial shell input swipe $startX $startY $endX $endY 1200 | Out-Null
if ($LASTEXITCODE -ne 0) { Fail "adb swipe failed." }

Start-Sleep -Seconds 3
Capture -AdbPath $adb -Serial $serial -Name "07-result"

$logs = (& $adb -s $serial logcat -d -v threadtime | Out-String)
$logs | Set-Content -Path (Join-Path $artifactDir "finish-logcat.txt") -Encoding UTF8
if ($logs -match "(?is)FATAL EXCEPTION.*Process:\s*ru\.release\.nesbeisya|ANR in\s+ru\.release\.nesbeisya") {
    Fail "fatal exception/ANR detected after Training gesture."
}

@(
    "Device: $serial",
    "Gesture: $startX,$startY -> $endX,$endY (1200 ms)",
    "Expected: result screen after one continuous Training stroke.",
    "See 06-before-gesture.png and 07-result.png."
) | Set-Content -Path (Join-Path $artifactDir "finish.txt") -Encoding UTF8

Write-Host "Training gesture submitted; result screenshot captured."
