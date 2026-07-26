# TODO - 阶段四：用户体验增强（90% → 95%）✅ 已完成

## 子阶段 4.1：系统托盘功能 ✅
- [x] 安装 Hardcodet.NotifyIcon.Wpf NuGet 包
- [x] 创建 TrayIconViewModel.cs
- [x] 更新 MainWindow.xaml — 添加 TaskbarIcon + 托盘右键菜单
- [x] 重构 MainWindow.xaml.cs — 使用 WPF TaskbarIcon + 最小化到托盘
- [x] 在 MainViewModel 中添加托盘状态更新
- [x] 创建 Assets/app.ico 和 Assets/app-running.ico 占位文件

## 子阶段 4.2：暗色主题支持 ✅
- [x] 创建 LightTheme.xaml 浅色主题
- [x] 创建 ThemeService.cs 主题管理服务
- [x] 更新 App.xaml — ThemeService 运行时加载主题
- [x] 在 MainViewModel 中添加 ToggleThemeCommand + ToggleTheme()
- [x] 在 MainView.xaml 中添加主题切换按钮

## 子阶段 4.3：代理分组功能 ✅
- [x] MainViewModel 中集成分组 (GroupViewModel)
- [x] 创建 GroupViewModel.cs
- [x] 集成分组管理功能 (ShowManageGroupsWindow, StartGroupProxies, StopGroupProxies)
- [x] MainView.xaml 添加分组过滤 ComboBox + 启动组/停止组按钮
- [x] 创建 ManageGroupsWindow + ManageGroupsViewModel
- [x] 添加按分组批量操作 (StartGroupCommand/StopGroupCommand)

## 编译验证 ✅
- [x] MainView.xaml 编译错误修复
- [x] 所有文件集成完成
- [x] 编译全部项目 — 0 errors, 0 warnings ✅

## 文档同步
- [ ] 更新 docs/progress.md
- [ ] 更新 docs/task-tracking.md
- [ ] 更新 docs/changelog.md
