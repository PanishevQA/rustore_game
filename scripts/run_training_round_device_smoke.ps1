param(
    [string]$PackageName = "ru.release.nesbeisya",
    [string]$UnityExe = $env:UNITY_EXE
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts\gameplay-device-smoke-round"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

function Fail([string]$Message) { throw "Training round device smoke failed: $Message" }

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

function Find-Marker([string]$ImagePath, [string]$Kind) {
    Add-Type -AssemblyName System.Drawing
    $bitmap = [System.Drawing.Bitmap]::FromFile($ImagePath)
    try {
        $xMin = [int]($bitmap.Width * 0.04)
        $xMax = [int]($bitmap.Width * 0.96)
        $yMin = [int]($bitmap.Height * 0.20)
        $yMax = [int]($bitmap.Height * 0.82)
        [long]$sumX = 0
        [long]$sumY = 0
        [long]$count = 0

        for ($y = $yMin; $y -le $yMax; $y += 2) {
            for ($x = $xMin; $x -le $xMax; $x += 2) {
                $pixel = $bitmap.GetPixel($x, $y)
                $match = $false
                if ($Kind -eq "cyan") {
                    $match = $pixel.R -lt 90 -and $pixel.G -gt 150 -and $pixel.B -gt 170
                } elseif ($Kind -eq "yellow") {
                    $match = $pixel.R -gt 170 -and $pixel.G -gt 120 -and $pixel.B -lt 140
                }
                if ($match) {
                    $sumX += $x
                    $sumY += $y
                    $count++
                }
            }
        }

        if ($count -lt 20) { Fail "could not detect $Kind route marker in $ImagePath; matched pixels=$count." }
        return [PSCustomObject]@{
            X = [int][Math]::Round($sumX / [double]$count)
            Y = [int][Math]::Round($sumY / [double]$count)
            Count = $count
        }
    }
    finally {
        $bitmap.Dispose()
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
$homeScreenshot = Capture -AdbPath $adb -Serial $serial -Name "01-home"

$trainingX = [int]($width * 0.50)
$trainingY = [int]($height * 0.715)
& $adb -s $serial shell input tap $trainingX $trainingY | Out-Null
Start-Sleep -Seconds 2
$menuScreenshot = Capture -AdbPath $adb -Serial $serial -Name "02-training-menu"

$startButtonX = [int]($width * 0.50)
$startButtonY = [int]($height * 0.85)
& $adb -s $serial shell input tap $startButtonX $startButtonY | Out-Null
Start-Sleep -Milliseconds 700
$routeVisibleScreenshot = Capture -AdbPath $adb -Serial $serial -Name "03-route-visible"

# Easy display is currently 3.5s plus the 3/2/1 countdown. Six seconds reliably lands in Drawing.
Start-Sleep -Seconds 6
$drawingScreenshot = Capture -AdbPath $adb -Serial $serial -Name "04-drawing-ready"

$cyan = Find-Marker -ImagePath $drawingScreenshot -Kind "cyan"
$yellow = Find-Marker -ImagePath $drawingScreenshot -Kind "yellow"
Write-Host "Detected cyan start: $($cyan.X),$($cyan.Y) pixels=$($cyan.Count)"
Write-Host "Detected yellow end: $($yellow.X),$($yellow.Y) pixels=$($yellow.Count)"

& $adb -s $serial shell input swipe $($cyan.X) $($cyan.Y) $($yellow.X) $($yellow.Y) 1200 | Out-Null
if ($LASTEXITCODE -ne 0) { Fail "adb training swipe failed." }
Start-Sleep -Seconds 3
$resultScreenshot = Capture -AdbPath $adb -Serial $serial -Name "05-result"

$logs = (& $adb -s $serial logcat -d -v threadtime | Out-String)
$logs | Set-Content -Path (Join-Path $artifactDir "logcat.txt") -Encoding UTF8
if ($logs -match "(?is)FATAL EXCEPTION.*Process:\s*ru\.release\.nesbeisya|ANR in\s+ru\.release\.nesbeisya") {
    Fail "fatal exception/ANR detected during Training round."
}
if ($logs -match "Could not produce class with ID 115|Strip Engine Code") {
    Fail "engine-stripping regression detected during Training round."
}

@(
    "Device: $serial",
    "Screen: $width x $height",
    "Training tap: $trainingX,$trainingY",
    "Start button tap: $startButtonX,$startButtonY",
    "Detected start: $($cyan.X),$($cyan.Y) matched=$($cyan.Count)",
    "Detected end: $($yellow.X),$($yellow.Y) matched=$($yellow.Count)",
    "Gesture duration: 1200 ms",
    "Expected final artifact: 05-result.png showing Training result screen."
) | Set-Content -Path (Join-Path $artifactDir "round.txt") -Encoding UTF8

Write-Host "Physical Training round smoke completed; result screenshot captured."
