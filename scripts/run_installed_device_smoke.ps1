param(
    [string]$PackageName = "ru.release.nesbeisya",
    [string]$ExpectedVersionName = "1.0",
    [int]$ExpectedVersionCode = 1,
    [string]$DeviceSerial,
    [string]$UnityExe = $env:UNITY_EXE
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts\device-runtime-smoke"
$reportPath = Join-Path $artifactDir "device-runtime-smoke.txt"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
Remove-Item $reportPath -Force -ErrorAction SilentlyContinue

function Report([string]$Message) {
    $Message | Tee-Object -FilePath $reportPath -Append | Out-Host
}

function Fail([string]$Message) {
    Report "FAIL: $Message"
    throw "Installed Android smoke failed: $Message"
}

function Resolve-Adb {
    $command = Get-Command adb -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $unityCandidate = $UnityExe
    if ([string]::IsNullOrWhiteSpace($unityCandidate)) {
        $versionFile = Join-Path $repoRoot "UnityProject\ProjectSettings\ProjectVersion.txt"
        if (Test-Path $versionFile) {
            $line = Get-Content $versionFile | Where-Object { $_ -match "^m_EditorVersion:" } | Select-Object -First 1
            if ($line) {
                $version = ($line -split ":", 2)[1].Trim()
                $unityCandidate = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
            }
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($unityCandidate)) {
        $expandedUnity = [Environment]::ExpandEnvironmentVariables($unityCandidate)
        if (Test-Path $expandedUnity) {
            $editorDir = Split-Path -Parent $expandedUnity
            $embedded = Join-Path $editorDir "Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
            if (Test-Path $embedded) { return (Resolve-Path $embedded).Path }
        }
    }

    Fail "adb was not found in PATH or in the pinned Unity Android SDK."
}

function Resolve-Device([string]$AdbPath) {
    $lines = & $AdbPath devices
    if ($LASTEXITCODE -ne 0) { Fail "adb devices failed with exit code $LASTEXITCODE." }

    $devices = @()
    foreach ($line in $lines) {
        if ($line -match "^([^\s]+)\s+device\s*$") { $devices += $Matches[1] }
    }

    if (-not [string]::IsNullOrWhiteSpace($DeviceSerial)) {
        if ($devices -notcontains $DeviceSerial) { Fail "requested device '$DeviceSerial' is not connected and authorized." }
        return $DeviceSerial
    }

    if ($devices.Count -eq 0) { Fail "no authorized Android device is connected." }
    if ($devices.Count -gt 1) { Fail "multiple Android devices are connected; specify -DeviceSerial." }
    return $devices[0]
}

function Get-AppPid([string]$AdbPath, [string]$Serial, [string]$Package) {
    $raw = (& $AdbPath -s $Serial shell pidof $Package 2>$null | Out-String).Trim()
    if (-not [string]::IsNullOrWhiteSpace($raw)) { return ($raw -split "\s+")[0] }

    $ps = (& $AdbPath -s $Serial shell ps -A 2>$null | Out-String)
    foreach ($line in ($ps -split "\r?\n")) {
        if ($line -match ("\s" + [regex]::Escape($Package) + "$")) {
            $parts = ($line -split "\s+") | Where-Object { $_ }
            if ($parts.Count -ge 2) { return $parts[1] }
        }
    }
    return ""
}

function Assert-NoFatal([string]$AdbPath, [string]$Serial, [string]$Package) {
    $logs = (& $AdbPath -s $Serial logcat -d -v brief | Out-String)
    $fatalForPackage = $logs -match ("(?is)(FATAL EXCEPTION|ANR in|Process:\s*" + [regex]::Escape($Package))
    if ($fatalForPackage) {
        $logPath = Join-Path $artifactDir "logcat-failure.txt"
        $logs | Set-Content -Path $logPath -Encoding UTF8
        Fail "fatal exception/ANR marker detected after app launch. See artifacts/device-runtime-smoke/logcat-failure.txt."
    }
}

$adb = Resolve-Adb
$serial = Resolve-Device -AdbPath $adb

Report "NESBEISYA - INSTALLED DEVICE RUNTIME SMOKE"
Report ("Generated UTC: " + [DateTime]::UtcNow.ToString("o"))
Report "ADB: $adb"
Report "Device serial: $serial"

$model = (& $adb -s $serial shell getprop ro.product.model | Out-String).Trim()
$release = (& $adb -s $serial shell getprop ro.build.version.release | Out-String).Trim()
$sdk = (& $adb -s $serial shell getprop ro.build.version.sdk | Out-String).Trim()
Report "Device model: $model"
Report "Android: $release (API $sdk)"

$listed = (& $adb -s $serial shell pm list packages $PackageName | Out-String).Trim()
if ($listed -notmatch ("package:" + [regex]::Escape($PackageName))) {
    Fail "package $PackageName is not installed."
}
Report "Package installed: $PackageName"

$dump = (& $adb -s $serial shell dumpsys package $PackageName | Out-String)
if ($dump -notmatch ("versionName=" + [regex]::Escape($ExpectedVersionName) + "(\s|$)")) {
    Fail "installed versionName does not match expected $ExpectedVersionName."
}
if ($dump -notmatch ("versionCode=" + $ExpectedVersionCode + "(\s|$)")) {
    Fail "installed versionCode does not match expected $ExpectedVersionCode."
}
if ($dump -match "\bDEBUGGABLE\b") {
    Fail "installed package is marked DEBUGGABLE; expected signed non-Development release build."
}
Report "Version: $ExpectedVersionName ($ExpectedVersionCode)"
Report "Release flag: non-debuggable PASS"

& $adb -s $serial shell am force-stop $PackageName | Out-Null
& $adb -s $serial logcat -c | Out-Null
& $adb -s $serial shell monkey -p $PackageName -c android.intent.category.LAUNCHER 1 | Out-Null
if ($LASTEXITCODE -ne 0) { Fail "launcher start failed." }
Start-Sleep -Seconds 8

$appPid = Get-AppPid -AdbPath $adb -Serial $serial -Package $PackageName
if ([string]::IsNullOrWhiteSpace($appPid)) { Fail "app process is not alive after launcher start." }
Report "Launcher start: PASS (pid $appPid)"
Assert-NoFatal -AdbPath $adb -Serial $serial -Package $PackageName
Report "Immediate crash/ANR scan: PASS"

& $adb -s $serial shell input keyevent 3 | Out-Null
Start-Sleep -Seconds 2
& $adb -s $serial shell monkey -p $PackageName -c android.intent.category.LAUNCHER 1 | Out-Null
if ($LASTEXITCODE -ne 0) { Fail "foreground relaunch failed after HOME." }
Start-Sleep -Seconds 4
$appPid = Get-AppPid -AdbPath $adb -Serial $serial -Package $PackageName
if ([string]::IsNullOrWhiteSpace($appPid)) { Fail "app process is not alive after background/foreground cycle." }
Assert-NoFatal -AdbPath $adb -Serial $serial -Package $PackageName
Report "Background/foreground cycle: PASS"

$deeplink = "nesbeisya://challenge/not-a-valid-token"
& $adb -s $serial shell am start -W -n "$PackageName/com.unity3d.player.UnityPlayerActivity" -a android.intent.action.VIEW -d $deeplink | Out-Null
if ($LASTEXITCODE -ne 0) { Fail "deeplink activity start command failed." }
Start-Sleep -Seconds 4
$appPid = Get-AppPid -AdbPath $adb -Serial $serial -Package $PackageName
if ([string]::IsNullOrWhiteSpace($appPid)) { Fail "app process died after malformed challenge deeplink." }
Assert-NoFatal -AdbPath $adb -Serial $serial -Package $PackageName
Report "Malformed installed deeplink safety: PASS"

Report "STATUS: AUTOMATED INSTALLED DEVICE SMOKE PASS"
