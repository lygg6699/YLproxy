# TODO

## DevContainer 修复（Phase 1）
- [x] 1) 在 `.devcontainer/devcontainer.json` 的 `customizations.vscode.extensions` 中追加 `ms-vscode.test-adapter-converter`
- [x] 2) 在 `.devcontainer/post-create.sh` 中移除 `dotnet build` 的 `--no-restore` 参数，避免还原失败导致构建直接失败
- [x] 3) 端口转发与 ASP.NET 证书配置项：已确认 forwardPorts=[9100,9001]；DOTNET_GENERATE_ASPNET_CERTIFICATE=false

## 安全性优化（Phase 2）
- [x] remoteUser 已为 `vscode`（已核查：无需在 Codespaces 中以 root 运行）




