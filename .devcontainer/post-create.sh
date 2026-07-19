#!/usr/bin/env bash
# YLproxy Codespaces 容器创建后初始化脚本
# ---------------------------------------------------------------------------
# 目标：让 Codespace 一进入即可直接编译解决方案、运行单元测试。
# 注意：WPF GUI（net10.0-windows）仅能在 Windows 运行；3proxy 为 Windows x64
#      二进制，Linux 容器内不准备、不运行。
set -euo pipefail

echo "==> [YLproxy] dotnet 版本信息"
dotnet --info || true

# 与 CI 一致：准备隔离的测试数据文件（部分测试依赖 data/config.json 存在）。
echo "==> [YLproxy] 准备隔离测试数据 data/config.json"
mkdir -p data
if [ ! -f data/config.json ]; then
  cp data/config.example.json data/config.json
  echo "    已从 data/config.example.json 生成 data/config.json"
else
  echo "    data/config.json 已存在，跳过"
fi

# 编译整个解决方案（WPF 项目借助 EnableWindowsTargeting 在 Linux 上参与编译）。
echo "==> [YLproxy] 编译解决方案（Debug）"
dotnet build YLproxy.sln --configuration Debug

echo "==> [YLproxy] 初始化完成。常用命令："
echo "      dotnet build YLproxy.sln"
echo "      dotnet test tests/YLproxy.Tests.csproj --filter Category=Unit"
