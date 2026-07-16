<#
.SYNOPSIS
    YLproxy Windows Service Uninstaller
.DESCRIPTION
    Wrapper script that calls install-service.ps1 -Uninstall and performs
    additional verification steps to ensure clean removal.

.EXAMPLE
    .\scripts\uninstall-service.ps1
    Uninstalls the YLproxy service and firewall rules.
    Run this from a PowerShell window with Administrator privileges.

.NOTES
    Requires Administrator privileges.
    User data files (config.json, ylproxy.db) are NOT deleted.
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$InstallScript = Join-Path $ScriptDir "install-service.ps1"
$ProjectRoot = Split-Path -Parent $ScriptDir

$ServiceName = "YLproxyService"

# Check for admin
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "[ERROR] Administrator privileges required. Run PowerShell as Administrator." -ForegroundColor Red
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  YLproxy Service Uninstaller" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Run the main uninstall
Write-Host "[STEP 1/3] Running uninstall via install-service.ps1..." -ForegroundColor Yellow
& $InstallScript -Action Uninstall
if ($LASTEXITCODE -ne 0) {
    Write-Host "[WARN] Uninstall script reported issues. Continuing verification..." -ForegroundColor Yellow
}

Write-Host ""

# Verify service removal
Write-Host "[STEP 2/3] Verifying service removal..." -ForegroundColor Yellow
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -eq $svc) {
    Write-Host "[OK]   Service '$ServiceName' successfully removed." -ForegroundColor Green
} else {
    Write-Host "[WARN] Service '$ServiceName' still present. Status: $($svc.Status)" -ForegroundColor Yellow
    Write-Host "       You may need to manually run: sc.exe delete $ServiceName" -ForegroundColor Yellow
}

# Verify firewall rule removal
Write-Host "[STEP 3/3] Verifying firewall rule removal..." -ForegroundColor Yellow
$fwRule = Get-NetFirewallRule -DisplayName "YLproxy Service Inbound" -ErrorAction SilentlyContinue
if ($null -eq $fwRule) {
    Write-Host "[OK]   Firewall rule 'YLproxy Service Inbound' removed." -ForegroundColor Green
} else {
    Write-Host "[WARN] Firewall rule still exists." -ForegroundColor Yellow
    Write-Host "       You can manually remove it with:" -ForegroundColor Yellow
    Write-Host "       Remove-NetFirewallRule -DisplayName 'YLproxy Service Inbound'" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Uninstallation Verification Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "[INFO] The following data files were preserved:" -ForegroundColor Cyan
Write-Host "  - $ProjectRoot\data\config.json" -ForegroundColor White
Write-Host "  - $ProjectRoot\data\ylproxy.db (if migrated)" -ForegroundColor White
Write-Host ""
