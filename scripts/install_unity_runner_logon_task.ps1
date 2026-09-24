param(
    [string]$RunnerDirectory = "C:\actions-runner",
    [string]$TaskName = "GitHub Unity Runner - rustore_game"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "Unity runner logon-task setup failed: $Message"
}

if ($env:OS -ne "Windows_NT") {
    Fail "this helper is only supported on Windows."
}

$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$userName = $identity.Name
$forbidden = @(
    "NT AUTHORITY\SYSTEM",
    "NT AUTHORITY\NETWORK SERVICE",
    "NT AUTHORITY\LOCAL SERVICE"
)

if ($forbidden -contains $userName.ToUpperInvariant()) {
    Fail "run this script from the normal Windows account that owns the active Unity Personal license."
}

$runnerDirectory = [Environment]::ExpandEnvironmentVariables($RunnerDirectory)
$runnerDirectory = [IO.Path]::GetFullPath($runnerDirectory)
$runCmd = Join-Path $runnerDirectory "run.cmd"
$watchdogPath = Join-Path $runnerDirectory "run-unity-runner-forever.ps1"
$watchdogLog = Join-Path $runnerDirectory "_diag\unity-runner-watchdog.log"
$serviceFile = Join-Path $runnerDirectory ".service"

if (-not (Test-Path $runCmd)) {
    Fail "GitHub runner run.cmd was not found: $runCmd"
}
if (-not (Test-Path $serviceFile)) {
    Fail "GitHub runner .service marker was not found: $serviceFile"
}

$serviceName = (Get-Content $serviceFile -Raw).Trim()
if ([string]::IsNullOrWhiteSpace($serviceName)) {
    Fail "runner .service file is empty."
}

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Stopping built-in runner service: $serviceName" -ForegroundColor Yellow
    if ($service.Status -ne "Stopped") {
        Stop-Service -Name $serviceName -Force
    }
    Set-Service -Name $serviceName -StartupType Disabled
}

$escapedRunnerDirectory = $runnerDirectory.Replace("'", "''")
$escapedWatchdogLog = $watchdogLog.Replace("'", "''")
$watchdog = @"
`$ErrorActionPreference = "Continue"
`$runnerDirectory = '$escapedRunnerDirectory'
`$runCmd = Join-Path `$runnerDirectory "run.cmd"
`$watchdogLog = '$escapedWatchdogLog'

function Write-WatchdogLog([string]`$Message) {
    try {
        `$directory = Split-Path -Parent `$watchdogLog
        if (-not (Test-Path `$directory)) {
            New-Item -ItemType Directory -Force -Path `$directory | Out-Null
        }
        Add-Content -Path `$watchdogLog -Value ("{0} {1}" -f [DateTime]::UtcNow.ToString("O"), `$Message)
    }
    catch {}
}

Write-WatchdogLog "Unity runner watchdog started."
while (`$true) {
    if (-not (Test-Path `$runCmd)) {
        Write-WatchdogLog "run.cmd is missing; retrying in 30 seconds."
        Start-Sleep -Seconds 30
        continue
    }

    try {
        Set-Location `$runnerDirectory
        Write-WatchdogLog "Starting GitHub Actions runner."
        & `$runCmd
        `$exitCode = `$LASTEXITCODE
        Write-WatchdogLog ("Runner exited with code {0}; restarting in 10 seconds." -f `$exitCode)
    }
    catch {
        Write-WatchdogLog ("Runner crashed: {0}; restarting in 10 seconds." -f `$_.Exception.Message)
    }

    Start-Sleep -Seconds 10
}
"@
Set-Content -Path $watchdogPath -Value $watchdog -Encoding UTF8

$existingTask = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if ($existingTask) {
    Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
}

$action = New-ScheduledTaskAction -Execute "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$watchdogPath`""
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $userName
$principal = New-ScheduledTaskPrincipal -UserId $userName -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -MultipleInstances IgnoreNew -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit ([TimeSpan]::Zero)

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description "Keeps the GitHub Actions Unity runner alive under the interactive Unity-licensed Windows user." -Force | Out-Null

Write-Host "Starting scheduled runner task now..." -ForegroundColor Cyan
Start-ScheduledTask -TaskName $TaskName

Start-Sleep -Seconds 3
$task = Get-ScheduledTask -TaskName $TaskName
$info = Get-ScheduledTaskInfo -TaskName $TaskName

Write-Host ""
Write-Host "Unity runner logon task configured." -ForegroundColor Green
Write-Host "Windows user: $userName"
Write-Host "Task: $TaskName"
Write-Host "Task state: $($task.State)"
Write-Host "Last task result: $($info.LastTaskResult)"
Write-Host "Legacy service startup: Disabled"
Write-Host ""
Write-Host "Watchdog: $watchdogPath"
Write-Host "Watchdog log: $watchdogLog"
Write-Host "The runner will now start automatically after this Windows user logs in and restart automatically if run.cmd exits." -ForegroundColor Green
Write-Host "Do not start the disabled GitHub runner service unless it is reconfigured to use the Unity-licensed user."
