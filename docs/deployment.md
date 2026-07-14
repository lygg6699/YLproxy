# 部署记录

## Phase 2 — MVVM 静态 GUI 基础结构

**部署时间：** 2026-07-13 12:09

**部署环境：** Windows 10, .NET 10.0

**构建命令：** `dotnet build YLXCX.slnx`

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