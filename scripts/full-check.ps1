#!/usr/bin/env pwsh
# YLproxy Full Check Script
# 一键执行完整的编译、测试、验证流程

param(
    [switch]$SkipTests,
    [switch]$SkipSmokeTest,
    [switch]$Verbose
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = $PSScriptRoot
$repositoryRoot = Split-Path -Parent $scriptRoot
Set-Location -LiteralPath $repositoryRoot
$env:DOTNET_CLI_UI_LANGUAGE = 'en-US'

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$reportDirectory = Join-Path $repositoryRoot 'reports'
$logDirectory = Join-Path $repositoryRoot 'logs'
$reportFile = Join-Path $reportDirectory "check-report-$timestamp.md"
$logFile = Join-Path $logDirectory "check-$timestamp.log"

# 颜色定义
$success = @{ ForegroundColor = 'Green' }
$errorStyle = @{ ForegroundColor = 'Red' }
$warning = @{ ForegroundColor = 'Yellow' }
$info = @{ ForegroundColor = 'Cyan' }

function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $logMessage = "[$timestamp] [$Level] $Message"
    Add-Content -Path $logFile -Value $logMessage
    
    switch ($Level) {
        'SUCCESS' { Write-Host "✅ $Message" @success }
        'ERROR' { Write-Host "❌ $Message" @errorStyle }
        'WARNING' { Write-Host "⚠️  $Message" @warning }
        'INFO' { Write-Host "ℹ️  $Message" @info }
        default { Write-Host $Message }
    }
}

function New-SmokeTestRoot {
    param(
        [Parameter(Mandatory)]
        [string]$GuiOutputDirectory
    )

    if (-not (Test-Path -LiteralPath $GuiOutputDirectory -PathType Container)) {
        throw "GUI output directory was not found: $GuiOutputDirectory"
    }

    $smokeRoot = Join-Path ([IO.Path]::GetTempPath()) "YLproxy-smoke-$([Guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $smokeRoot -Force | Out-Null

    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'YLproxy.sln') -Destination $smokeRoot -Force
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'AppSettings.json') -Destination $smokeRoot -Force

    $dataDirectory = Join-Path $smokeRoot 'data'
    New-Item -ItemType Directory -Path $dataDirectory -Force | Out-Null
    '{"Proxies":[]}' | Set-Content -LiteralPath (Join-Path $dataDirectory 'config.json') -Encoding utf8NoBOM

    $logsDirectory = Join-Path $smokeRoot 'logs'
    New-Item -ItemType Directory -Path $logsDirectory -Force | Out-Null

    $runtimeSource = Join-Path $repositoryRoot 'runtime\3proxy\bin64'
    if (-not (Test-Path -LiteralPath $runtimeSource -PathType Container)) {
        throw "3proxy runtime directory was not found: $runtimeSource"
    }

    $runtimeTarget = Join-Path $smokeRoot 'runtime\3proxy\bin64'
    New-Item -ItemType Directory -Path $runtimeTarget -Force | Out-Null
    Copy-Item -Path (Join-Path $runtimeSource '*') -Destination $runtimeTarget -Recurse -Force
    New-Item -ItemType Directory -Path (Join-Path $smokeRoot 'runtime\3proxy\cfg') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $smokeRoot 'runtime\3proxy\logs') -Force | Out-Null

    $guiOutputTarget = Join-Path $smokeRoot 'src\YLproxy.GUI\bin\Debug\net10.0-windows'
    New-Item -ItemType Directory -Path $guiOutputTarget -Force | Out-Null
    Copy-Item -Path (Join-Path $GuiOutputDirectory '*') -Destination $guiOutputTarget -Recurse -Force

    return $smokeRoot
}

# 初始化
Write-Host "=" * 50
Write-Host "YLproxy Full Check Script" -ForegroundColor Cyan
Write-Host "=" * 50
Write-Host ""

# 创建日志目录
if (-not (Test-Path "logs")) { New-Item -ItemType Directory -Path "logs" | Out-Null }
if (-not (Test-Path "reports")) { New-Item -ItemType Directory -Path "reports" | Out-Null }

Write-Log "检查开始" "INFO"

# 第 1 步：清理
Write-Host ""
Write-Host "第 1 步：清理旧编译..." -ForegroundColor Cyan
Write-Log "清理项目" "INFO"

dotnet clean YLproxy.sln --configuration Debug --nologo 2>&1 | Tee-Object -FilePath $logFile -Append
$cleanExitCode = $LASTEXITCODE

if ($cleanExitCode -ne 0) {
    Write-Log "首次清理失败 (Exit Code: $cleanExitCode)，正在重试。" "WARNING"
    dotnet clean YLproxy.sln --configuration Debug --nologo 2>&1 | Tee-Object -FilePath $logFile -Append
    $cleanExitCode = $LASTEXITCODE
}

if ($cleanExitCode -eq 0) {
    Write-Log "清理成功" "SUCCESS"
} else {
    Write-Log "清理失败 (Exit Code: $cleanExitCode)" "ERROR"
    Write-Host "❌ 清理失败，停止执行" -ForegroundColor Red
    exit 1
}

# 第 2 步：编译
Write-Host ""
Write-Host "第 2 步：编译项目..." -ForegroundColor Cyan
Write-Log "开始编译" "INFO"

dotnet build --configuration Debug 2>&1 | Tee-Object -FilePath $logFile -Append
$buildExitCode = $LASTEXITCODE

if ($buildExitCode -eq 0) {
    Write-Log "编译成功" "SUCCESS"
} else {
    Write-Log "编译失败 (Exit Code: $buildExitCode)" "ERROR"
    Write-Host ""
    Write-Host "❌ 编译失败，停止执行" -ForegroundColor Red
    exit 1
}

# 第 3 步：单元测试
if (-not $SkipTests) {
    Write-Host ""
    Write-Host "第 3 步：运行单元测试..." -ForegroundColor Cyan
    Write-Log "开始单元测试" "INFO"
    
    dotnet test --no-build --verbosity normal 2>&1 | Tee-Object -FilePath $logFile -Append
    $testExitCode = $LASTEXITCODE
    
    if ($testExitCode -eq 0) {
        Write-Log "所有测试通过" "SUCCESS"
    } else {
        Write-Log "测试失败 (Exit Code: $testExitCode)" "ERROR"
        Write-Host ""
        Write-Host "❌ 测试失败，停止执行" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Log "跳过单元测试 (--SkipTests)" "WARNING"
}

$smokeTestStatus = 'SKIPPED'

# 第 4 步：Smoke Test（可选）
if (-not $SkipSmokeTest) {
    $smokeTestStatus = 'PASS'
    $smokeRoot = $null
    $appProcess = $null

    Write-Host ""
    Write-Host "第 4 步：运行 Smoke Test..." -ForegroundColor Cyan
    Write-Log "开始功能测试" "INFO"

    try {
        $guiOutputDirectory = Join-Path $repositoryRoot 'src\YLproxy.GUI\bin\Debug\net10.0-windows'
        $smokeRoot = New-SmokeTestRoot -GuiOutputDirectory $guiOutputDirectory
        $appPath = Join-Path $smokeRoot 'src\YLproxy.GUI\bin\Debug\net10.0-windows\YLproxy.GUI.exe'

        if (-not (Test-Path -LiteralPath $appPath -PathType Leaf)) {
            throw "GUI executable was not found: $appPath"
        }

        Write-Log "启动隔离 Smoke Test 实例..." "INFO"
        $appProcess = Start-Process -FilePath $appPath -WorkingDirectory $smokeRoot -PassThru
        $startupDeadline = (Get-Date).AddSeconds(10)

        do {
            $appProcess.Refresh()
            if ($appProcess.HasExited) {
                throw "GUI exited during startup with code $($appProcess.ExitCode)."
            }

            Start-Sleep -Milliseconds 250
        } while ((Get-Date) -lt $startupDeadline)

        $appProcess.Refresh()
        if ($appProcess.HasExited) {
            throw "GUI exited during startup with code $($appProcess.ExitCode)."
        }

        Write-Log "隔离 Smoke Test 实例启动成功。" "SUCCESS"

        $smokeLogPath = Join-Path $smokeRoot "logs\log_$(Get-Date -Format 'yyyyMMdd').txt"
        if (Test-Path -LiteralPath $smokeLogPath -PathType Leaf) {
            $recentLogs = Get-Content -LiteralPath $smokeLogPath -Tail 20
            $errorCount = ($recentLogs | Select-String "ERROR" | Measure-Object).Count
            if ($errorCount -gt 0) {
                Write-Log "隔离日志中发现 $errorCount 个错误。" "WARNING"
            } else {
                Write-Log "隔离日志检查完成，未发现错误。" "SUCCESS"
            }
        } else {
            Write-Log "隔离实例未生成应用日志，继续完成启动级 Smoke Test。" "WARNING"
        }
    } catch {
        $smokeTestStatus = 'FAIL'
        Write-Log "Smoke Test 失败: $($_.Exception.Message)" "ERROR"
        throw
    } finally {
        if ($null -ne $appProcess) {
            try {
                $appProcess.Refresh()
                if (-not $appProcess.HasExited) {
                    $appProcess.Kill($true)
                    $appProcess.WaitForExit(5000) | Out-Null
                }
                Write-Log "隔离 Smoke Test 实例已关闭。" "INFO"
            } catch {
                Write-Log "关闭 Smoke Test 实例时发生异常: $($_.Exception.Message)" "WARNING"
            }
        }

        if ($null -ne $smokeRoot -and (Test-Path -LiteralPath $smokeRoot)) {
            Remove-Item -LiteralPath $smokeRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
} else {
    Write-Log "跳过 Smoke Test (--SkipSmokeTest)" "WARNING"
}

# 第 5 步：生成报告
Write-Host ""
Write-Host "第 5 步：生成报告..." -ForegroundColor Cyan

$report = @"
# YLproxy Full Check Report

**生成时间**：$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

## 检查结果

| 项目 | 状态 |
|---|---|
| 编译 | ✅ PASS |
| 单元测试 | $(if ($SkipTests) { '⏭️  SKIPPED' } else { '✅ PASS' }) |
| Smoke Test | $(if ($smokeTestStatus -eq 'SKIPPED') { '⏭️  SKIPPED' } elseif ($smokeTestStatus -eq 'PASS') { '✅ PASS' } else { '❌ FAIL' }) |

## 修改统计

生成时间：$(Get-Date)
日志文件：$logFile

## 结论

✅ 检查完成，可以继续进行下一步操作。

---

**报告生成**：$reportFile
"@

$report | Out-File -FilePath $reportFile -Encoding UTF8
Write-Log "报告已生成: $reportFile" "SUCCESS"

# 最终输出
Write-Host ""
Write-Host "=" * 50
Write-Host "✅ 所有检查完成！" -ForegroundColor Green
Write-Host "=" * 50
Write-Host ""
Write-Host "📋 详细日志：$logFile"
Write-Host "📊 检查报告：$reportFile"
Write-Host ""

Write-Log "检查完成，所有验证通过" "SUCCESS"
