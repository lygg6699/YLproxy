## 配置唯一性与 C# 配置归一

**部署时间：** 2026-07-15

**验证命令：** `dotnet build YLproxy.sln --no-restore`；`dotnet test tests/YLproxy.Tests.csproj --no-restore`

**修改内容：** 全局配置服务重命名为 `AppSettingsService`，根配置模型重命名为 `AppSettingsConfig`，删除未使用接口和更新入口，强制配置只能落在规范路径。

**保留项：** `data/config.json` 和 `runtime/3proxy/cfg` 未删除。
## 配置一致性治理

**部署时间：** 2026-07-15

**验证命令：** `dotnet build src/YLproxy.GUI/YLproxy.GUI.csproj --no-restore`；`dotnet test tests/YLproxy.Tests.csproj --no-restore`

**验证结果：** GUI 构建成功；测试 6 Passed, 0 Failed。

**修改内容：** 全局配置校验、统一日志配置读取、代理数据服务重命名、唯一数据路径约束和配置契约测试。

**保留项：** `data/config.json` 与现有 `runtime/3proxy/cfg` 均保留，未执行删除。
# 部署记录

## Phase 2 — MVVM 静态 GUI 基础结构

**部署时间：** 2026-07-13 12:09

**部署环境：** Windows 10, .NET 10.0

**构建命令：** `dotnet build YLproxy.sln`

**构建结果：** 成功（0 Error, 0 Warning）

**修改内容：**
- 新增 YLproxy.GUI/ViewModelBase.cs
- 新增 YLproxy.GUI/RelayCommand.cs
- 新增 YLproxy.GUI/MainViewModel.cs
- 新增 YLproxy.GUI/Views/MainView.xaml
- 新增 YLproxy.GUI/Views/MainView.xaml.cs
- 修改 YLproxy.GUI/MainWindow.xaml（嵌入 MainView）
- 修改 YLproxy.GUI/App.xaml（启动 MainWindow）
- 删除 temp_wpf_test/ 残留测试项目

## Phase 9 — 终端乱码修复与编码统一

**部署时间：** 2026-07-15 06:16

**部署环境：** Windows 10/11, .NET 10.0

**构建命令：** `dotnet build YLproxy.sln`

**测试命令：** `dotnet test tests/YLproxy.Tests.csproj`

**验证结果：**
- 构建成功（0 Error, 0 Warning）
- 测试通过（2 Passed, 0 Failed）

**修改内容：**
- 新增 `.editorconfig`：统一文本文件 UTF-8 编码策略
- 新增 `.vscode/settings.json`：固定编辑器编码与 dotnet 终端语言
- 更新 `.blackboxrules`：固定 dotnet 命令前置语言环境变量
- 修复 `.blackbox/tmp/shell_tool_0b6f357b97b3.log` 已乱码文本
- 更新 `README.md`：补充 Windows 终端乱码规避说明
