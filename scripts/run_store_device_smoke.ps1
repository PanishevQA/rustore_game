param(
    [string]$PackageName = "ru.release.nesbeisya",
    [string]$UnityExe = $env:UNITY_EXE
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts\store-device-smoke"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

function Fail([string]$Message) { throw "Store device smoke failed: $Message" }

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
& $adb -s $serial shell am force-stop $PackageName | Out-Null
& $adb -s $serial logcat -c | Out-Null
& $adb -s $serial shell monkey -p $PackageName -c android.intent.category.LAUNCHER 1 | Out-Null
if ($LASTEXITCODE -ne 0) { Fail "launcher start failed." }
Start-Sleep -Seconds 5
$homeScreenshot = Capture -AdbPath $adb -Serial $serial -Name "01-home"

$packages = (& $adb -s $serial shell pm list packages | Out-String)
($packages -split "\r?\n" | Where-Object { $_ -match "(?i)rustore|vk\.store" }) |
    Set-Content -Path (Join-Path $artifactDir "rustore-packages.txt") -Encoding UTF8

# Home store card center on the verified 1080x2400 release layout.
& $adb -s $serial shell input tap 780 1940 | Out-Null
Start-Sleep -Seconds 1
$loadingScreenshot = Capture -AdbPath $adb -Serial $serial -Name "02-store-loading"
Start-Sleep -Seconds 6
$resolvedScreenshot = Capture -AdbPath $adb -Serial $serial -Name "03-store-resolved"

$gamePid = (& $adb -s $serial shell pidof $PackageName 2>$null | Out-String).Trim()
if ([string]::IsNullOrWhiteSpace($gamePid)) { Fail "game process died while opening Store." }

$logs = (& $adb -s $serial logcat -d -v threadtime | Out-String)
$logs | Set-Content -Path (Join-Path $artifactDir "logcat.txt") -Encoding UTF8
$storeLines = ($logs -split "\r?\n") | Where-Object {
    $_ -match "(?i)(RuStore|Pay|Store unavailable|catalog|product|purchase)"
}
$storeLines | Set-Content -Path (Join-Path $artifactDir "store-log-lines.txt") -Encoding UTF8

if ($logs -match "(?is)FATAL EXCEPTION.*Process:\s*ru\.release\.nesbeisya|ANR in\s+ru\.release\.nesbeisya") {
    Fail "fatal exception/ANR detected during Store flow."
}

@(
    "Device: $serial",
    "Game pid: $gamePid",
    "Home -> Store tap: 780,1940",
    "Expected artifacts: Home, Store loading, Store resolved/catalog state.",
    "No purchase action is performed by this smoke."
) | Set-Content -Path (Join-Path $artifactDir "store.txt") -Encoding UTF8

Write-Host "Store catalog device smoke completed."
