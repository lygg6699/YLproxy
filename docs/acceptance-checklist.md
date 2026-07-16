# YLproxy 外部代理供应商端到端验收清单

> 版本：P0 | 最后更新：2026-07-17
> 准备：至少 1 个可连接的外部 HTTP/HTTPS 代理账号

## 预检

- [ ] `dotnet build -c Release` → 0 Error, 0 Warning
- [ ] `dotnet test -c Release` → 36 Passed（3 预存 Forwarder 失败可忽略）
- [ ] `data/config.json` 存在且可读
- [ ] `runtime/3proxy/bin64/3proxy.exe` 存在
- [ ] AppSettings.json 中 `Api.AccessToken` 已更改为自定义值

## 验收场景

### 1. 添加外部代理（GUI）

- [ ] 启动 GUI：`dotnet run --project src/YLproxy.GUI -c Release`
- [ ] 点击"添加代理"，填写：
  - 名称：`验收测试-1`
  - 远程地址：外部代理 IP/域名
  - 远程端口：外部代理端口
  - 用户名/密码：外部代理认证凭据
- [ ] 添加成功，列表中出现新条目
- [ ] 密码列显示为 `****`（脱敏正确）

### 2. 代理测试

- [ ] 右键新代理 → "测试连接"
- [ ] 弹窗显示测试结果（成功/失败 + 延迟 ms）
- [ ] 测试结果正确反映网络实际可达性

### 3. 代理启动

- [ ] 选中代理 → 点击"启动"
- [ ] 状态变更为"Running"
- [ ] `runtime/3proxy/cfg/{id}.cfg` 文件生成
- [ ] `runtime/3proxy/logs/3proxy-{id}.log` 文件生成
- [ ] 本地端口 `{localPort}` 被监听：`netstat -an | findstr {localPort}`

### 4. 代理转发验证

- [ ] 使用 curl 通过本地代理访问外网：
  ```sh
  curl -x http://127.0.0.1:{localPort} http://www.baidu.com -v
  ```
- [ ] HTTP 响应码 200
- [ ] `-v` 输出中可以看到经由本地代理

### 5. 代理停止

- [ ] 选中运行中的代理 → 点击"停止"
- [ ] 状态变更为"Stopped"
- [ ] `runtime/3proxy/cfg/{id}.cfg` 文件**被删除**
- [ ] `netstat -an | findstr {localPort}` 端口已释放

### 6. 代理删除

- [ ] 停止代理后 → 删除
- [ ] 列表条目移除
- [ ] `data/config.json` 中该条目消失
- [ ] SQLite 数据库同步删除（如已迁移）

### 7. 凭据持久化验证

- [ ] 重启 GUI
- [ ] 之前的代理配置重新加载
- [ ] `data/config.json` 中凭据为 `dpapi:v1:` 加密格式（非明文）
- [ ] ProxyTester 测试仍能通过（解密成功）

### 8. 监控自动检测

- [ ] 手动 kill 3proxy 进程（模拟崩溃）
  ```sh
  taskkill /f /im 3proxy.exe /fi "USERNAME eq %USERNAME%"
  ```
- [ ] 5 秒内 MonitorService 检测到进程退出
- [ ] 状态变更为"Failed"
- [ ] 日志中有 `Monitor: proxy {id} ... process exited unexpectedly`

### 9. 日志凭据脱敏

- [ ] 检查 `logs/log_{date}.txt`：
  ```sh
  findstr "dpapi:v1:" logs\log_*.txt
  ```
- [ ] 不应出现 `dpapi:v1:` 字符串（已被 `[REDACTED]` 替代）

### 10. 异常场景

- [ ] 添加一个错误的代理凭据（不可达的远程端口）
- [ ] 测试连接 → 应返回"连接失败"而非崩溃
- [ ] 启动错误的代理 → 应正确处理启动失败

## 验收结果

| 场景 | 结果 | 备注 |
|---|---|---|
| 1. 添加外部代理 | ⬜ | |
| 2. 代理测试 | ⬜ | |
| 3. 代理启动 | ⬜ | |
| 4. 代理转发验证 | ⬜ | |
| 5. 代理停止 | ⬜ | |
| 6. 代理删除 | ⬜ | |
| 7. 凭据持久化验证 | ⬜ | |
| 8. 监控自动检测 | ⬜ | |
| 9. 日志凭据脱敏 | ⬜ | |
| 10. 异常场景 | ⬜ | |

## 签字

- 验收人：___________
- 日期：___________
- 外部代理供应商：___________
- 代理类型：HTTP / HTTPS / SOCKS5
