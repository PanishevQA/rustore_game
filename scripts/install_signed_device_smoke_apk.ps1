param(
    [string]$ApkPath,
    [string]$DeviceSerial,
    [string]$UnityExe = $env:UNITY_EXE,
    [switch]$SkipLaunchCheck,
    [string]$ChallengeUri
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Fail([string]$Message) {
    throw "Device-smoke APK install failed: $Message"
}

function Resolve-FullPath([string]$Value) {
    $expanded = [Environment]::ExpandEnvironmentVariables($Value)
    if ([IO.Path]::IsPathRooted($expanded)) {
        return [IO.Path]::GetFullPath($expanded)
    }
    return [IO.Path]::GetFullPath((Join-Path $repoRoot $expanded))
}

function Resolve-Adb {
    $command = Get-Command adb -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

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
            if (Test-Path $embedded) {
                return (Resolve-Path $embedded).Path
            }
        }
    }

    Fail "adb was not found in PATH or in the pinned Unity Android SDK."
}

if ([string]::IsNullOrWhiteSpace($ApkPath)) {
    $ApkPath = "artifacts\release-candidate\android-device-smoke\nesbeisya-device-smoke.apk"
}
$apk = Resolve-FullPath $ApkPath
if (-not (Test-Path $apk -PathType Leaf)) {
    Fail "APK was not found: $apk"
}

$apkSha256 = (Get-FileHash -Path $apk -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumPath = $apk + ".sha256"
if (Test-Path $checksumPath -PathType Leaf) {
    $line = (Get-Content $checksumPath | Select-Object -First 1).Trim()
    $expected = ($line -split "\s+")[0].ToLowerInvariant()
    if ($expected -notmatch "^[0-9a-f]{64}$") {
        Fail "SHA-256 sidecar is malformed: $checksumPath"
    }

    if ($apkSha256 -ne $expected) {
        Fail "APK SHA-256 does not match its sidecar. Expected $expected, got $apkSha256."
    }
    Write-Host "APK SHA-256 verified: $apkSha256" -ForegroundColor Green
}
else {
    Write-Warning "SHA-256 sidecar is missing; installation will continue without checksum verification."
}

$adb = Resolve-Adb
Write-Host "ADB: $adb" -ForegroundColor Cyan

$deviceLines = & $adb devices
if ($LASTEXITCODE -ne 0) {
    Fail "adb devices failed with exit code $LASTEXITCODE."
}

$devices = @()
foreach ($line in $deviceLines) {
    if ($line -match "^([^\s]+)\s+device\s*$") {
        $devices += $Matches[1]
    }
}

if ([string]::IsNullOrWhiteSpace($DeviceSerial)) {
    if ($devices.Count -eq 0) {
        Fail "no authorized Android device is connected. Enable USB debugging and accept the RSA prompt."
    }
    if ($devices.Count -gt 1) {
        Fail "multiple Android devices are connected. Re-run with -DeviceSerial <serial>."
    }
    $DeviceSerial = $devices[0]
}
elseif ($devices -notcontains $DeviceSerial) {
    Fail "requested device '$DeviceSerial' is not connected and authorized."
}

$packageName = "ru.release.nesbeisya"
$model = ((& $adb -s $DeviceSerial shell getprop ro.product.model) | Out-String).Trim()
$apiLevel = ((& $adb -s $DeviceSerial shell getprop ro.build.version.sdk) | Out-String).Trim()
$androidVersion = ((& $adb -s $DeviceSerial shell getprop ro.build.version.release) | Out-String).Trim()
Write-Host "Device: $model | Android $androidVersion | API $apiLevel" -ForegroundColor Cyan

Write-Host "Installing on device: $DeviceSerial" -ForegroundColor Cyan
& $adb -s $DeviceSerial install -r $apk
if ($LASTEXITCODE -ne 0) {
    Fail "adb install failed with exit code $LASTEXITCODE. If another build with a different signature is installed, uninstall it explicitly before retrying."
}

$packageDump = & $adb -s $DeviceSerial shell dumpsys package $packageName
if ($LASTEXITCODE -ne 0 -or -not ($packageDump -match "Package \[$packageName\]")) {
    Fail "installed package '$packageName' could not be verified with dumpsys package."
}

$versionName = (($packageDump | Select-String -Pattern "versionName=" | Select-Object -First 1).Line -replace ".*versionName=", "").Trim()
$versionCodeMatch = $packageDump | Select-String -Pattern "versionCode=([0-9]+)" | Select-Object -First 1
$versionCode = if ($versionCodeMatch -and $versionCodeMatch.Matches.Count -gt 0) { $versionCodeMatch.Matches[0].Groups[1].Value } else { "unknown" }

$launchPid = ""
$deeplinkResult = "not requested"
if (-not $SkipLaunchCheck) {
    Write-Host "Launching package through the Android launcher intent..." -ForegroundColor Cyan
    $launchOutput = & $adb -s $DeviceSerial shell monkey -p $packageName -c android.intent.category.LAUNCHER 1
    if ($LASTEXITCODE -ne 0 -or ($launchOutput -join [Environment]::NewLine) -match "No activities found") {
        Fail "launcher start failed for $packageName."
    }

    Start-Sleep -Seconds 3
    $launchPid = ((& $adb -s $DeviceSerial shell pidof $packageName) | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($launchPid)) {
        Fail "package process is not alive three seconds after launcher start."
    }
    Write-Host "Launch preflight passed. PID: $launchPid" -ForegroundColor Green

    if (-not [string]::IsNullOrWhiteSpace($ChallengeUri)) {
        if (-not $ChallengeUri.StartsWith("nesbeisya://challenge/", [StringComparison]::OrdinalIgnoreCase)) {
            Fail "ChallengeUri must start with nesbeisya://challenge/."
        }

        Write-Host "Testing installed-game challenge deeplink..." -ForegroundColor Cyan
        & $adb -s $DeviceSerial shell am start -W -a android.intent.action.VIEW -d $ChallengeUri -p $packageName | Out-Host
        if ($LASTEXITCODE -ne 0) {
            Fail "challenge deeplink start failed."
        }

        Start-Sleep -Seconds 2
        $pidAfterDeeplink = ((& $adb -s $DeviceSerial shell pidof $packageName) | Out-String).Trim()
        if ([string]::IsNullOrWhiteSpace($pidAfterDeeplink)) {
            Fail "package process died after challenge deeplink."
        }
        $deeplinkResult = "PASS"
        Write-Host "Challenge deeplink process-survival check passed." -ForegroundColor Green
    }
}

$reportDir = Join-Path $repoRoot "artifacts\device-smoke"
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
$reportPath = Join-Path $reportDir "connected-device-preflight.txt"
@(
    "Connected device preflight: PASS",
    "TimestampUtc=$([DateTime]::UtcNow.ToString("o"))",
    "DeviceSerial=$DeviceSerial",
    "Model=$model",
    "AndroidVersion=$androidVersion",
    "ApiLevel=$apiLevel",
    "Package=$packageName",
    "VersionName=$versionName",
    "VersionCode=$versionCode",
    "ApkSha256=$apkSha256",
    "LaunchCheck=$(if ($SkipLaunchCheck) { "SKIPPED" } else { "PASS" })",
    "LaunchPid=$launchPid",
    "ChallengeDeeplink=$deeplinkResult"
) | Set-Content -Path $reportPath -Encoding UTF8

Write-Host "Signed device-smoke APK installed successfully." -ForegroundColor Green
Write-Host "Package: $packageName"
Write-Host "Version: $versionName ($versionCode)"
Write-Host "Preflight report: $reportPath" -ForegroundColor Cyan
Write-Host "Next: complete the manual RuStore/device checks from docs/DEVICE_SMOKE_TEST.md."
