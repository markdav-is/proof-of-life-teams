#Requires -Version 5.1
<#
.SYNOPSIS
    Monitors for Microsoft Office application launches and reports presence to the PoL API.

.DESCRIPTION
    Uses WMI __InstanceCreationEvent to watch for new Win32_Process instances
    matching Office apps (Word, Excel, Outlook, Teams, etc.).

    This provides a "soft" signal that someone is actively working, even if they
    haven't generated a Teams presence event yet (e.g. they opened Word offline).

    Designed to run as a per-user startup script (not requiring admin) via:
      - User's Startup folder
      - Group Policy User Configuration > Scripts > Logon

.PARAMETER ApiBaseUrl
    Base URL of the Proof-of-Life API.

.PARAMETER ApiKey
    API key for the Proof-of-Life API.

.EXAMPLE
    .\OfficeActivityHook.ps1 -ApiBaseUrl https://pol-api.yourdomain.com -ApiKey MyKey
#>
param(
    [Parameter(Mandatory)]
    [string]$ApiBaseUrl,

    [Parameter(Mandatory)]
    [string]$ApiKey
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

# Office processes that indicate active M365 usage
$officeProcesses = @(
    "WINWORD", "EXCEL", "POWERPNT", "OUTLOOK", "ONENOTE",
    "MSACCESS", "MSPUB", "TEAMS", "Teams",
    "olk",          # new Outlook
    "LYNC",         # Skype for Business
    "GrooveMonitor" # OneDrive
)

$reportedToday = $false
$lastReportDate = [DateTime]::UtcNow.Date

function Get-CurrentUserInfo {
    $upn = [System.DirectoryServices.AccountManagement.UserPrincipal]::Current
    if ($upn) {
        return @{
            UserId      = $upn.UserPrincipalName ?? "$env:USERNAME@$env:USERDNSDOMAIN"
            DisplayName = $upn.DisplayName ?? $env:USERNAME
            Email       = $upn.EmailAddress ?? "$env:USERNAME@$env:USERDNSDOMAIN"
            Department  = "Unknown"
            JobTitle    = ""
        }
    }
    return @{
        UserId      = "$env:USERNAME@$env:USERDNSDOMAIN"
        DisplayName = $env:USERNAME
        Email       = "$env:USERNAME@$env:USERDNSDOMAIN"
        Department  = "Unknown"
        JobTitle    = ""
    }
}

function Send-PresenceEvent([hashtable]$user, [string]$processName) {
    $body = @{
        userId      = $user.UserId
        displayName = $user.DisplayName
        email       = $user.Email
        department  = $user.Department
        jobTitle    = $user.JobTitle
        managerId   = $null
        source      = "OfficeActivity"
    } | ConvertTo-Json

    try {
        Invoke-RestMethod `
            -Uri "$ApiBaseUrl/api/presence" `
            -Method POST `
            -Headers @{ "X-Api-Key" = $ApiKey; "Content-Type" = "application/json" } `
            -Body $body `
            -ErrorAction Stop | Out-Null

        Write-Host "$(Get-Date -Format 'u') [OK] Presence reported via $processName for $($user.DisplayName)"
    } catch {
        Write-Warning "$(Get-Date -Format 'u') [WARN] Failed to report presence: $_"
    }
}

# WMI query — fires when any process matching the pattern starts
$processFilter = ($officeProcesses | ForEach-Object { "TargetInstance.Name LIKE '$_.exe'" }) -join " OR "
$wmiQuery = "SELECT * FROM __InstanceCreationEvent WITHIN 5 WHERE TargetInstance ISA 'Win32_Process' AND ($processFilter)"

Write-Host "$(Get-Date -Format 'u') [INFO] Watching for Office activity..."

Register-WmiEvent -Query $wmiQuery -SourceIdentifier "OfficeProcessStart" -Action {
    $today = [DateTime]::UtcNow.Date
    if ($today -gt $script:lastReportDate) {
        $script:reportedToday = $false
        $script:lastReportDate = $today
    }

    if ($script:reportedToday) { return }

    $processName = $Event.SourceEventArgs.NewEvent.TargetInstance.Name
    Write-Host "$(Get-Date -Format 'u') [INFO] Office process detected: $processName"

    $userInfo = Get-CurrentUserInfo
    Send-PresenceEvent -user $userInfo -processName $processName
    $script:reportedToday = $true
} | Out-Null

try {
    while ($true) { Start-Sleep -Seconds 30 }
} finally {
    Unregister-Event -SourceIdentifier "OfficeProcessStart" -ErrorAction SilentlyContinue
    Write-Host "$(Get-Date -Format 'u') [INFO] Office activity monitor stopped."
}
