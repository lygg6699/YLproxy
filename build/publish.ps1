<#
.SYNOPSIS
    YLproxy Release Package Builder
.DESCRIPTION
    Builds a release package for YLproxy including the published .NET app,
    3proxy runtime dependencies, and a version metadata file.
    Optionally creates a ZIP archive for distribution.

.PARAMETER OutputDir
    Directory where the published output will be placed (default: build/publish)

.PARAMETER Configuration
    Build configuration (default: Release)

.PARAMETER Runtime
    Target runtime identifier (default: win-x64)

.PARAMETER CreateZip
    If specified, creates a ZIP archive of the published output

.PARAMETER ZipOutput
    Path for the output ZIP file (default: build/YLproxy-v{version}-win-x64.zip)

.EXAMPLE
    .\build\publish.ps1
    Builds Release for win-x64 to build/publish/

.EXAMPLE
    .\build\publish.ps1 -Configuration Debug -CreateZip
    Builds Debug and creates a ZIP archive

.EXAMPLE
    .\build\publish.ps1 -OutputDir "C:\deploy\ylproxy" -CreateZip
    Publishes to a custom directory and zips it
#>

[CmdletBinding()]
param(
    [string]$OutputDir = "",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$Runtime = "win-x64",

    [switch]$CreateZip,

    [string]$ZipOutput = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $ProjectRoot "build\publish"
}
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  YLproxy Release Publisher" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "[INFO] Configuration : $Configuration"
Write-Host "[INFO] Runtime        : $Runtime"
Write-Host "[INFO] Output         : $OutputDir"
Write-Host ""

# Clean
if (Test-Path -LiteralPath $OutputDir) {
    Write-Host "[1/6] Cleaning previous output..." -ForegroundColor Yellow
    Remove-Item -Path $OutputDir -Recurse -Force -ErrorAction Stop
}

# Publish
Write-Host "[2/6] Publishing YLproxy.GUI..." -ForegroundColor Yellow
$publishArgs = @(
    "publish", "src/YLproxy.GUI/YLproxy.GUI.csproj",
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--self-contained", "false",
    "--output", $OutputDir
)
$publishResult = & dotnet @publishArgs 2>&1
Write-Host ($publishResult -join "`n")
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] dotnet publish failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit 1
}
Write-Host "[OK]   Publish complete." -ForegroundColor Green

# Verify output
Write-Host "[3/6] Verifying published output..." -ForegroundColor Yellow
$guiExe = Join-Path $OutputDir "YLproxy.GUI.exe"
$appSettings = Join-Path $OutputDir "AppSettings.json"
if (-not (Test-Path -LiteralPath $guiExe -PathType Leaf)) {
    Write-Host "[ERROR] YLproxy.GUI.exe not found in output." -ForegroundColor Red
    exit 1
}
if (-not (Test-Path -LiteralPath $appSettings -PathType Leaf)) {
    Write-Host "[WARN] AppSettings.json not found in output." -ForegroundColor Yellow
}
Write-Host "[OK]   Output verified." -ForegroundColor Green

# Copy 3proxy runtime
Write-Host "[4/6] Copying 3proxy runtime..." -ForegroundColor Yellow
$source3proxy = Join-Path $ProjectRoot "runtime\3proxy\bin64"
$target3proxy = Join-Path $OutputDir "runtime\3proxy\bin64"
New-Item -ItemType Directory -Force -Path $target3proxy | Out-Null

Get-ChildItem -Path $source3proxy -Include "*.exe", "*.dll" | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $target3proxy -Force
    Write-Host "  Copied: $($_.Name)"
}
Write-Host "[OK]   3proxy runtime copied." -ForegroundColor Green

# Create target cfg and logs directories
$cfgDir = Join-Path $OutputDir "runtime\3proxy\cfg"
$logDir = Join-Path $OutputDir "runtime\3proxy\logs"
New-Item -ItemType Directory -Force -Path $cfgDir | Out-Null
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

# Generate version file
Write-Host "[5/6] Generating version.txt..." -ForegroundColor Yellow
$versionFile = Join-Path $OutputDir "version.txt"
$exeInfo = Get-Item $guiExe
$threeProxyExe = Join-Path $OutputDir "runtime\3proxy\bin64\3proxy.exe"
$threeProxyVer = "unknown"
if (Test-Path $threeProxyExe) {
    $threeProxyVer = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($threeProxyExe).FileVersion
}

$dotnetVer = (dotnet --version 2>$null) ?? "unknown"
$buildTime = Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC"
$buildTimeUtc = (Get-Date).ToUniversalTime().ToString("o")

@"
YLproxy Release Package
========================
Build Time:      $buildTime
Build Time (o):  $buildTimeUtc
Configuration:   $Configuration
Runtime:         $Runtime
.NET Version:    $dotnetVer
3proxy Version:  $threeProxyVer
GUI Executable:  $($exeInfo.Name)
GUI Size:        $($exeInfo.Length) bytes
"@ | Out-File -FilePath $versionFile -Encoding UTF8

Write-Host "[OK]   version.txt created." -ForegroundColor Green

# Optional ZIP
if ($CreateZip) {
    Write-Host "[6/6] Creating ZIP archive..." -ForegroundColor Yellow

    if ([string]::IsNullOrWhiteSpace($ZipOutput)) {
        $v = $threeProxyVer -replace '[^\d\.]', ''
        $ZipOutput = Join-Path $ProjectRoot "build\YLproxy-v${v}-win-x64.zip"
    }

    if (Test-Path $ZipOutput) {
        Remove-Item $ZipOutput -Force
    }

    Compress-Archive -Path "$OutputDir\*" -DestinationPath $ZipOutput -Force
    $zipInfo = Get-Item $ZipOutput
    Write-Host "[OK]   ZIP created: $ZipOutput ($([math]::Round($zipInfo.Length/1MB, 2)) MB)" -ForegroundColor Green
} else {
    Write-Host "[6/6] Skipped ZIP creation (use -CreateZip to enable)." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Publish Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Output: $OutputDir" -ForegroundColor White
Write-Host ""
