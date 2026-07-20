# TODO: Cloud Group5 执行进度追踪

> 开始时间：2026-07-21
> 授权来源：用户明确授权"开始执行"

## Step 0: Git 基线清理
- [x] git checkout main && git pull origin main
- [x] 删除远程 fix/cloud-group5 分支
- [x] 确认当前基线: d112f52 refactor(P0-6): ProxyProcessManager

## Step 1: P1-1 + P1-7 配置类迁移到 Models + 常量化
- [ ] 创建 ConfigDefaults.cs
- [ ] 创建 6 个配置类文件 (AppSettingsConfig, LoggingConfig, ProxyConfig, ThreeProxyConfig, StartupConfig, ApiConfig)
- [ ] 更新 AppSettingsService.cs (删除配置类定义、替换魔术字符串、添加using)
- [ ] 更新 LoggerFactory.cs
- [ ] 更新 IAppSettingsService.cs
- [ ] 更新 ProxyProcessManager.cs, ProxyProcessManagerAdapter.cs
- [ ] 更新 ProxyRuntimeConfiguration.cs
- [ ] 更新 PreFlightChecker.cs
- [ ] 更新 ApiEndpoints.cs, ApiServer.cs
- [ ] 更新 App.xaml.cs, MainViewModel.cs
- [ ] 更新 5 个测试文件
- [ ] 删除 src/YLproxy.Proxy/Abstractions/ThreeProxyConfig.cs
- [ ] 验证: dotnet build

## Step 2: P0-7 ApiEndpoints 重复 try-catch
- [ ] 添加 SafeExecute/SafeExecuteAsync 辅助方法
- [ ] 替换 10 个端点的 try-catch 块
- [ ] 验证: dotnet build

## Step 3: P1 剩余 4 项
- [ ] P1-2: AppSettingsService 强类型方法
- [ ] P1-5: MonitorService MonitorTick 优化
- [ ] P1-6: ApiServer + ApiAuthMiddleware Swagger 生产开关
- [ ] P1-9: 审计日志 (来源IP + User-Agent)
- [ ] 验证: dotnet build

## Step 4: P1-10 测试补齐
- [ ] MonitorService 退避算法边界测试
- [ ] ConfigGenerator 参数验证测试
- [ ] FileLogger 日志轮转测试
- [ ] ManagedProxyForwarder CONNECT 407 测试
- [ ] ProxyDataService 恢复逻辑测试
- [ ] 验证: dotnet test

## Step 5: P2 优化性债务 (8 项)
- [ ] P2-2: ProxyDataSerializer Async 方法
- [ ] P2-3: IsRunning 移除 Ensure3ProxyDependencies
- [ ] P2-5: ManagedProxyForwarder 流式读取
- [ ] P2-7: Core 对 Proxy 接口化
- [ ] P2-8: data/README.md 同步
- [ ] P2-9: AppSettings.example.json 同步
- [ ] P2-10: global.json + Directory.Build.props 锁定版本
- [ ] P2-12: 统一路径解析策略
- [ ] 验证: dotnet build

## Step 6: P3 风险性债务 (7 项)
- [ ] P3-1: ManagedProxyForwarder SemaphoreSlim 并发限制
- [ ] P3-2: CONNECT 响应精确解析 HTTP 状态码
- [ ] P3-3: ProxyProcessManager Kill 后等待确认
- [ ] P3-4: AppSettingsService 放宽日志目录强约束
- [ ] P3-5: FileLogger lock -> ReaderWriterLockSlim
- [ ] P3-7: ProxyProcessManager 非 Windows 平台兼容
- [ ] P3-8: ManagedProxyForwarder Stop 协同修复
- [ ] 验证: dotnet build && dotnet test

## Step 7: Git 提交与推送
- [ ] 提交 1: 配置类迁移 + 常量
- [ ] 提交 2: P0-7 + P1 剩余
- [ ] 提交 3: 测试 + P2 + P3
- [ ] 推送到远程 main

## Step 8: 验证
- [ ] dotnet build && dotnet test

