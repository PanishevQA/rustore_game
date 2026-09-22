param(
    [string]$PackageName = "ru.release.nesbeisya",
    [string]$UnityExe = $env:UNITY_EXE
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts\gameplay-device-smoke"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

function Fail([string]$Message) { throw "Gameplay device smoke prepare failed: $Message" }

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

$sizeLine = (& $adb -s $serial shell wm size | Out-String).Trim()
if ($sizeLine -notmatch "(\d+)x(\d+)") { Fail "could not parse wm size: $sizeLine" }
$width = [int]$Matches[1]
$height = [int]$Matches[2]

& $adb -s $serial shell input keyevent KEYCODE_WAKEUP | Out-Null
& $adb -s $serial shell wm dismiss-keyguard | Out-Null
& $adb -s $serial shell am force-stop $PackageName | Out-Null
& $adb -s $serial shell monkey -p $PackageName -c android.intent.category.LAUNCHER 1 | Out-Null
if ($LASTEXITCODE -ne 0) { Fail "launcher start failed." }
Start-Sleep -Seconds 5
Capture -AdbPath $adb -Serial $serial -Name "01-home"

$trainingX = [int]($width * 0.50)
$trainingY = [int]($height * 0.715)
& $adb -s $serial shell input tap $trainingX $trainingY | Out-Null
Start-Sleep -Seconds 2
Capture -AdbPath $adb -Serial $serial -Name "02-training-menu"

$startX = [int]($width * 0.50)
$startY = [int]($height * 0.85)
& $adb -s $serial shell input tap $startX $startY | Out-Null

Start-Sleep -Milliseconds 700
Capture -AdbPath $adb -Serial $serial -Name "03-route-visible"
Start-Sleep -Seconds 3
Capture -AdbPath $adb -Serial $serial -Name "04-route-transition"
Start-Sleep -Seconds 4
Capture -AdbPath $adb -Serial $serial -Name "05-drawing-ready"

$report = @(
    "Device: $serial",
    "Screen: $width x $height",
    "Training tap: $trainingX,$trainingY",
    "Start tap: $startX,$startY",
    "State intentionally left active after 05-drawing-ready.png for the follow-up gesture."
)
$report | Set-Content -Path (Join-Path $artifactDir "prepare.txt") -Encoding UTF8
Write-Host "Training round prepared and left active for follow-up gesture."
