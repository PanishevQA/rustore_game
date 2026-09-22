param(
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $repoRoot "artifacts\rustore-release-artifact-probe"
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$packages = @(
    @{
        Label = "Install Referrer"
        Id = "ru.rustore.installreferrer"
        Version = "10.6.1"
        TarballUrl = "https://gitflic.ru/project/rustore/unity-rustore-install-referrer-sdk/release/2f17b04e-716e-4a9e-bba4-cfda7dc0b6aa/7cdfd7da-9326-4a9e-856d-a2d041b2b170/download"
        ChecksumUrl = "https://gitflic.ru/project/rustore/unity-rustore-install-referrer-sdk/release/2f17b04e-716e-4a9e-bba4-cfda7dc0b6aa/719b37a2-7cb7-437a-8ee2-d246588c6da8/download"
    },
    @{
        Label = "Remote Config"
        Id = "ru.rustore.remoteconfig"
        Version = "10.5.1"
        TarballUrl = "https://gitflic.ru/project/rustore/unity-rustore-remote-config-sdk/release/e5557a31-215f-4ff5-92ec-3ded2d797346/ed94525e-816a-47fd-ba23-391eba907d6b/download"
        ChecksumUrl = "https://gitflic.ru/project/rustore/unity-rustore-remote-config-sdk/release/e5557a31-215f-4ff5-92ec-3ded2d797346/466b5740-38be-4a36-b23e-ea1629c85d44/download"
    }
)

function Get-ExpectedSha256([string]$ChecksumPath) {
    $text = Get-Content $ChecksumPath -Raw
    $match = [Regex]::Match($text, '(?i)\b[0-9a-f]{64}\b')
    if (-not $match.Success) {
        throw "Could not parse SHA-256 from $ChecksumPath"
    }
    return $match.Value.ToLowerInvariant()
}

function Get-MetaGuidMap([string]$Root) {
    $map = @{}
    Get-ChildItem $Root -Recurse -File -Filter *.meta | ForEach-Object {
        $text = Get-Content $_.FullName -Raw
        $match = [Regex]::Match($text, '(?im)^guid:\s*([0-9a-f]{32})\s*$')
        if ($match.Success) {
            $guid = $match.Groups[1].Value.ToLowerInvariant()
            if (-not $map.ContainsKey($guid)) { $map[$guid] = @() }
            $map[$guid] += $_.FullName.Substring($Root.Length).TrimStart('\')
        }
    }
    return $map
}

$results = @()
foreach ($package in $packages) {
    $safe = $package.Id.Replace(".", "-")
    $tgz = Join-Path $OutDir ($safe + "-" + $package.Version + ".tgz")
    $shaFile = Join-Path $OutDir ($safe + "-" + $package.Version + ".tgz.sha256")
    $extract = Join-Path $OutDir ($safe + "-extract")

    Remove-Item $tgz, $shaFile -Force -ErrorAction SilentlyContinue
    Remove-Item $extract -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $extract | Out-Null

    Write-Host "Downloading $($package.Label) $($package.Version)..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $package.TarballUrl -OutFile $tgz
    Invoke-WebRequest -Uri $package.ChecksumUrl -OutFile $shaFile

    $expected = Get-ExpectedSha256 $shaFile
    $actual = (Get-FileHash $tgz -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected) {
        throw "$($package.Label) SHA-256 mismatch. Expected $expected, got $actual."
    }

    & tar.exe -xzf $tgz -C $extract
    if ($LASTEXITCODE -ne 0) {
        throw "Could not extract $tgz"
    }

    $packageJson = Get-ChildItem $extract -Recurse -File -Filter package.json | Select-Object -First 1
    if (-not $packageJson) {
        throw "$($package.Label) package.json not found after extraction."
    }
    $json = Get-Content $packageJson.FullName -Raw | ConvertFrom-Json
    if ($json.name -ne $package.Id -or $json.version -ne $package.Version) {
        throw "$($package.Label) identity mismatch: name=$($json.name), version=$($json.version)"
    }

    $results += [PSCustomObject]@{
        Label = $package.Label
        Id = $package.Id
        Version = $package.Version
        Sha256 = $actual
        ExtractRoot = $extract
        Meta = Get-MetaGuidMap $extract
    }

    Write-Host "$($package.Label): verified $actual" -ForegroundColor Green
}

$left = $results[0]
$right = $results[1]
$collisions = New-Object System.Collections.Generic.List[object]
foreach ($guid in $left.Meta.Keys) {
    if (-not $right.Meta.ContainsKey($guid)) { continue }
    $collisions.Add([PSCustomObject]@{
        Guid = $guid
        InstallReferrerPaths = ($left.Meta[$guid] -join "; ")
        RemoteConfigPaths = ($right.Meta[$guid] -join "; ")
    })
}

$summaryPath = Join-Path $OutDir "tarball-guid-summary.txt"
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("RuStore official release tarball inspection")
$lines.Add("Generated UTC: " + [DateTime]::UtcNow.ToString("O"))
foreach ($r in $results) {
    $lines.Add("$($r.Id) $($r.Version) SHA256=$($r.Sha256)")
}
$lines.Add("Cross-package duplicate GUID count: " + $collisions.Count)
foreach ($c in $collisions) {
    $lines.Add("GUID $($c.Guid)")
    $lines.Add("  InstallReferrer: $($c.InstallReferrerPaths)")
    $lines.Add("  RemoteConfig:    $($c.RemoteConfigPaths)")
}
[IO.File]::WriteAllLines($summaryPath, $lines)
Get-Content $summaryPath

if ($collisions.Count -gt 0) {
    Write-Host "Official release tarballs contain cross-package duplicate GUIDs." -ForegroundColor Red
    exit 2
}

Write-Host "Official release tarballs have no cross-package duplicate GUIDs." -ForegroundColor Green
