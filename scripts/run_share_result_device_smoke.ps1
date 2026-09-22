param(
    [string]$PackageName = "ru.release.nesbeisya",
    [string]$UnityExe = $env:UNITY_EXE
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts\share-device-smoke"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

function Fail([string]$Message) { throw "Share-result device smoke failed: $Message" }

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
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $local -PathType Leaf)) { Fail "pull failed for $Name." }
    & $AdbPath -s $Serial shell rm -f $remote | Out-Null
    return $local
}

$adb = Resolve-Adb
$serial = Resolve-Device -AdbPath $adb
& $adb -s $serial shell input keyevent KEYCODE_WAKEUP | Out-Null
& $adb -s $serial shell wm dismiss-keyguard | Out-Null

$gamePid = (& $adb -s $serial shell pidof $PackageName 2>$null | Out-String).Trim()
if ([string]::IsNullOrWhiteSpace($gamePid)) { Fail "game process is not alive; expected the Training result screen from the previous stage." }

$before = Capture -AdbPath $adb -Serial $serial -Name "01-before-share"
$focusBefore = (& $adb -s $serial shell dumpsys window | Select-String -Pattern "mCurrentFocus|mFocusedApp" | Out-String).Trim()
$focusBefore | Set-Content -Path (Join-Path $artifactDir "focus-before.txt") -Encoding UTF8

# Result screen share button is the full-width lower button on the 1080x2400 release layout.
& $adb -s $serial shell input tap 540 2190 | Out-Null
Start-Sleep -Seconds 2
$sheet = Capture -AdbPath $adb -Serial $serial -Name "02-share-sheet"
$focusShare = (& $adb -s $serial shell dumpsys window | Select-String -Pattern "mCurrentFocus|mFocusedApp" | Out-String).Trim()
$focusShare | Set-Content -Path (Join-Path $artifactDir "focus-share.txt") -Encoding UTF8

& $adb -s $serial shell input keyevent KEYCODE_BACK | Out-Null
Start-Sleep -Seconds 2
$after = Capture -AdbPath $adb -Serial $serial -Name "03-after-return"
$focusAfter = (& $adb -s $serial shell dumpsys window | Select-String -Pattern "mCurrentFocus|mFocusedApp" | Out-String).Trim()
$focusAfter | Set-Content -Path (Join-Path $artifactDir "focus-after.txt") -Encoding UTF8

$pidAfter = (& $adb -s $serial shell pidof $PackageName 2>$null | Out-String).Trim()
if ([string]::IsNullOrWhiteSpace($pidAfter)) { Fail "game process died after returning from Android share sheet." }

$logs = (& $adb -s $serial logcat -d -v threadtime | Out-String)
$logs | Set-Content -Path (Join-Path $artifactDir "logcat.txt") -Encoding UTF8
if ($logs -match "(?is)FATAL EXCEPTION.*Process:\s*ru\.release\.nesbeisya|ANR in\s+ru\.release\.nesbeisya") {
    Fail "fatal exception/ANR detected during share flow."
}

@(
    "Device: $serial",
    "Game pid before share: $gamePid",
    "Game pid after return: $pidAfter",
    "Expected artifacts: result screen -> Android share sheet -> same result screen after Back."
    "Focus before: $focusBefore",
    "Focus during share: $focusShare",
    "Focus after: $focusAfter"
) | Set-Content -Path (Join-Path $artifactDir "share.txt") -Encoding UTF8

Write-Host "Android share-result smoke completed."
