#!/usr/bin/env pwsh
# YLproxy Log Cleanup Script
# 日志轮转与自动清理机制，防止日志文件无限增长。
#
# 用法：
#   pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\cleanup-logs.ps1
#   pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\cleanup-logs.ps1 -WhatIf  # 预览模式
#
# 计划任务安装（管理员）：
#   pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\init-environment.ps1

param(
    [switch]$WhatIf,             # 预览模式：只显示将要删除的文件，不实际删除
    [int]$MaxAgeDays = 30,       # 应用日志最大保留天数
    [int]$MaxSizeMB = 100,       # 应用日志最大文件大小（MB）
    [int]$ProxyLogMaxAgeDays = 7 # 3proxy 日志最大保留天数
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

$scriptRoot = $PSScriptRoot
$repositoryRoot = Split-Path -Parent $scriptRoot
Set-Location -LiteralPath $repositoryRoot

$totalDeleted = 0
$totalFreedBytes = 0

function Write-Action {
    param([string]$Message, [string]$Color = 'White')
    if ($WhatIf) {
        Write-Host "🔍 [PREVIEW] $Message" -ForegroundColor Cyan
    } else {
        Write-Host "  ✔ $Message" -ForegroundColor $Color
    }
}

function Remove-FileIf {
    param(
        [Parameter(Mandatory)] [System.IO.FileInfo]$File,
        [string]$Reason,
        [scriptblock]$Condition
    )

    $shouldRemove = & $Condition
    if (-not $shouldRemove) { return $false }

    $sizeKB = [math]::Round($File.Length / 1KB, 1)
    Write-Action "删除: $($File.FullName) ($sizeKB KB) — $Reason" -Color 'Yellow'

    if (-not $WhatIf) {
        try {
            $size = $File.Length
            Remove-Item -LiteralPath $File.FullName -Force -ErrorAction Stop
            $script:totalDeleted++
            $script:totalFreedBytes += $size
        } catch {
            Write-Host "    ❌ 删除失败: $_" -ForegroundColor Red
        }
    }
    return $true
}

# ============================================================
# 清理应用日志 (logs/*.log)
# ============================================================
Write-Host "`n========== 应用日志清理 (logs/) ==========" -ForegroundColor Green
$logDir = Join-Path $repositoryRoot 'logs'

if (Test-Path -LiteralPath $logDir -PathType Container) {
    $logFiles = Get-ChildItem -Path $logDir -File -Filter '*.log'
    
    if ($logFiles.Count -eq 0) {
        Write-Host "  没有需要清理的日志文件。" -ForegroundColor Gray
    } else {
        Write-Host "  共发现 $($logFiles.Count) 个日志文件" -ForegroundColor Gray
        
        $cutoffDate = (Get-Date).AddDays(-$MaxAgeDays)
        $maxSizeBytes = $MaxSizeMB * 1MB

        foreach ($file in $logFiles) {
            # 规则1：清理过期日志（超过保留天数）
            if ($file.LastWriteTime -lt $cutoffDate) {
                Remove-FileIf -File $file -Reason "超过 $MaxAgeDays 天保留期限 (最后写入: $($file.LastWriteTime.ToString('yyyy-MM-dd')))" -Condition { $true }
                continue
            }

            # 规则2：清理过大日志（超过大小限制）
            if ($file.Length -gt $maxSizeBytes) {
                $sizeMB = [math]::Round($file.Length / 1MB, 1)
                Remove-FileIf -File $file -Reason "超过 $MaxSizeMB MB 大小限制 (实际: $sizeMB MB)" -Condition { $true }
            }
        }
    }
} else {
    Write-Host "  日志目录不存在，跳过。" -ForegroundColor Gray
}

# ============================================================
# 清理 3proxy 日志 (runtime/3proxy/logs/)
# ============================================================
Write-Host "`n========== 3proxy 日志清理 (runtime/3proxy/logs/) ==========" -ForegroundColor Green
$proxyLogDir = Join-Path $repositoryRoot 'runtime/3proxy/logs'

if (Test-Path -LiteralPath $proxyLogDir -PathType Container) {
    $proxyLogFiles = Get-ChildItem -Path $proxyLogDir -File
    
    if ($proxyLogFiles.Count -eq 0) {
        Write-Host "  没有需要清理的日志文件。" -ForegroundColor Gray
    } else {
        Write-Host "  共发现 $($proxyLogFiles.Count) 个文件" -ForegroundColor Gray
        $proxyCutoff = (Get-Date).AddDays(-$ProxyLogMaxAgeDays)

        foreach ($file in $proxyLogFiles) {
            if ($file.LastWriteTime -lt $proxyCutoff) {
                Remove-FileIf -File $file -Reason "超过 $ProxyLogMaxAgeDays 天保留期限" -Condition { $true }
            }
        }
    }
} else {
    Write-Host "  3proxy 日志目录不存在，跳过。" -ForegroundColor Gray
}

# ============================================================
# 清理空日志目录中过期的子文件夹（如按日期归档的目录）
# ============================================================
Write-Host "`n========== 空目录清理 ==========" -ForegroundColor Green
$dirsToCheck = @(
    (Join-Path $repositoryRoot 'logs'),
    (Join-Path $repositoryRoot 'runtime/3proxy/logs')
)

foreach ($dir in $dirsToCheck) {
    if (Test-Path -LiteralPath $dir -PathType Container) {
        $emptyDirs = Get-ChildItem -Path $dir -Directory | Where-Object { 
            (Get-ChildItem -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue).Count -eq 0 
        }
        foreach ($emptyDir in $emptyDirs) {
            Write-Action "删除空目录: $($emptyDir.FullName)" -Color 'Yellow'
            if (-not $WhatIf) {
                try {
                    Remove-Item -LiteralPath $emptyDir.FullName -Recurse -Force -ErrorAction Stop
                } catch {
                    Write-Host "    ❌ 删除失败: $_" -ForegroundColor Red
                }
            }
        }
    }
}

# ============================================================
# 汇总报告
# ============================================================
Write-Host "`n========== 清理报告 ==========" -ForegroundColor Green
$freedMB = [math]::Round($totalFreedBytes / 1MB, 2)
$freedKB = [math]::Round($totalFreedBytes / 1KB, 0)

if ($WhatIf) {
    Write-Host "🔍 预览模式 — 没有实际删除任何文件。" -ForegroundColor Cyan
    Write-Host "   将删除 $totalDeleted 个文件，释放约 $freedKB KB ($freedMB MB)" -ForegroundColor Cyan
    Write-Host "   移除 -WhatIf 参数执行实际清理。" -ForegroundColor Cyan
} else {
    if ($totalDeleted -gt 0) {
        Write-Host "✅ 清理完成！删除了 $totalDeleted 个文件，释放了 $freedKB KB ($freedMB MB) 磁盘空间。" -ForegroundColor Green
    } else {
        Write-Host "✅ 没有需要清理的文件。" -ForegroundColor Green
    }
}

Write-Host ""
exit 0

