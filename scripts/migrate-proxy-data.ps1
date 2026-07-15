[CmdletBinding()]
param(
    [switch]$Apply
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

Copy-Item -LiteralPath $configPath -Destination $backupPath -Force

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
    foreach ($proxy in @($config.Proxies)) {
        foreach ($field in @('Username', 'Password')) {
            $value = [string]$proxy.$field
            if (-not [string]::IsNullOrEmpty($value) -and -not $value.StartsWith('dpapi:v1:', [StringComparison]::Ordinal)) {
                throw "Proxy $($proxy.Id) still contains an unprotected $field value."
            }
        }
    }

    $migrationSucceeded = $true
    Write-Output "Proxy data migration completed: $(@($config.Proxies).Count) proxy entries checked."
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
}
