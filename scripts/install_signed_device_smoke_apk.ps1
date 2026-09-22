param(
    [string]$ApkPath,
    [string]$DeviceSerial,
    [string]$UnityExe = $env:UNITY_EXE
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

$checksumPath = $apk + ".sha256"
if (Test-Path $checksumPath -PathType Leaf) {
    $line = (Get-Content $checksumPath | Select-Object -First 1).Trim()
    $expected = ($line -split "\s+")[0].ToLowerInvariant()
    if ($expected -notmatch "^[0-9a-f]{64}$") {
        Fail "SHA-256 sidecar is malformed: $checksumPath"
    }

    $actual = (Get-FileHash -Path $apk -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected) {
        Fail "APK SHA-256 does not match its sidecar. Expected $expected, got $actual."
    }
    Write-Host "APK SHA-256 verified: $actual" -ForegroundColor Green
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

Write-Host "Installing on device: $DeviceSerial" -ForegroundColor Cyan
& $adb -s $DeviceSerial install -r $apk
if ($LASTEXITCODE -ne 0) {
    Fail "adb install failed with exit code $LASTEXITCODE. If another build with a different signature is installed, uninstall it explicitly before retrying."
}

Write-Host "Signed device-smoke APK installed successfully." -ForegroundColor Green
Write-Host "Package: ru.release.nesbeisya"
Write-Host "Next: run the physical-device checks from docs/RUSTORE_RELEASE_CHECKLIST.md."
