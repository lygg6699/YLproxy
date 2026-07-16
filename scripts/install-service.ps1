<#
.SYNOPSIS
    YLproxy Windows Service Installer
.DESCRIPTION
    Installs, uninstalls, or checks the status of the YLproxy Windows Service.
    The service runs the YLproxy.GUI.exe as a background service managing local proxy instances.

.PARAMETER Action
    -Install   : Creates and starts the YLproxy Windows Service (requires Administrator)
    -Uninstall : Stops and removes the YLproxy Windows Service (requires Administrator)
    -Status    : Displays the current service status

.PARAMETER ServiceName
    Windows service name (default: YLproxyService)

.PARAMETER BinaryPath
    Path to the YLproxy.GUI.exe executable
    Default: src/YLproxy.GUI/bin/Release/net10.0-windows/YLproxy.GUI.exe

.PARAMETER DisplayName
    Display name for the Windows service (default: YLproxy 代理转换服务)

.PARAMETER PortRangeStart
    Start of the TCP port range for firewall rules (default: 9001)

.PARAMETER PortRangeEnd
    End of the TCP port range for firewall rules (default: 9100)

.EXAMPLE
    .\scripts\install-service.ps1 -Install
    Installs and starts the YLproxy service with default settings

.EXAMPLE
    .\scripts\install-service.ps1 -Uninstall
    Stops and removes the YLproxy service

.EXAMPLE
    .\scripts\install-service.ps1 -Status
    Checks the current status of the YLproxy service

.EXAMPLE
    .\scripts\install-service.ps1 -Install -BinaryPath "C:\YLproxy\YLproxy.GUI.exe"
    Installs using a custom binary path

.NOTES
    Requires Administrator privileges for -Install and -Uninstall actions.
    Data files (config.json, ylproxy.db) are NOT deleted during uninstallation.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Install", "Uninstall", "Status")]
    [string]$Action,

    [string]$ServiceName = "YLproxyService",

    [string]$BinaryPath = "",

    [string]$DisplayName = "YLproxy 代理转换服务",

    [int]$PortRangeStart = 9001,

    [int]$PortRangeEnd = 9100
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ─── Resolve script and project root ───
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir

# Default binary path
if ([string]::IsNullOrWhiteSpace($BinaryPath)) {
    $BinaryPath = Join-Path $ProjectRoot "src\YLproxy.GUI\bin\Release\net10.0-windows\YLproxy.GUI.exe"
}
$BinaryPath = [System.IO.Path]::GetFullPath($BinaryPath)

# ─── Helper functions ───
function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "[OK]   $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-ErrorMsg {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Assert-Admin {
    if (-not (Test-Admin)) {
        Write-ErrorMsg "This action requires Administrator privileges. Please run PowerShell as Administrator."
        exit 1
    }
}

function Invoke-ScCommand {
    param([string]$Arguments)
    $cmd = "sc.exe $Arguments"
    Write-Info "Executing: $cmd"
    $result = cmd /c "$cmd 2>&1"
    Write-Host $result
    if ($LASTEXITCODE -ne 0) {
        Write-ErrorMsg "Command failed with exit code $LASTEXITCODE"
        Write-Host $result
        return $false
    }
    return $true
}

# ─── Action: Status ───
if ($Action -eq "Status") {
    Write-Info "Checking status of service: $ServiceName"
    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($null -eq $svc) {
        Write-Warn "Service '$ServiceName' is not installed."
    } else {
        Write-Success "Service '$ServiceName' Status: $($svc.Status)"
        Write-Info "StartType: $($svc.StartType)"
    }

    # Check firewall rules
    $fwRuleName = "YLproxy Service Inbound"
    $fwRule = Get-NetFirewallRule -DisplayName $fwRuleName -ErrorAction SilentlyContinue
    if ($null -eq $fwRule) {
        Write-Warn "Firewall rule '$fwRuleName' does not exist."
    } else {
        Write-Success "Firewall rule '$fwRuleName': Enabled=$($fwRule.Enabled)"
    }

    exit 0
}

# ─── Action: Install ───
if ($Action -eq "Install") {
    Assert-Admin

    # a) Verify binary exists
    if (-not (Test-Path -LiteralPath $BinaryPath -PathType Leaf)) {
        Write-ErrorMsg "Binary not found: $BinaryPath"
        Write-Warn "Run 'dotnet publish src/YLproxy.GUI -c Release -r win-x64 --self-contained false' first, or use build/publish.ps1"
        exit 1
    }
    Write-Success "Binary verified: $BinaryPath"

    # b) Check if service already exists
    $existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($null -ne $existing) {
        Write-Warn "Service '$ServiceName' already exists."
        if ($existing.Status -eq "Running") {
            Write-Info "Stopping existing service..."
            Stop-Service -Name $ServiceName -Force
            Start-Sleep -Seconds 2
        }
        Write-Info "Deleting existing service..."
        Invoke-ScCommand "delete $ServiceName" | Out-Null
        Start-Sleep -Seconds 1
    }

    # c) Create the service
    Write-Info "Creating Windows service: $ServiceName"
    $createResult = Invoke-ScCommand "create $ServiceName binPath= `"$BinaryPath`" start= auto DisplayName= `"$DisplayName`""
    if (-not $createResult) {
        Write-ErrorMsg "Failed to create service."
        exit 1
    }
    Write-Success "Service '$ServiceName' created."

    # d) Configure firewall inbound rule
    Write-Info "Configuring firewall: TCP ports ${PortRangeStart}-${PortRangeEnd}"
    $fwRuleName = "YLproxy Service Inbound"
    $existingFw = Get-NetFirewallRule -DisplayName $fwRuleName -ErrorAction SilentlyContinue
    if ($null -ne $existingFw) {
        Write-Info "Removing existing firewall rule..."
        Remove-NetFirewallRule -DisplayName $fwRuleName
    }

    try {
        New-NetFirewallRule -DisplayName $fwRuleName `
            -Direction Inbound `
            -Protocol TCP `
            -LocalPort $PortRangeStart-$PortRangeEnd `
            -Action Allow `
            -Profile Any `
            -Description "Allows YLproxy local proxy ports (TCP ${PortRangeStart}-${PortRangeEnd})"
        Write-Success "Firewall rule '$fwRuleName' created."
    } catch {
        Write-Warn "Failed to create firewall rule: $_"
        Write-Info "You may need to add the rule manually."
    }

    # e) Start the service
    Write-Info "Starting service: $ServiceName"
    Start-Service -Name $ServiceName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3

    # f) Verify service status
    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($null -eq $svc) {
        Write-ErrorMsg "Service '$ServiceName' was not found after creation."
        exit 1
    }

    if ($svc.Status -eq "Running") {
        Write-Success "Service '$ServiceName' is RUNNING."
    } else {
        Write-Warn "Service '$ServiceName' status: $($svc.Status)"
        Write-Info "Check Windows Event Viewer for details, or run: sc.exe query $ServiceName"
    }

    Write-Success "Installation complete."
    exit 0
}

# ─── Action: Uninstall ───
if ($Action -eq "Uninstall") {
    Assert-Admin

    # a) Stop service if running
    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($null -ne $svc) {
        if ($svc.Status -eq "Running") {
            Write-Info "Stopping service: $ServiceName"
            try {
                Stop-Service -Name $ServiceName -Force
                Start-Sleep -Seconds 2
                Write-Success "Service stopped."
            } catch {
                Write-Warn "Failed to stop service gracefully: $_"
            }
        }

        # b) Delete service
        Write-Info "Deleting service: $ServiceName"
        $deleteResult = Invoke-ScCommand "delete $ServiceName"
        if ($deleteResult) {
            Write-Success "Service '$ServiceName' deleted."
        }
    } else {
        Write-Warn "Service '$ServiceName' is not installed."
    }

    # c) Remove firewall rule
    $fwRuleName = "YLproxy Service Inbound"
    $existingFw = Get-NetFirewallRule -DisplayName $fwRuleName -ErrorAction SilentlyContinue
    if ($null -ne $existingFw) {
        Write-Info "Removing firewall rule: $fwRuleName"
        Remove-NetFirewallRule -DisplayName $fwRuleName
        Write-Success "Firewall rule removed."
    } else {
        Write-Warn "Firewall rule '$fwRuleName' not found."
    }

    # d) Remind about data preservation
    Write-Info "User data has NOT been deleted:"
    Write-Info "  - data/config.json"
    Write-Info "  - data/ylproxy.db (if migrated)"

    Write-Success "Uninstallation complete."
    exit 0
}
