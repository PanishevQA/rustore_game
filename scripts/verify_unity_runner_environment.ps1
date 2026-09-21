param(
    [string]$UnityExe = $env:UNITY_EXE
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectRoot = Join-Path $repoRoot "UnityProject"
$versionPath = Join-Path $projectRoot "ProjectSettings\ProjectVersion.txt"

function Fail([string]$Message) {
    throw "Unity self-hosted runner preflight failed: $Message"
}

function Resolve-RequiredCommand([string]$Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $command) {
        Fail "$Name was not found in PATH."
    }
    return $command
}

function Resolve-Unity {
    param([string]$Explicit, [string]$Version)

    if (-not [string]::IsNullOrWhiteSpace($Explicit)) {
        $candidate = [Environment]::ExpandEnvironmentVariables($Explicit.Trim())
        if (-not (Test-Path $candidate)) {
            Fail "UNITY_EXE points to a missing file: $candidate"
        }
        return (Resolve-Path $candidate).Path
    }

    $candidates = @(
        "C:\Program Files\Unity\Hub\Editor\$Version\Editor\Unity.exe",
        "C:\Program Files\Unity Hub\Editor\$Version\Editor\Unity.exe",
        "$env:ProgramFiles\Unity\Hub\Editor\$Version\Editor\Unity.exe"
    ) | Select-Object -Unique

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path $candidate)) {
            return (Resolve-Path $candidate).Path
        }
    }

    Fail "Unity $Version was not found in standard Unity Hub locations. Set repository variable UNITY_EXE to the full Unity.exe path."
}

if ($env:OS -ne "Windows_NT") {
    Fail "the Unity runner must use Windows."
}

if (-not (Test-Path $versionPath)) {
    Fail "Unity project version file is missing: $versionPath"
}

$versionLine = Get-Content $versionPath | Where-Object { $_ -match "^m_EditorVersion:" } | Select-Object -First 1
if (-not $versionLine) {
    Fail "could not read m_EditorVersion from $versionPath"
}
$editorVersion = ($versionLine -split ":", 2)[1].Trim()
if ($editorVersion -ne "6000.3.24f1") {
    Fail "repository expects Unity 6000.3.24f1 but ProjectVersion.txt contains $editorVersion"
}

$git = Resolve-RequiredCommand "git"
$python = Get-Command python -ErrorAction SilentlyContinue
$py = Get-Command py -ErrorAction SilentlyContinue
if (-not $python -and -not $py) {
    Fail "Python 3 was not found as python or py."
}

$unity = Resolve-Unity -Explicit $UnityExe -Version $editorVersion

$drive = Get-PSDrive -Name ([IO.Path]::GetPathRoot($repoRoot).TrimEnd("\").TrimEnd(":")) -ErrorAction SilentlyContinue
if ($drive -and $drive.Free -lt 10GB) {
    Fail ("at least 10 GB free disk space is required for a reliable Unity batchmode workspace; free={0:N1} GB" -f ($drive.Free / 1GB))
}

if (-not (Test-Path (Join-Path $repoRoot "scripts\agent_check.ps1"))) {
    Fail "scripts\agent_check.ps1 is missing."
}

Write-Host "Unity self-hosted runner environment is ready." -ForegroundColor Green
Write-Host "Repository: $repoRoot"
Write-Host "Unity: $unity"
Write-Host "Unity version: $editorVersion"
if ($python) {
    Write-Host "Python: $($python.Source)"
} else {
    Write-Host "Python launcher: $($py.Source)"
}
Write-Host "Git: $($git.Source)"
Write-Host ""
Write-Host "Important: keep the GitHub Actions runner in its own directory, separate from your normal Unity working copy." -ForegroundColor Yellow
