# runtime/3proxy/

## 目录简介

3proxy 运行环境，包含 3proxy 可执行文件、依赖库和配置模板。这是代理转换功能的核心组件，负责实际的网络流量转发和认证处理。

## 主要子目录说明

### bin64/
3proxy 可执行文件和 DLL：
- `3proxy.exe`：主可执行文件，实现代理功能
- `3proxy_crypt.exe`：密码加密工具（当前未使用）
- `FilePlugin.dll`：必需的插件，提供文件操作功能
- `StringsPlugin.dll`：必需的插件，提供字符串处理功能
- `TrafficPlugin.dll`：可选插件，提供流量统计功能
- `utf8tocp1251.dll`：编码转换插件
- `WindowsAuthentication.dll`：Windows 认证插件

### cfg/
配置模板：
- `3proxy.cfg.sample`：详细的 3proxy 配置示例和说明文档
- 包含丰富的注释说明各种配置选项的用途
- 0.scenario.txt, 1.cfg, 2.cfg, counters.sample：测试和示例配置
- sql/ 子目录：包含 SQL 相关的配置示例

## 使用说明

3proxy 运行环境使用方式：
- 应用程序通过 ProxyProcessManager 定位并启动此目录中的 3proxy.exe
- 配置文件由 ConfigGenerator 动态生成并放置在 cfg/ 子目录中
- 日志文件写入到 logs/ 子目录（相对于 3proxy 运行目录）
- 所有必要的依赖 DLL 必须存在于 bin64/ 目录中才能正常启动

## 注意事项

⚠️ **路径必须正确**：
- 这是目前项目的**主要阻塞问题**
- ProxyProcessManager.GetRuntime3ProxyPath() 方法中的路径计算错误
- 导致应用程序无法找到 3proxy.exe 而显示「3proxy.exe not found」错误
- 必须修复路径解析逻辑才能使代理功能正常工作

⚠️ **依赖完整性**：
- 启动时会检查必要的 DLL 依赖：FilePlugin.dll 和 StringsPlugin.dll
- 缺少这些依赖会导致启动失败，请确保此目录完整
- 建议保持此目录与官方 3proxy 发行版一致

⚠️ **工作目录要求**：
- 3proxy 必须在其自身目录中启动才能正确解析相对路径
- ProxyProcessManager 正确设置了 WorkingDirectory 来满足此要求
- 但前提是路径解析必须首先正确

## 后续计划（可选）

- 考虑从官方渠道定期更新 3proxy 版本以获得安全修复和新功能
- 添加版本检查机制以确保使用兼容的 3proxy 版本
- 考虑打包额外有用的插件（如 TrafficPlugin 用于流量监控）
- 添加自定义配置模板以优化特定使用场景的性能