param(
    [string]$RunnerWorkspace = "C:\actions-runner\_work\rustore_game\rustore_game",
    [string]$TaskName = "GitHub Unity Runner - rustore_game",
    [string]$KeystoreSource,
    [string]$KeyAlias = "nesbeysya",
    [string]$CertificateDName = "CN=NESBEISYA Android Release, OU=Release, O=PanishevQA",
    [int]$ValidityDays = 10000,
    [switch]$CreateIfMissing,
    [switch]$SkipTaskRestart
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "Release signing setup failed: $Message"
}

function Resolve-FullPath([string]$PathValue) {
    $expanded = [Environment]::ExpandEnvironmentVariables($PathValue)
    if ([IO.Path]::IsPathRooted($expanded)) { return [IO.Path]::GetFullPath($expanded) }
    return [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $expanded))
}

function Read-RequiredSecret([string]$Prompt) {
    $secure = Read-Host $Prompt -AsSecureString
    if ($null -eq $secure -or $secure.Length -le 0) { Fail "$Prompt cannot be empty." }
    return $secure
}

function Convert-SecureToPlainText([Security.SecureString]$SecureValue) {
    $ptr = [IntPtr]::Zero
    try {
        $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    }
    finally {
        if ($ptr -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
    }
}

function Resolve-Keytool([string]$Workspace) {
    $command = Get-Command keytool -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $unityExe = $env:UNITY_EXE
    if (-not [string]::IsNullOrWhiteSpace($unityExe)) {
        $expandedUnity = [Environment]::ExpandEnvironmentVariables($unityExe)
        if (Test-Path $expandedUnity -PathType Leaf) {
            $editorDirectory = Split-Path -Parent $expandedUnity
            $candidate = Join-Path $editorDirectory "Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe"
            if (Test-Path $candidate -PathType Leaf) { return (Resolve-Path $candidate).Path }
        }
    }

    $versionFile = Join-Path $Workspace "UnityProject\ProjectSettings\ProjectVersion.txt"
    if (Test-Path $versionFile -PathType Leaf) {
        $line = Get-Content $versionFile | Where-Object { $_ -match "^m_EditorVersion:" } | Select-Object -First 1
        if ($line) {
            $version = ($line -split ":", 2)[1].Trim()
            foreach ($candidate in @(
                "C:\Program Files\Unity\Hub\Editor\$version\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe",
                "C:\Program Files\Unity Hub\Editor\$version\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe"
            )) {
                if (Test-Path $candidate -PathType Leaf) { return (Resolve-Path $candidate).Path }
            }
        }
    }

    Fail "keytool was not found in PATH or in the Android OpenJDK bundled with the pinned Unity Editor."
}

if ($env:OS -ne "Windows_NT") { Fail "this helper is only supported on Windows." }
if ($ValidityDays -lt 3650) { Fail "ValidityDays must be at least 3650 days for a long-lived Android release key." }
if ([string]::IsNullOrWhiteSpace($KeyAlias)) { Fail "KeyAlias cannot be empty." }

$workspace = Resolve-FullPath $RunnerWorkspace
$unityProject = Join-Path $workspace "UnityProject"
if (-not (Test-Path $unityProject -PathType Container)) { Fail "runner UnityProject directory was not found: $unityProject" }

if ([string]::IsNullOrWhiteSpace($KeystoreSource)) {
    if ($CreateIfMissing) {
        $KeystoreSource = Join-Path $env:USERPROFILE "Documents\NesbeisyaReleaseSigning\user.keystore"
    } else {
        $KeystoreSource = Read-Host "Path to the existing production keystore"
    }
}
if ([string]::IsNullOrWhiteSpace($KeystoreSource)) { Fail "production keystore path is required." }

$source = Resolve-FullPath $KeystoreSource
$sourceExists = Test-Path $source -PathType Leaf
if (-not $sourceExists -and -not $CreateIfMissing) {
    Fail "production keystore file was not found: $source. Use -CreateIfMissing only if this is the first release key."
}
if (-not $sourceExists -and $source.StartsWith($workspace, [StringComparison]::OrdinalIgnoreCase)) {
    Fail "new master keystore must be created outside the GitHub runner workspace so checkout/cleanup cannot destroy the only copy."
}

Write-Host ""
Write-Host "Enter signing secrets using secure prompts." -ForegroundColor Cyan
Write-Host "Never paste signing passwords into chat, GitHub variables, repository files, or command-line arguments." -ForegroundColor Yellow
$keystoreSecure = Read-RequiredSecret "Production keystore password"
$keyAliasSecure = Read-RequiredSecret "Production key-alias password"

$keystorePassword = $null
$keyAliasPassword = $null
try {
    $keystorePassword = Convert-SecureToPlainText $keystoreSecure
    $keyAliasPassword = Convert-SecureToPlainText $keyAliasSecure

    $env:NESBEISYA_KEYTOOL_STOREPASS = $keystorePassword
    $env:NESBEISYA_KEYTOOL_KEYPASS = $keyAliasPassword
    $keytool = Resolve-Keytool -Workspace $workspace

    if (-not $sourceExists) {
        $sourceDirectory = Split-Path -Parent $source
        if ([string]::IsNullOrWhiteSpace($sourceDirectory)) { Fail "could not resolve master keystore directory." }
        New-Item -ItemType Directory -Force -Path $sourceDirectory | Out-Null

        Write-Host "Creating new production release keystore outside the repository..." -ForegroundColor Cyan
        $createArgs = @(
            "-genkeypair", "-alias", $KeyAlias, "-keyalg", "RSA", "-keysize", "4096",
            "-validity", $ValidityDays.ToString(), "-storetype", "JKS", "-keystore", $source,
            "-storepass:env", "NESBEISYA_KEYTOOL_STOREPASS",
            "-keypass:env", "NESBEISYA_KEYTOOL_KEYPASS", "-dname", $CertificateDName
        )
        & $keytool @createArgs
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $source -PathType Leaf)) {
            Fail "keytool did not create the production keystore successfully."
        }
        $sourceExists = $true
    }

    $listArgs = @(
        "-list", "-keystore", $source, "-alias", $KeyAlias,
        "-storepass:env", "NESBEISYA_KEYTOOL_STOREPASS"
    )
    & $keytool @listArgs | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Fail "keystore password is invalid or alias '$KeyAlias' is not present in the selected keystore."
    }

    $destination = Join-Path $unityProject "user.keystore"
    if (-not [string]::Equals($source, $destination, [StringComparison]::OrdinalIgnoreCase)) {
        Copy-Item -Path $source -Destination $destination -Force
    }
    if (-not (Test-Path $destination -PathType Leaf)) {
        Fail "keystore copy did not produce the expected runner file: $destination"
    }

    [Environment]::SetEnvironmentVariable("NESBEISYA_KEYSTORE_PASS", $keystorePassword, "User")
    [Environment]::SetEnvironmentVariable("NESBEISYA_KEYALIAS_PASS", $keyAliasPassword, "User")
}
finally {
    Remove-Item Env:NESBEISYA_KEYTOOL_STOREPASS -ErrorAction SilentlyContinue
    Remove-Item Env:NESBEISYA_KEYTOOL_KEYPASS -ErrorAction SilentlyContinue
    $keystorePassword = $null
    $keyAliasPassword = $null
    $keystoreSecure = $null
    $keyAliasSecure = $null
}

if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable("NESBEISYA_KEYSTORE_PASS", "User")) -or
    [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable("NESBEISYA_KEYALIAS_PASS", "User"))) {
    Fail "user-scoped signing environment variables were not persisted."
}

$destination = Join-Path $unityProject "user.keystore"
Write-Host ""
Write-Host "Release signing machine configuration saved." -ForegroundColor Green
Write-Host "Master keystore: $source"
Write-Host "Runner keystore copy: $destination"
Write-Host "Key alias: $KeyAlias"
Write-Host "Environment variables: NESBEISYA_KEYSTORE_PASS, NESBEISYA_KEYALIAS_PASS (values hidden)"
Write-Host ""
Write-Host "IMPORTANT: keep at least two offline backups of the master keystore and its passwords." -ForegroundColor Yellow
Write-Host "Losing the production signing key can prevent publishing compatible updates." -ForegroundColor Yellow

if (-not $SkipTaskRestart) {
    $task = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if (-not $task) {
        Fail "scheduled runner task '$TaskName' was not found. Run install_unity_runner_logon_task.ps1 first or use -SkipTaskRestart and restart the runner manually."
    }
    Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    Start-ScheduledTask -TaskName $TaskName
    Write-Host "Runner task restarted: $TaskName" -ForegroundColor Green
} else {
    Write-Host "Runner task restart skipped. Restart it manually before Build mode so Unity inherits the user-scoped variables." -ForegroundColor Yellow
}
