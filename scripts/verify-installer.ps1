#!/usr/bin/env pwsh

[CmdletBinding()]
param(
    [string]$PublishDir = "build/publish",
    [string]$InstallerPath = "",
    [switch]$SkipMsi
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repoRoot

function Assert-PathExists {
    param(
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [string]$Message
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw $Message
    }
}

$publishFullPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $PublishDir))
Assert-PathExists -Path $publishFullPath -Message "Publish output not found: $publishFullPath"

$required = @(
    "YLproxy.GUI.exe",
    "AppSettings.json",
    "runtime/3proxy/bin64/3proxy.exe",
    "runtime/3proxy/bin64/FilePlugin.dll",
    "runtime/3proxy/bin64/StringsPlugin.dll"
)

foreach ($rel in $required) {
    $path = Join-Path $publishFullPath $rel
    Assert-PathExists -Path $path -Message "Required publish file missing: $rel"
}

Write-Host "[OK] Publish package files verified."

if (-not $SkipMsi) {
    if ([string]::IsNullOrWhiteSpace($InstallerPath)) {
        $latestMsi = Get-ChildItem -Path (Join-Path $repoRoot "installer") -Filter "*.msi" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1

        if ($null -eq $latestMsi) {
            throw "No MSI found under installer/."
        }

        $InstallerPath = $latestMsi.FullName
    }

    Assert-PathExists -Path $InstallerPath -Message "Installer not found: $InstallerPath"

    $msiInfo = Get-Item -LiteralPath $InstallerPath
    if ($msiInfo.Length -lt 200KB) {
        throw "Installer file too small, likely invalid: $InstallerPath"
    }

    Write-Host "[OK] MSI verified: $InstallerPath ($([math]::Round($msiInfo.Length / 1MB, 2)) MB)"
}

Write-Host "Installer verification passed."
