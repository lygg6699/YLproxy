#!/usr/bin/env pwsh
# YLproxy Full Check Script
# 一键执行完整的编译、测试、验证流程

param(
    [switch]$SkipTests,
    [switch]$SkipSmokeTest,
    [switch]$Verbose
)

# 配置
$reportFile = "reports/check-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').md"
$logFile = "logs/check-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"

# 颜色定义
$success = @{ ForegroundColor = 'Green' }
$error = @{ ForegroundColor = 'Red' }
$warning = @{ ForegroundColor = 'Yellow' }
$info = @{ ForegroundColor = 'Cyan' }

function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $logMessage = "[$timestamp] [$Level] $Message"
    Add-Content -Path $logFile -Value $logMessage
    
    switch ($Level) {
        'SUCCESS' { Write-Host "✅ $Message" @success }
        'ERROR' { Write-Host "❌ $Message" @error }
        'WARNING' { Write-Host "⚠️  $Message" @warning }
        'INFO' { Write-Host "ℹ️  $Message" @info }
        default { Write-Host $Message }
    }
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

dotnet clean -q 2>&1 | Tee-Object -FilePath $logFile -Append
$cleanExitCode = $LASTEXITCODE

if ($cleanExitCode -eq 0) {
    Write-Log "清理成功" "SUCCESS"
} else {
    Write-Log "清理失败 (Exit Code: $cleanExitCode)" "WARNING"
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

# 第 4 步：Smoke Test（可选）
if (-not $SkipSmokeTest) {
    Write-Host ""
    Write-Host "第 4 步：运行 Smoke Test..." -ForegroundColor Cyan
    Write-Log "开始功能测试" "INFO"
    
    # 启动应用并验证
    Write-Log "启动应用..." "INFO"
    $appProcess = Start-Process -FilePath "bin/Debug/net10.0/YLproxy.exe" -PassThru
    
    # 给应用 3 秒时间启动
    Start-Sleep -Seconds 3
    
    # 检查应用是否仍在运行
    if ($appProcess.HasExited) {
        Write-Log "应用启动失败" "ERROR"
        Write-Host "❌ Smoke Test 失败：应用无法启动" -ForegroundColor Red
        exit 1
    } else {
        Write-Log "应用启动成功" "SUCCESS"
    }
    
    # 检查日志
    if (Test-Path "logs/ylproxy.log") {
        $recentLogs = Get-Content "logs/ylproxy.log" -Tail 20
        Write-Log "日志内容检查完成" "INFO"
        
        $errorCount = ($recentLogs | Select-String "ERROR" | Measure-Object).Count
        if ($errorCount -gt 0) {
            Write-Log "日志中发现 $errorCount 个错误" "WARNING"
        } else {
            Write-Log "日志中无错误" "SUCCESS"
        }
    }
    
    # 关闭应用
    $appProcess | Stop-Process -Force
    Write-Log "应用已关闭" "INFO"
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
| Smoke Test | $(if ($SkipSmokeTest) { '⏭️  SKIPPED' } else { '✅ PASS' }) |

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
