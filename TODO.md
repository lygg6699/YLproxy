# 任务 TODO：目录迁移 + 统一配置/日志/异常

- [x] 读取并梳理当前解决方案与 csproj 引用关系（已做：部分读取）
- [ ] 目录迁移：重构为指定结构（src/YLproxy.{GUI,Core,Proxy,Models,Utils,Infrastructure} + runtime/3proxy + data/logs/docs/tests/build），并删除旧的不在结构中的目录与文件
- [ ] 修复/更新 .csproj 中的 ProjectReference 路径与任何相对路径
- [x] 配置文件统一：AppSettings.json 为唯一全局配置源，data/config.json 为唯一代理数据源；已完成路径校验与 C# 配置服务归一
- [ ] 替换硬编码配置：日志目录、代理目录、端口范围、检测时间、主题、自动启动、更新地址等
- [ ] 日志系统规范：全模块统一使用 ILogger；按天生成日志并自动清理过期日志
- [ ] 删除旧日志实现/替换调用点
- [ ] 异常处理统一：
  - [ ] 捕获异常 → 写入日志 → UI 提示
  - [ ] 禁止空 catch；修复不合规的 try/catch
- [ ] 迁移报告：输出修改过的文件、替换过的函数、删除的内容
- [ ] 编译与运行校验：GUI/Core/Tests 可编译（至少 build 通过）

