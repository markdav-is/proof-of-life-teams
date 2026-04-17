#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Watches Windows Security event log for logon events and reports presence
    to the Proof-of-Life API.

.DESCRIPTION
    Listens for Windows Event ID 4624 (Successful Logon) and 4648 (Explicit
    Logon). On detection, looks up the user's UPN from Active Directory and
    POSTs a presence event to the PoL API.

    Designed to run as a Windows Service via NSSM or Task Scheduler.

    Detects:
      - Interactive logons (type 2, 10, 11)
      - Network / mapped-drive logons (type 3) — optional
      - Remote desktop logons (type 10)

.PARAMETER ApiBaseUrl
    Base URL of the Proof-of-Life API, e.g. https://pol-api.yourdomain.com

.PARAMETER ApiKey
    API key (X-Api-Key header value)

.PARAMETER Department
    Fallback department name if AD lookup fails

.PARAMETER IncludeNetworkLogons
    Also report type-3 (network) logons (can be noisy). Default: $false

.EXAMPLE
    .\LoginEventDetector.ps1 -ApiBaseUrl https://pol-api.yourdomain.com -ApiKey MyKey

.NOTES
    Requires: PowerShell 5.1+, ActiveDirectory module or ADSI fallback.
    Run as SYSTEM or an account with "Manage auditing and security log" rights.
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [string]$ApiBaseUrl,

    [Parameter(Mandatory)]
    [string]$ApiKey,

    [string]$Department = "Unknown",

    [switch]$IncludeNetworkLogons
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

$logonTypes = @(2, 10, 11)   # Interactive, RemoteInteractive, CachedInteractive
if ($IncludeNetworkLogons) { $logonTypes += 3 }

$reportedToday = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$lastReset = [DateTime]::UtcNow.Date

function Reset-DailyIfNeeded {
    $today = [DateTime]::UtcNow.Date
    if ($today -gt $script:lastReset) {
        $script:reportedToday.Clear()
        $script:lastReset = $today
        Write-Host "$(Get-Date -Format 'u') [INFO] New day — presence cache reset"
    }
}

function Get-UserInfo([string]$samAccount, [string]$domain) {
    # Try ActiveDirectory module first, fall back to ADSI
    try {
        if (Get-Module -ListAvailable -Name ActiveDirectory) {
            Import-Module ActiveDirectory -ErrorAction SilentlyContinue
            $adUser = Get-ADUser -Identity $samAccount `
                -Properties UserPrincipalName, DisplayName, Department, Title, Manager `
                -ErrorAction Stop
            return @{
                UserId      = $adUser.UserPrincipalName
                DisplayName = $adUser.Name
                Email       = $adUser.UserPrincipalName
                Department  = if ($adUser.Department) { $adUser.Department } else { $script:Department }
                JobTitle    = if ($adUser.Title) { $adUser.Title } else { "" }
            }
        }
    } catch { }

    # ADSI fallback (no AD module required)
    try {
        $searcher = [adsisearcher]"(&(objectClass=user)(sAMAccountName=$samAccount))"
        $result = $searcher.FindOne()
        if ($result) {
            $upn = $result.Properties["userprincipalname"][0]
            return @{
                UserId      = $upn
                DisplayName = $result.Properties["displayname"][0]
                Email       = $upn
                Department  = if ($result.Properties["department"].Count -gt 0) { $result.Properties["department"][0] } else { $script:Department }
                JobTitle    = if ($result.Properties["title"].Count -gt 0) { $result.Properties["title"][0] } else { "" }
            }
        }
    } catch { }

    # Last resort: synthesise UPN from SAM + domain
    return @{
        UserId      = "$samAccount@$domain"
        DisplayName = $samAccount
        Email       = "$samAccount@$domain"
        Department  = $script:Department
        JobTitle    = ""
    }
}

function Send-PresenceEvent([hashtable]$user) {
    $body = @{
        userId      = $user.UserId
        displayName = $user.DisplayName
        email       = $user.Email
        department  = $user.Department
        jobTitle    = $user.JobTitle
        managerId   = $null
        source      = "WindowsLogin"
    } | ConvertTo-Json

    $headers = @{
        "X-Api-Key"    = $ApiKey
        "Content-Type" = "application/json"
    }

    try {
        $response = Invoke-RestMethod `
            -Uri "$ApiBaseUrl/api/presence" `
            -Method POST `
            -Headers $headers `
            -Body $body `
            -ErrorAction Stop
        Write-Host "$(Get-Date -Format 'u') [OK]   Reported: $($user.DisplayName) ($($user.Department))"
    } catch {
        Write-Warning "$(Get-Date -Format 'u') [WARN] Failed to report $($user.UserId): $_"
    }
}

function Handle-LogonEvent([System.Diagnostics.Eventing.Reader.EventLogRecord]$event) {
    Reset-DailyIfNeeded

    $logonType  = $event.Properties[8].Value   # 8 = LogonType in 4624
    $samAccount = $event.Properties[5].Value   # 5 = TargetUserName
    $domain     = $event.Properties[6].Value   # 6 = TargetDomainName

    # Skip machine accounts and SYSTEM
    if ($samAccount -match '\$$' -or $samAccount -in @("SYSTEM", "LOCAL SERVICE", "NETWORK SERVICE")) {
        return
    }
    if ($logonType -notin $script:logonTypes) { return }

    # De-duplicate: only report once per user per day
    if (-not $script:reportedToday.Add($samAccount)) { return }

    Write-Host "$(Get-Date -Format 'u') [INFO] Logon detected: $samAccount (type $logonType)"
    $userInfo = Get-UserInfo -samAccount $samAccount -domain $domain
    Send-PresenceEvent -user $userInfo
}

# ── Event subscription ───────────────────────────────────────────────────────
Write-Host "$(Get-Date -Format 'u') [INFO] Starting Windows login detector"
Write-Host "$(Get-Date -Format 'u') [INFO] API: $ApiBaseUrl | Logon types: $($logonTypes -join ', ')"

$query = @"
<QueryList>
  <Query Id="0" Path="Security">
    <Select Path="Security">
      *[System[EventID=4624] and EventData[Data[@Name='LogonType'] and ($( ($logonTypes | ForEach-Object { "Data='{0}'" -f $_ }) -join ' or ' ))]]
    </Select>
  </Query>
</QueryList>
"@

$watcher = New-Object System.Diagnostics.Eventing.Reader.EventLogWatcher(
    (New-Object System.Diagnostics.Eventing.Reader.EventLogQuery("Security",
        [System.Diagnostics.Eventing.Reader.PathType]::LogName, $query))
)

$watcher.add_EventRecordWritten({
    param($sender, $e)
    if ($e.EventRecord) {
        Handle-LogonEvent -event $e.EventRecord
    }
})

$watcher.Enabled = $true
Write-Host "$(Get-Date -Format 'u') [INFO] Watching for logon events. Press Ctrl+C to stop."

try {
    while ($true) { Start-Sleep -Seconds 10 }
} finally {
    $watcher.Enabled = $false
    $watcher.Dispose()
    Write-Host "$(Get-Date -Format 'u') [INFO] Watcher stopped."
}
