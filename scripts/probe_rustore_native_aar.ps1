param(
    [string]$UnityExe = $env:UNITY_EXE
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot "artifacts\android-integration-probe"
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

$projectVersion = Get-Content (Join-Path $repoRoot "UnityProject\ProjectSettings\ProjectVersion.txt") |
    Where-Object { $_ -match "^m_EditorVersion:" } |
    Select-Object -First 1
if (-not $projectVersion) { throw "Could not resolve Unity version." }
$version = ($projectVersion -split ":", 2)[1].Trim()

if ([string]::IsNullOrWhiteSpace($UnityExe)) {
    $UnityExe = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
}
if (-not (Test-Path $UnityExe)) { throw "Unity executable not found: $UnityExe" }

$javaBin = Join-Path (Split-Path -Parent $UnityExe) "Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin"
$jarExe = Join-Path $javaBin "jar.exe"
$javapExe = Join-Path $javaBin "javap.exe"
if (-not (Test-Path $jarExe) -or -not (Test-Path $javapExe)) {
    throw "Unity Android OpenJDK tools were not found under $javaBin"
}

$gradleRoot = Join-Path $env:USERPROFILE ".gradle\caches\modules-2\files-2.1\ru.rustore.sdk"
if (-not (Test-Path $gradleRoot)) {
    throw "RuStore Gradle cache not found after Android build: $gradleRoot"
}

$work = Join-Path $artifactRoot "aar-inspection"
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $work | Out-Null

$archives = New-Object System.Collections.Generic.List[object]
$sourceFiles = Get-ChildItem $gradleRoot -Recurse -File |
    Where-Object { $_.Extension -in @(".aar", ".jar") }

foreach ($source in $sourceFiles) {
    if ($source.Extension -eq ".jar") {
        $archives.Add([PSCustomObject]@{ Source = $source.FullName; Jar = $source.FullName })
        continue
    }

    $safeName = ($source.FullName -replace '[^A-Za-z0-9._-]', '_')
    $extract = Join-Path $work $safeName
    New-Item -ItemType Directory -Force -Path $extract | Out-Null

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($source.FullName, $extract)
    $classesJar = Join-Path $extract "classes.jar"
    if (Test-Path $classesJar) {
        $archives.Add([PSCustomObject]@{ Source = $source.FullName; Jar = $classesJar })
    }
}

if ($archives.Count -eq 0) {
    throw "No RuStore AAR/JAR class archives were found in Gradle cache."
}

$targets = @{
    Client = "ru/rustore/sdk/install/referrer/InstallReferrerClient.class"
    Success = "ru/rustore/sdk/core/tasks/OnSuccessListener.class"
    Failure = "ru/rustore/sdk/core/tasks/OnFailureListener.class"
}

$found = @{}
$resultCandidates = New-Object System.Collections.Generic.List[object]
$listingPath = Join-Path $artifactRoot "rustore-aar-classes.txt"
Remove-Item $listingPath -Force -ErrorAction SilentlyContinue

foreach ($archive in $archives) {
    $entries = & $jarExe tf $archive.Jar
    if ($LASTEXITCODE -ne 0) { throw "jar tf failed for $($archive.Jar)" }

    Add-Content $listingPath ("=== " + $archive.Source + " ===")
    $entries | Where-Object {
        $_ -match 'InstallReferrer|OnSuccessListener|OnFailureListener'
    } | Add-Content $listingPath

    foreach ($name in $targets.Keys) {
        if (-not $found.ContainsKey($name) -and $entries -contains $targets[$name]) {
            $found[$name] = [PSCustomObject]@{
                Jar = $archive.Jar
                Class = ($targets[$name] -replace '/', '.' -replace '\.class$', '')
                Source = $archive.Source
            }
        }
    }

    foreach ($entry in ($entries | Where-Object { $_ -match 'InstallReferrerV2\.class$' })) {
        $resultCandidates.Add([PSCustomObject]@{
            Jar = $archive.Jar
            Class = ($entry -replace '/', '.' -replace '\.class$', '')
            Source = $archive.Source
        })
    }
}

foreach ($name in $targets.Keys) {
    if (-not $found.ContainsKey($name)) {
        throw "Expected RuStore class not found in resolved artifacts: $($targets[$name])"
    }
}
if ($resultCandidates.Count -eq 0) {
    throw "InstallReferrerV2 result class was not found in resolved RuStore artifacts."
}

$clientDump = Join-Path $artifactRoot "javap-install-referrer-client.txt"
$client = $found["Client"]
& $javapExe -classpath $client.Jar -public $client.Class | Tee-Object -FilePath $clientDump
if ($LASTEXITCODE -ne 0) { throw "javap failed for InstallReferrerClient." }
$clientText = Get-Content $clientDump -Raw
if ($clientText -notmatch 'getInstallReferrerV2\s*\(') {
    throw "Resolved InstallReferrerClient does not expose getInstallReferrerV2()."
}

$resultDump = Join-Path $artifactRoot "javap-install-referrer-v2.txt"
$resultVerified = $false
foreach ($candidate in $resultCandidates) {
    $dump = & $javapExe -classpath $candidate.Jar -public $candidate.Class
    if ($LASTEXITCODE -ne 0) { continue }
    if (($dump -join [Environment]::NewLine) -match 'getInstallReferrerV2\s*\(') {
        $dump | Tee-Object -FilePath $resultDump | Out-Host
        $resultVerified = $true
        break
    }
}
if (-not $resultVerified) {
    throw "InstallReferrerV2 class was found, but no getInstallReferrer() getter was exposed."
}

foreach ($name in @("Success", "Failure")) {
    $target = $found[$name]
    $dumpPath = Join-Path $artifactRoot ("javap-" + $name.ToLowerInvariant() + "-listener.txt")
    & $javapExe -classpath $target.Jar -public $target.Class | Tee-Object -FilePath $dumpPath
    if ($LASTEXITCODE -ne 0) { throw "javap failed for $name listener." }
}

$summary = Join-Path $artifactRoot "native-aar-summary.txt"
@(
    "RuStore native AAR contract: PASS",
    "InstallReferrerClient=$($client.Source)",
    "ClientMethod=getInstallReferrerV2",
    "ResultGetter=getInstallReferrer",
    "SuccessListener=$($found['Success'].Class)",
    "FailureListener=$($found['Failure'].Class)"
) | Set-Content $summary

Get-Content $summary
