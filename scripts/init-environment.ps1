#!/usr/bin/env pwsh
# YLproxy Environment Initialization Script
# 一次性环境初始化：安装 Git hooks + 注册日志清理计划任务。
#
# 用法：
#   pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\init-environment.ps1
#
# 参数：
#   -InstallHooks       : 只安装 Git hooks（默认：true）
#   -InstallScheduledTask : 只注册计划任务（需要管理员权限）
#   -UninstallScheduledTask : 卸载计划任务（需要管理员权限）
#   -WhatIf             : 预览模式，不实际执行

param(
    [switch]$InstallHooks = $true,
    [switch]$InstallScheduledTask,
    [switch]$UninstallScheduledTask,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = $PSScriptRoot
$repositoryRoot = Split-Path -Parent $scriptRoot
$repoName = Split-Path -Leaf $repositoryRoot

$success = @{ ForegroundColor = 'Green' }
$errorStyle = @{ ForegroundColor = 'Red' }
$warning = @{ ForegroundColor = 'Yellow' }
$info = @{ ForegroundColor = 'Cyan' }

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " YLproxy 环境初始化" -ForegroundColor Cyan
Write-Host " 仓库: $repositoryRoot" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ============================================================
# 1. 安装 Git pre-commit 钩子
# ============================================================
if ($InstallHooks -or (-not $InstallScheduledTask -and -not $UninstallScheduledTask)) {
    Write-Host ">>> 步骤 1/2: 安装 Git pre-commit 钩子..." -ForegroundColor Cyan

    $hookSource = Join-Path $repositoryRoot '.githooks\pre-commit'
    $gitDir = Join-Path $repositoryRoot '.git'
    $hooksDir = Join-Path $gitDir 'hooks'
    $hookTarget = Join-Path $hooksDir 'pre-commit'

    # 检查 .git 目录是否存在
    if (-not (Test-Path -LiteralPath $gitDir -PathType Container)) {
        Write-Host "  ❌ 错误: 未找到 .git 目录。请在 Git 仓库根目录运行此脚本。" @errorStyle
        Write-Host "     当前路径: $repositoryRoot" @errorStyle
        exit 1
    }

    # 检查钩子源文件是否存在
    if (-not (Test-Path -LiteralPath $hookSource -PathType Leaf)) {
        Write-Host "  ❌ 错误: 未找到钩子模板文件: $hookSource" @errorStyle
        exit 1
    }

    # 确保 .git/hooks 目录存在
    if (-not (Test-Path -LiteralPath $hooksDir -PathType Container)) {
        Write-Host "  创建目录: $hooksDir" @info
        if (-not $WhatIf) {
            New-Item -ItemType Directory -Path $hooksDir -Force | Out-Null
        }
    }

    # 检查是否已安装
    if (Test-Path -LiteralPath $hookTarget -PathType Leaf) {
        $existingContent = Get-Content -LiteralPath $hookTarget -Raw
        $newContent = Get-Content -LiteralPath $hookSource -Raw
        if ($existingContent -eq $newContent) {
            Write-Host "  ✔ pre-commit 钩子已安装且为最新版本，跳过。" @success
        } else {
            Write-Host "  ⚠  发现已存在的 pre-commit 钩子（内容不同），将覆盖。" @warning
            if (-not $WhatIf) {
                Copy-Item -LiteralPath $hookSource -Destination $hookTarget -Force
                Write-Host "  ✔ pre-commit 钩子已更新。" @success
            }
        }
    } else {
        if (-not $WhatIf) {
            Copy-Item -LiteralPath $hookSource -Destination $hookTarget -Force
            Write-Host "  ✔ pre-commit 钩子已安装到: $hookTarget" @success
        } else {
            Write-Host "  🔍 [PREVIEW] 将安装 pre-commit 钩子到: $hookTarget" @info
        }
    }

    Write-Host ""
}

# ============================================================
# 2. 注册/卸载日志清理计划任务
# ============================================================
if ($InstallScheduledTask) {
    Write-Host ">>> 步骤 2/2: 注册日志清理计划任务..." -ForegroundColor Cyan
    Write-Host "   ⚠  需要管理员权限运行此步骤。" @warning
    Write-Host ""

    $taskName = "YLproxy Log Cleanup"
    $scriptPath = Join-Path $repositoryRoot 'scripts\cleanup-logs.ps1'
    $action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`""
    $trigger = New-ScheduledTaskTrigger -Daily -At "02:00"
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable

    # 检查计划任务是否已存在
    $existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    if ($existingTask) {
        Write-Host "  ⚠  计划任务 '$taskName' 已存在，将更新。" @warning
        if (-not $WhatIf) {
            Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
            Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -RunLevel Highest | Out-Null
            Write-Host "  ✔ 计划任务已更新: 每天 02:00 执行日志清理" @success
        }
    } else {
        if (-not $WhatIf) {
            Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -RunLevel Highest | Out-Null
            Write-Host "  ✔ 计划任务已创建: $taskName" @success
            Write-Host "    执行时间: 每天 02:00" @info
            Write-Host "    脚本路径: $scriptPath" @info
        } else {
            Write-Host "  🔍 [PREVIEW] 将创建计划任务: $taskName" @info
        }
    }

    Write-Host ""
}

# ============================================================
# 2b. 卸载计划任务
# ============================================================
if ($UninstallScheduledTask) {
    Write-Host ">>> 步骤: 卸载日志清理计划任务..." -ForegroundColor Cyan

    $taskName = "YLproxy Log Cleanup"
    $existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue

    if ($existingTask) {
        if (-not $WhatIf) {
            Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
            Write-Host "  ✔ 计划任务 '$taskName' 已卸载。" @success
        } else {
            Write-Host "  🔍 [PREVIEW] 将卸载计划任务: $taskName" @info
        }
    } else {
        Write-Host "  ℹ️  计划任务 '$taskName' 不存在，跳过。" @info
    }

    Write-Host ""
}

# ============================================================
# 3. 完成
# ============================================================
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " 环境初始化完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($InstallHooks -or (-not $InstallScheduledTask -and -not $UninstallScheduledTask)) {
    Write-Host "📌 Git pre-commit 钩子已激活" -ForegroundColor Green
    Write-Host "   当您执行 git commit 时，会自动检查是否有敏感文件被暂存。"
    Write-Host "   钩子源文件: .githooks/pre-commit（版本控制追踪）"
    Write-Host "   安装位置: .git/hooks/pre-commit（本地仅运行时可执行）"
    Write-Host ""
}

if ($InstallScheduledTask) {
    Write-Host "📌 日志清理计划任务已注册" -ForegroundColor Green
    Write-Host "   任务名: YLproxy Log Cleanup"
    Write-Host "   执行时间: 每天 02:00"
    Write-Host "   手动执行: pwsh .\scripts\cleanup-logs.ps1"
    Write-Host "   预览模式: pwsh .\scripts\cleanup-logs.ps1 -WhatIf"
    Write-Host ""
}

Write-Host "💡 建议将此初始化步骤添加到开发环境配置文档中。" -ForegroundColor Yellow
Write-Host ""

exit 0

