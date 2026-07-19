# deploy/

部署相关配置。当前仓库主体为 Windows WPF 桌面程序，暂无 Kubernetes 部署目标；
本目录提供 **kubeconfig 占位模板**，供未来将跨平台组件（如 `YLproxy.Api`）部署到
Kubernetes 集群时使用。

## 文件

| 文件 | 是否提交 | 说明 |
| --- | --- | --- |
| `kubeconfig.example.yaml` | 是 | 占位模板，**不含真实凭据**，仅结构与注释。 |
| `kubeconfig.yaml` | 否（已被 `.gitignore` 忽略） | 你本地填入真实值后的实际配置，含机密，禁止提交。 |

## 使用步骤

1. 复制模板：
   ```bash
   cp deploy/kubeconfig.example.yaml deploy/kubeconfig.yaml
   ```
2. 替换所有 `REPLACE_WITH_*` 占位符（API server 地址、CA、认证方式、namespace）。
3. 本地使用：
   ```bash
   export KUBECONFIG="$PWD/deploy/kubeconfig.yaml"
   kubectl config get-contexts
   ```

## 安全须知

- **绝不要提交** 含真实 token / 客户端密钥的 `kubeconfig.yaml`。
- 在 CI/CD 中，将整份 kubeconfig 存为 **secret**，运行时写入 `$HOME/.kube/config`
  或用 `KUBECONFIG` 指向临时文件；用完即删。
- 优先使用最小权限的 ServiceAccount（仅授予部署所需命名空间的权限）。
- 提醒：WPF GUI 与 Windows x64 3proxy 无法在 Linux 容器 / k8s 中运行，
  k8s 仅适用于未来的跨平台服务端组件。
