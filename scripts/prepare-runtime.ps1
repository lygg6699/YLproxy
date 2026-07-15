#!/usr/bin/env pwsh
# Download and prepare the pinned Windows x64 3proxy runtime.

[CmdletBinding()]
param(
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$version = '0.9.7'
$archiveName = "3proxy-$version-x64.zip"
$downloadUri = "https://github.com/3proxy/3proxy/releases/download/$version/$archiveName"
$expectedSha256 = 'e94f4967f46f859d49345afdcb1830cf9b042b5b9fdfc3bef33d65e95715cae3'
$requiredFiles = @('3proxy.exe', 'FilePlugin.dll', 'StringsPlugin.dll')

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runtimeRoot = Join-Path $repositoryRoot 'runtime\3proxy'
$binaryDirectory = Join-Path $runtimeRoot 'bin64'
$existingMissingFiles = @(
    $requiredFiles | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $binaryDirectory $_) -PathType Leaf)
    }
)

if (-not $Force -and $existingMissingFiles.Count -eq 0) {
    Write-Host "3proxy $version is already prepared at $binaryDirectory."
    exit 0
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) "YLproxy-3proxy-$([Guid]::NewGuid().ToString('N'))"
$archivePath = Join-Path $tempRoot $archiveName
$extractRoot = Join-Path $tempRoot 'extract'
$stagingDirectory = Join-Path $tempRoot 'bin64'

New-Item -ItemType Directory -Path $tempRoot, $extractRoot, $stagingDirectory -Force | Out-Null

try {
    Write-Host "Downloading $downloadUri"
    Invoke-WebRequest -Uri $downloadUri -OutFile $archivePath

    $actualSha256 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualSha256 -ne $expectedSha256) {
        throw "3proxy archive hash mismatch. Expected $expectedSha256 but received $actualSha256."
    }

    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractRoot -Force
    $sourceExecutable = @(Get-ChildItem -LiteralPath $extractRoot -Filter '3proxy.exe' -File -Recurse | Select-Object -First 1)
    if ($sourceExecutable.Count -ne 1) {
        throw "The 3proxy archive did not contain exactly one 3proxy.exe."
    }

    $sourceDirectory = $sourceExecutable[0].DirectoryName
    Copy-Item -Path (Join-Path $sourceDirectory '*') -Destination $stagingDirectory -Recurse -Force

    $stagedMissingFiles = @(
        $requiredFiles | Where-Object {
            -not (Test-Path -LiteralPath (Join-Path $stagingDirectory $_) -PathType Leaf)
        }
    )
    if ($stagedMissingFiles.Count -gt 0) {
        throw "The 3proxy archive is missing required files: $($stagedMissingFiles -join ', ')"
    }

    if ($Force -and (Test-Path -LiteralPath $binaryDirectory)) {
        Remove-Item -LiteralPath $binaryDirectory -Recurse -Force
    }

    New-Item -ItemType Directory -Path $binaryDirectory -Force | Out-Null
    Copy-Item -Path (Join-Path $stagingDirectory '*') -Destination $binaryDirectory -Recurse -Force
    Write-Host "3proxy $version prepared at $binaryDirectory."
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
