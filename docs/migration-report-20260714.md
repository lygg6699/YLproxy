# YLproxy 迁移报告（2026-07-14）

> 本报告由迁移流程自动生成（本次为初始占位），后续会在每个阶段完成后追加内容。

## 进度
- [x] 目录迁移与清理
- [x] 配置集中化到 AppSettings.json
- [x] 日志系统规范化
- [ ] 异常处理统一（不属于本次配置路径修复范围）
- [x] 编译校验

## 变更汇总
- 修改过的文件：PathResolver、Core ConfigService、Infrastructure ConfigService/Logger、GUI 配置调用、Proxy 运行时配置、项目文件和文档
- 替换过的函数/调用点：相对路径统一解析；日志、端口范围、监控间隔、3proxy 运行目录与 DLL 列表统一接入 AppSettings
- 删除的旧内容：未实现的 Application 配置节，以及 `src/YLproxy.GUI` 下错误的 data/logs 运行副本

