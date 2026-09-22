param(
    [string]$RunnerWorkspace = "C:\actions-runner\_work\rustore_game\rustore_game",
    [string]$TaskName = "GitHub Unity Runner - rustore_game",
    [string]$KeystoreSource,
    [switch]$SkipTaskRestart
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "Release signing setup failed: $Message"
}

function Resolve-FullPath([string]$PathValue) {
    $expanded = [Environment]::ExpandEnvironmentVariables($PathValue)
    if ([IO.Path]::IsPathRooted($expanded)) {
        return [IO.Path]::GetFullPath($expanded)
    }
    return [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $expanded))
}

function Read-RequiredSecret([string]$Prompt) {
    $secure = Read-Host $Prompt -AsSecureString
    if ($null -eq $secure -or $secure.Length -le 0) {
        Fail "$Prompt cannot be empty."
    }
    return $secure
}

function Convert-SecureToPlainText([Security.SecureString]$SecureValue) {
    $ptr = [IntPtr]::Zero
    try {
        $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    }
    finally {
        if ($ptr -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
        }
    }
}

if ($env:OS -ne "Windows_NT") {
    Fail "this helper is only supported on Windows."
}

if ([string]::IsNullOrWhiteSpace($KeystoreSource)) {
    $KeystoreSource = Read-Host "Path to the existing production keystore"
}
if ([string]::IsNullOrWhiteSpace($KeystoreSource)) {
    Fail "production keystore path is required."
}

$source = Resolve-FullPath $KeystoreSource
if (-not (Test-Path $source -PathType Leaf)) {
    Fail "production keystore file was not found: $source"
}

$workspace = Resolve-FullPath $RunnerWorkspace
$unityProject = Join-Path $workspace "UnityProject"
if (-not (Test-Path $unityProject -PathType Container)) {
    Fail "runner UnityProject directory was not found: $unityProject"
}

$destination = Join-Path $unityProject "user.keystore"
if (-not [string]::Equals($source, $destination, [StringComparison]::OrdinalIgnoreCase)) {
    Copy-Item -Path $source -Destination $destination -Force
}
if (-not (Test-Path $destination -PathType Leaf)) {
    Fail "keystore copy did not produce the expected runner file: $destination"
}

Write-Host ""
Write-Host "Enter signing secrets using secure prompts." -ForegroundColor Cyan
Write-Host "Never paste signing passwords into chat, GitHub variables, repository files, or command-line arguments." -ForegroundColor Yellow
$keystoreSecure = Read-Host "Production keystore password" -AsSecureString
$keyAliasSecure = Read-Host "Production key-alias password" -AsSecureString
if ($null -eq $keystoreSecure -or $keystoreSecure.Length -le 0) {
    Fail "production keystore password cannot be empty."
}
if ($null -eq $keyAliasSecure -or $keyAliasSecure.Length -le 0) {
    Fail "production key-alias password cannot be empty."
}

$keystorePassword = $null
$keyAliasPassword = $null
try {
    $keystorePassword = Convert-SecureToPlainText $keystoreSecure
    $keyAliasPassword = Convert-SecureToPlainText $keyAliasSecure

    [Environment]::SetEnvironmentVariable("NESBEISYA_KEYSTORE_PASS", $keystorePassword, "User")
    [Environment]::SetEnvironmentVariable("NESBEISYA_KEYALIAS_PASS", $keyAliasPassword, "User")
}
finally {
    $keystorePassword = $null
    $keyAliasPassword = $null
    $keystoreSecure = $null
    $keyAliasSecure = $null
}

if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable("NESBEISYA_KEYSTORE_PASS", "User")) -or
    [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable("NESBEISYA_KEYALIAS_PASS", "User"))) {
    Fail "user-scoped signing environment variables were not persisted."
}

Write-Host ""
Write-Host "Release signing machine configuration saved." -ForegroundColor Green
Write-Host "Keystore destination: $destination"
Write-Host "Environment variables: NESBEISYA_KEYSTORE_PASS, NESBEISYA_KEYALIAS_PASS (values hidden)"

if (-not $SkipTaskRestart) {
    $task = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if (-not $task) {
        Fail "scheduled runner task '$TaskName' was not found. Run install_unity_runner_logon_task.ps1 first or use -SkipTaskRestart and restart the runner manually."
    }

    Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    Start-ScheduledTask -TaskName $TaskName
    Write-Host "Runner task restarted: $TaskName" -ForegroundColor Green
}
else {
    Write-Host "Runner task restart skipped. Restart it manually before Build mode so Unity inherits the user-scoped variables." -ForegroundColor Yellow
}
