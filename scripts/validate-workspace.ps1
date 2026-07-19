[CmdletBinding()]
param(
    [string]$WorkspaceFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$workspaceRoot = Split-Path -Parent $projectRoot
if ([string]::IsNullOrWhiteSpace($WorkspaceFile)) {
    $WorkspaceFile = Join-Path $workspaceRoot 'YLproxy.code-workspace'
} else {
    $WorkspaceFile = [IO.Path]::GetFullPath($WorkspaceFile)
}

function Assert-Condition {
    param(
        [Parameter(Mandatory)]
        [bool]$Condition,
        [Parameter(Mandatory)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-File {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [string]$Description
    )

    Assert-Condition (Test-Path -LiteralPath $Path -PathType Leaf) "$Description was not found: $Path"
}

function Assert-Directory {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [string]$Description
    )

    Assert-Condition (Test-Path -LiteralPath $Path -PathType Container) "$Description was not found: $Path"
}

Assert-File -Path $WorkspaceFile -Description 'VS Code workspace file'
Assert-File -Path (Join-Path $projectRoot 'global.json') -Description 'global.json'
Assert-File -Path (Join-Path $projectRoot 'YLproxy.sln') -Description 'solution file'
Assert-File -Path (Join-Path $projectRoot 'AppSettings.json') -Description 'global settings file'
Assert-File -Path (Join-Path $projectRoot 'scripts\full-check.ps1') -Description 'Full Check script'
Assert-File -Path (Join-Path $projectRoot 'scripts\migrate-proxy-data.ps1') -Description 'proxy data migration script'
Assert-File -Path (Join-Path $projectRoot 'src\YLproxy.GUI\YLproxy.GUI.csproj') -Description 'GUI project file'
Assert-File -Path (Join-Path $projectRoot 'tests\YLproxy.Tests.csproj') -Description 'test project file'
Assert-Directory -Path (Join-Path $projectRoot 'data') -Description 'data directory'

$workspace = Get-Content -Raw -LiteralPath $WorkspaceFile | ConvertFrom-Json
$folders = @($workspace.folders)
Assert-Condition ($folders.Count -eq 1) 'The workspace must contain exactly one folder.'

# Resolve the workspace folder relative to the directory that actually contains
# the workspace file, so both layouts validate correctly:
#   * ci.code-workspace living at the repository root (folder path ".")
#   * YLproxy.code-workspace living beside the repository (folder path "YLproxy")
$workspaceFileDirectory = Split-Path -Parent $WorkspaceFile
$workspaceProjectPath = [IO.Path]::GetFullPath((Join-Path $workspaceFileDirectory ([string]$folders[0].path)))
Assert-Condition ($workspaceProjectPath -ieq $projectRoot) 'The workspace folder must point to the YLproxy project root.'

$tasks = @($workspace.tasks.tasks)
$requiredTaskLabels = @(
    'YLproxy: Validate Environment',
    'YLproxy: Restore',
    'YLproxy: Build',
    'YLproxy: Build Release',
    'YLproxy: Test',
    'YLproxy: Run GUI',
    'YLproxy: Full Check',
    'YLproxy: Full Check with Smoke Test',
    'YLproxy: Clean'
)

foreach ($label in $requiredTaskLabels) {
    Assert-Condition (@($tasks | Where-Object { $_.label -eq $label }).Count -eq 1) "Missing workspace task: $label"
}

$fullCheckTask = $tasks | Where-Object { $_.label -eq 'YLproxy: Full Check' } | Select-Object -First 1
$fullCheckArgs = @($fullCheckTask.args | ForEach-Object { [string]$_ })
Assert-Condition ($fullCheckArgs -contains '-SkipSmokeTest') 'The default Full Check task must remain non-destructive.'

$smokeTask = $tasks | Where-Object { $_.label -eq 'YLproxy: Full Check with Smoke Test' } | Select-Object -First 1
$smokeArgs = @($smokeTask.args | ForEach-Object { [string]$_ })
Assert-Condition (-not ($smokeArgs -contains '-SkipSmokeTest')) 'The explicit Smoke Test task must execute the isolated Smoke Test.'

$launchConfigurations = @($workspace.launch.configurations)
$guiLaunch = $launchConfigurations | Where-Object { $_.name -eq 'YLproxy GUI (Debug)' } | Select-Object -First 1
Assert-Condition ($null -ne $guiLaunch) 'Missing YLproxy GUI debug configuration.'
Assert-Condition ([string]$guiLaunch.program -like '*src/YLproxy.GUI/bin/Debug/net10.0-windows/YLproxy.GUI.dll') 'The GUI debug configuration points to an unexpected assembly.'

$globalSettings = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'global.json') | ConvertFrom-Json
$expectedSdk = [string]$globalSettings.sdk.version
Assert-Condition (-not [string]::IsNullOrWhiteSpace($expectedSdk)) 'global.json does not define an SDK version.'
$rollForward = [string]$globalSettings.sdk.rollForward
Assert-Condition ($rollForward -in @('latestMinor', 'latestPatch')) 'global.json must use a latestMinor or latestPatch roll-forward policy.'

$settings = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'AppSettings.json') | ConvertFrom-Json
$runtimeDirectory = ([string]$settings.ThreeProxy.RuntimeDirectory).Replace('/', '\')
$runtimeRoot = Join-Path $projectRoot $runtimeDirectory
Assert-Directory -Path (Join-Path $runtimeRoot 'bin64') -Description '3proxy runtime bin64 directory'
Assert-File -Path (Join-Path $runtimeRoot 'bin64\3proxy.exe') -Description '3proxy executable'
foreach ($requiredDll in @($settings.ThreeProxy.RequiredDlls)) {
    Assert-File -Path (Join-Path $runtimeRoot "bin64\$requiredDll") -Description "3proxy dependency $requiredDll"
}

$dataConfigPath = Join-Path $projectRoot 'data\config.json'
if (Test-Path -LiteralPath $dataConfigPath -PathType Leaf) {
    $dataConfig = Get-Content -Raw -LiteralPath $dataConfigPath | ConvertFrom-Json
    foreach ($proxy in @($dataConfig.Proxies)) {
        foreach ($field in @('Username', 'Password')) {
            $value = [string]$proxy.$field
            Assert-Condition ([string]::IsNullOrEmpty($value) -or $value.StartsWith('dpapi:v1:', [StringComparison]::Ordinal)) "Proxy $($proxy.Id) contains an unprotected $field value."
        }
    }
}

Assert-Condition ($null -ne (Get-Command dotnet -ErrorAction SilentlyContinue)) 'The dotnet command is not available.'
Assert-Condition ($null -ne (Get-Command pwsh -ErrorAction SilentlyContinue)) 'The pwsh command is not available.'

Push-Location $projectRoot
try {
    $actualSdk = (& dotnet --version).Trim()
} finally {
    Pop-Location
}
$expectedSdkParts = $expectedSdk -split '\.'
$actualSdkParts = $actualSdk -split '\.'
Assert-Condition ($expectedSdkParts.Count -eq 3 -and $actualSdkParts.Count -eq 3) 'The active .NET SDK version format is invalid.'

$sameMajorMinor = ($actualSdkParts[0] -eq $expectedSdkParts[0]) -and ($actualSdkParts[1] -eq $expectedSdkParts[1])
if ($rollForward -eq 'latestPatch') {
    # latestPatch: stay inside the pinned feature band, only newer patches allowed.
    $expectedFeatureBand = [int]$expectedSdkParts[2] - ([int]$expectedSdkParts[2] % 100)
    $actualFeatureBand = [int]$actualSdkParts[2] - ([int]$actualSdkParts[2] % 100)
    $sdkMatchesPolicy = $sameMajorMinor -and ($actualFeatureBand -eq $expectedFeatureBand) -and ([int]$actualSdkParts[2] -ge [int]$expectedSdkParts[2])
} else {
    # latestMinor: same major.minor, any newer feature band/patch at or above the baseline.
    $sdkMatchesPolicy = $sameMajorMinor -and ([int]$actualSdkParts[2] -ge [int]$expectedSdkParts[2])
}
Assert-Condition $sdkMatchesPolicy "The active .NET SDK ($actualSdk) does not satisfy global.json $rollForward policy (baseline $expectedSdk)."

Write-Output 'Workspace and environment validation passed.'
Write-Output "Project root: $projectRoot"
Write-Output "Workspace file: $WorkspaceFile"
Write-Output "SDK: $actualSdk"
Write-Output "3proxy runtime: $runtimeRoot"
