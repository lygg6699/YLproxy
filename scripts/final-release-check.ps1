#!/usr/bin/env pwsh

[CmdletBinding()]
param(
    [switch]$SkipCoverage,
    [switch]$SkipPerformance,
    [switch]$SkipPublish,
    [switch]$SkipInstaller
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repoRoot

$env:DOTNET_CLI_UI_LANGUAGE = 'en-US'

Write-Host "=== YLproxy Final Release Check ===" -ForegroundColor Cyan

./scripts/prepare-runtime.ps1

dotnet restore YLproxy.sln
dotnet build YLproxy.sln -c Release --no-restore -warnaserror

if ($SkipCoverage) {
    dotnet test tests/YLproxy.Tests.csproj -c Release --no-build --no-restore --filter "TestCategory!=E2E"
}
else {
    dotnet test tests/YLproxy.Tests.csproj -c Release --no-build --no-restore --filter "TestCategory!=E2E" --collect:"XPlat Code Coverage" --results-directory ./coverage
}

if (-not $SkipPerformance) {
    dotnet test tests/YLproxy.Tests.csproj -c Release --no-build --no-restore --filter "TestCategory=Performance"
}

dotnet list YLproxy.sln package --vulnerable

if (-not $SkipPublish) {
    ./build/publish.ps1 -Configuration Release -CreateZip
}

if (-not $SkipInstaller) {
    $wixAvailable = $false
    try {
        wix --version | Out-Null
        $wixAvailable = $true
    }
    catch {
        $wixAvailable = $false
    }

    if ($wixAvailable) {
        $props = [xml](Get-Content "Directory.Build.props")
        $versionNode = $props.Project.PropertyGroup.Version
        $version = if ($versionNode) { $versionNode.InnerText.Trim() } else { "0.0.0" }

        wix build ./installer/Product.wxs -d publish="./build/publish" -d version="$version" -o "./installer/YLproxy-$version.msi"
    }
    else {
        Write-Warning "WiX tool not found; skipping MSI build."
    }
}

./scripts/verify-installer.ps1 -SkipMsi:$SkipInstaller

Write-Host "Final release check passed." -ForegroundColor Green
