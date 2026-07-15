# YLproxy.Proxy/

## 目录简介

与 3proxy 的集成层，负责生成 3proxy 配置文件和管理 3proxy 进程生命周期。此层将代理模型转换为实际的 3proxy 执行命令。

## 主要文件/子目录说明

### ConfigGenerator.cs
生成 3proxy 配置文件：
- 根据 ProxyItem 生成符合 3proxy 语法的配置文件内容
- 支持有认证和无认证两种代理类型
- 配置包括：服务模式、日志格式、访问控制、端口监听、`parent` HTTP 上游转发和 `fakeresolve`
- 生成的配置遵循 3proxy 最佳实践，包含适当的日志轮转和访问控制

### ProxyProcessManager.cs
管理 3proxy 进程：
- 负责 3proxy 进程的启动、停止和状态检查
- 确保所有必要的 3proxy 依赖文件存在（exe 和 DLL）
- 管理进程句柄以防止资源泄漏
- 提供线程安全的进程操作（使用 ConcurrentDictionary）
- 自动创建必要的配置和日志目录
- 启动失败、正常停止或检测到进程退出后删除含凭据的运行时 cfg

## 使用说明

此层由 Core 层和 GUI 层调用：
- ProxyTester 间接依赖（通过测试上游代理可用性）
- Core 层的 MonitorService 调用 IsRunning() 检查进程状态
- GUI 层的 MainViewModel 调用 Start() 和 Stop() 来控制代理
- 配置生成和进程管理完全封装在此层内

## 注意事项

✅ **运行时路径约定**：
- `PathResolver` 以有效的 `YLproxy.sln` 所在目录为开发运行根目录
- 发布目录没有解决方案文件时，以应用程序基目录为根目录
- `ThreeProxy.RuntimeDirectory` 默认指向 `runtime/3proxy`
- 3proxy 主程序和 DLL 必须位于 `runtime/3proxy/bin64/`

⚠️ **依赖文件检查**：
- 启动时会检查必要的 DLL 依赖：FilePlugin.dll 和 StringsPlugin.dll
- 缺少这些依赖会导致启动失败，请确保 `runtime/3proxy/bin64/` 目录完整

⚠️ **工作目录要求**：
- 3proxy 必须在其自身目录中启动才能正确解析相对路径配置
- ProxyProcessManager 正确设置了 WorkingDirectory 来满足此要求

⚠️ **敏感配置生命周期**：
- `data/config.json` 中的用户名和密码使用 DPAPI 加密；3proxy 运行期间 cfg 可能短暂包含明文凭据
- cfg 只用于进程启动和运行，生命周期结束后由 ProxyProcessManager 删除
- 不应手动保留或提交 `runtime/3proxy/cfg/` 下的生成配置

## 后续计划（可选）

- 添加更详细的版本信息和兼容性检查
- 考虑支持 3proxy 的热重载配置功能
- 添加更全面的依赖验证（包括所有可能需要的 DLL）
- 实现 3proxy 进程的优雅关闭尝试（ ennen 使用 Kill）
