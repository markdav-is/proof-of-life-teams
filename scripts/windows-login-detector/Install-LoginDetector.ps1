#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs the LoginEventDetector as a Windows Scheduled Task that runs at startup.

.DESCRIPTION
    Creates a Task Scheduler task that starts the PowerShell watcher under SYSTEM account
    at machine startup. Requires NSSM or Task Scheduler.

    The task restarts automatically if it crashes.

.PARAMETER ApiBaseUrl
    Base URL of the Proof-of-Life API.

.PARAMETER ApiKey
    API key for the Proof-of-Life API.

.EXAMPLE
    .\Install-LoginDetector.ps1 -ApiBaseUrl https://pol-api.yourdomain.com -ApiKey MyKey
#>
param(
    [Parameter(Mandatory)]
    [string]$ApiBaseUrl,

    [Parameter(Mandatory)]
    [string]$ApiKey
)

$taskName = "ProofOfLife-LoginDetector"
$scriptPath = Join-Path $PSScriptRoot "LoginEventDetector.ps1"

# Store API key in Windows Credential Manager (safer than plaintext in task XML)
$cred = [System.Management.Automation.PSCredential]::new(
    "PoLApiKey",
    (ConvertTo-SecureString $ApiKey -AsPlainText -Force))
# Write to SYSTEM-accessible registry location
$regPath = "HKLM:\SOFTWARE\ProofOfLife"
if (-not (Test-Path $regPath)) { New-Item -Path $regPath -Force | Out-Null }
Set-ItemProperty -Path $regPath -Name "ApiBaseUrl" -Value $ApiBaseUrl
Set-ItemProperty -Path $regPath -Name "ApiKey" -Value $ApiKey  # Encrypt with DPAPI in production

$action = New-ScheduledTaskAction `
    -Execute "powershell.exe" `
    -Argument "-NonInteractive -NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`" -ApiBaseUrl `"$ApiBaseUrl`" -ApiKey `"$ApiKey`""

$trigger = New-ScheduledTaskTrigger -AtStartup

$settings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit (New-TimeSpan -Hours 0) `
    -RestartCount 5 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -MultipleInstances IgnoreNew

$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest

# Remove existing task if present
Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue

Register-ScheduledTask `
    -TaskName $taskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Principal $principal `
    -Description "Reports Windows interactive logons to the Proof-of-Life presence API"

Write-Host "Scheduled task '$taskName' registered. Starting now..."
Start-ScheduledTask -TaskName $taskName
Write-Host "Done."
