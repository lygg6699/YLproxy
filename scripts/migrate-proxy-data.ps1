[CmdletBinding()]
param(
    [switch]$Apply,

    [Parameter(Mandatory = $false)]
    [ValidateSet('', '1.0', '1.1')]
    [string]$TargetVersion = '1.1'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $Apply) {
    throw 'This operation changes data/config.json. Re-run with -Apply after reviewing the migration scope.'
}

$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$configPath = Join-Path $projectRoot 'data\config.json'
$appPath = Join-Path $projectRoot 'src\YLproxy.GUI\bin\Debug\net10.0-windows\YLproxy.GUI.exe'
$backupPath = Join-Path ([IO.Path]::GetTempPath()) "YLproxy-config-backup-$([Guid]::NewGuid().ToString('N')).json"
$process = $null
$migrationSucceeded = $false
$restoreSucceeded = $false

function Stop-MigrationProcess {
    if ($null -eq $process) {
        return
    }

    try {
        $process.Refresh()
        if (-not $process.HasExited) {
            $process.Kill($true)
            $process.WaitForExit(5000) | Out-Null
        }
    }
    catch {
        Write-Warning "Unable to stop migration process cleanly: $($_.Exception.Message)"
    }
}

function Get-ConfigVersion {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    try {
        $config = Get-Content -Raw -LiteralPath $Path -ErrorAction Stop | ConvertFrom-Json
        $version = [string]$config.Version
        if ([string]::IsNullOrEmpty($version)) {
            return $null
        }
        return $version
    }
    catch {
        Write-Warning "Unable to read version from config: $($_.Exception.Message)"
        return $null
    }
}

function Test-IsVersionUpgradeNeeded {
    param([string]$CurrentVersion, [string]$TargetVersion)

    if ([string]::IsNullOrEmpty($CurrentVersion)) {
        return $true
    }

    return $CurrentVersion -ne $TargetVersion
}

function Write-VersionComplianceReport {
    param([string]$ConfigPath)

    $version = Get-ConfigVersion -Path $ConfigPath
    $versionDisplay = if ($version) { $version } else { '<none> (legacy)' }
    $complianceStatus = if ([string]::IsNullOrEmpty($version) -or $version -ne '1.1') { 'OUTDATED' } else { 'CURRENT' }

    Write-Output "Config version: $versionDisplay"
    Write-Output "Version status: $complianceStatus"
    Write-Output "Current schema version: 1.1"

    if ($complianceStatus -eq 'OUTDATED') {
        Write-Output "Action: Version upgrade recommended ($versionDisplay → 1.1)"
    }
    else {
        Write-Output "Action: No upgrade needed"
    }
}

# ── Pre-flight checks ────────────────────────────────────────

if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    throw "Proxy data file was not found: $configPath"
}

if (-not (Test-Path -LiteralPath $appPath -PathType Leaf)) {
    throw "Build the GUI before migration: $appPath"
}

$existingGuiProcesses = @(Get-Process -Name 'YLproxy.GUI' -ErrorAction SilentlyContinue)
if ($existingGuiProcesses.Count -gt 0) {
    throw 'A YLproxy GUI process is already running. Close it before migrating proxy data.'
}

# ── Version compliance report ─────────────────────────────────
Write-Output "=== Version Compliance Report ==="
Write-VersionComplianceReport -ConfigPath $configPath
Write-Output ""

$currentVersion = Get-ConfigVersion -Path $configPath
$needsUpgrade = Test-IsVersionUpgradeNeeded -CurrentVersion $currentVersion -TargetVersion $TargetVersion

if (-not $needsUpgrade) {
    Write-Output "Config is already at target version $TargetVersion. Proceeding with credential migration only."
}
else {
    Write-Output "Config version upgrade needed: $([string]$currentVersion) → $TargetVersion"
}

# ── Backup ────────────────────────────────────────────────────
$versionSuffix = if ($currentVersion) { $currentVersion.Replace('.', '-') } else { 'legacy' }
$versionedBackupPath = Join-Path ([IO.Path]::GetTempPath()) "YLproxy-config-$versionSuffix-backup-$([Guid]::NewGuid().ToString('N')).json"
Copy-Item -LiteralPath $configPath -Destination $backupPath -Force
Copy-Item -LiteralPath $configPath -Destination $versionedBackupPath -Force
Write-Output "Backup saved: $backupPath"
Write-Output "Version-tagged backup saved: $versionedBackupPath"

# ── Main migration ────────────────────────────────────────────
try {
    $process = Start-Process -FilePath $appPath -WorkingDirectory $projectRoot -PassThru
    $deadline = (Get-Date).AddSeconds(10)

    do {
        $process.Refresh()
        if ($process.HasExited) {
            throw "YLproxy GUI exited during migration with code $($process.ExitCode)."
        }

        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)

    $process.Refresh()
    if ($process.HasExited) {
        throw "YLproxy GUI exited during migration with code $($process.ExitCode)."
    }

    Stop-MigrationProcess
    $process = $null

    $config = Get-Content -Raw -LiteralPath $configPath | ConvertFrom-Json

    # ── Version upgrade ────────────────────────────────────────
    $postUpgradeVersion = [string]$config.Version
    if ($needsUpgrade) {
        if ([string]::IsNullOrEmpty($postUpgradeVersion)) {
            Write-Output "Upgrading config from legacy (no version) to $TargetVersion"
            $config | Add-Member -MemberType NoteProperty -Name 'Version' -Value $TargetVersion -Force
        }
        elseif ($postUpgradeVersion -ne $TargetVersion) {
            Write-Output "Upgrading config from version $postUpgradeVersion to $TargetVersion"
            $config.Version = $TargetVersion
        }
    }

    # ── Credential migration check ─────────────────────────────
    $credentialMigrationNeeded = $false
    foreach ($proxy in @($config.Proxies)) {
        foreach ($field in @('Username', 'Password')) {
            $value = [string]$proxy.$field
            if (-not [string]::IsNullOrEmpty($value) -and -not $value.StartsWith('dpapi:v1:', [StringComparison]::Ordinal)) {
                $credentialMigrationNeeded = $true
                break
            }
        }
        if ($credentialMigrationNeeded) { break }
    }

    if ($credentialMigrationNeeded) {
        Write-Output "Credential migration required: re-encrypting plaintext credentials via DPAPI..."
        foreach ($proxy in @($config.Proxies)) {
            foreach ($field in @('Username', 'Password')) {
                $value = [string]$proxy.$field
                if (-not [string]::IsNullOrEmpty($value) -and -not $value.StartsWith('dpapi:v1:', [StringComparison]::Ordinal)) {
                    throw "Proxy $($proxy.Id) still contains an unprotected $field value after GUI migration."
                }
            }
        }
        Write-Output "All proxy credentials are now DPAPI-encrypted."
    }
    else {
        Write-Output "All credentials are already encrypted (dpapi:v1: prefix detected). No credential migration needed."
    }

    # ── Post-migration validation ──────────────────────────────
    $config = Get-Content -Raw -LiteralPath $configPath | ConvertFrom-Json
    $finalVersion = [string]$config.Version
    Write-Output "Post-migration version: $([string]::IsNullOrEmpty($finalVersion) ? '<none>' : $finalVersion)"

    foreach ($proxy in @($config.Proxies)) {
        foreach ($field in @('Username', 'Password')) {
            $value = [string]$proxy.$field
            if (-not [string]::IsNullOrEmpty($value) -and -not $value.StartsWith('dpapi:v1:', [StringComparison]::Ordinal)) {
                throw "Proxy $($proxy.Id) still contains an unprotected $field value."
            }
        }
    }

    $migrationSucceeded = $true

    # ── Summary ────────────────────────────────────────────────
    Write-Output ""
    Write-Output "=== Migration Summary ==="
    Write-Output "Proxy entries processed: $(@($config.Proxies).Count)"
    Write-Output "Target version: $TargetVersion"
    Write-Output "Final version: $([string]::IsNullOrEmpty($finalVersion) ? '<none>' : $finalVersion)"
    Write-Output "Credential migration: $(-not $credentialMigrationNeeded)"
    Write-Output "Status: SUCCESS"
}
catch {
    Stop-MigrationProcess
    try {
        Copy-Item -LiteralPath $backupPath -Destination $configPath -Force
        $restoreSucceeded = $true
        Write-Warning 'Migration failed; the original proxy data was restored.'
    }
    catch {
        Write-Error "Migration failed and automatic restore also failed. Backup retained at $backupPath"
    }

    throw
}
finally {
    Stop-MigrationProcess
    if (($migrationSucceeded -or $restoreSucceeded) -and (Test-Path -LiteralPath $backupPath)) {
        Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
    }
    if ($migrationSucceeded -and (Test-Path -LiteralPath $versionedBackupPath)) {
        Remove-Item -LiteralPath $versionedBackupPath -Force -ErrorAction SilentlyContinue
    }
}

