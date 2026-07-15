# 📚 YLproxy 项目执行指南

> YLproxy 项目的实施手册、开发工作流、任务追踪与文档更新规则

**版本**：V1.0  
**最后更新**：2026-07-15  
**维护方**：YLproxy 开发团队

---

## 🚀 快速导航

### 👤 不同角色的入门指南

#### 🤖 AI 代理首次接触项目
1. 阅读 [`/.agent`](../.agent) - AI 唯一执行规则（5 分钟）
2. 阅读本文件（docs/README.md）- 项目执行指南（10 分钟）
3. 查看 [`progress.md`](progress.md) - 当前进度（3 分钟）
4. 查看 [`task-tracking.md`](task-tracking.md) - 待办任务（3 分钟）
5. 执行任何任务时参考 [`/.guard/workflow.md`](../.guard/workflow.md) 的 10 步流程

#### 👨‍💻 新开发者快速上手
1. 阅读 [`development-deployment-outline/00-快速开始.md`](development-deployment-outline/00-快速开始.md)（5 分钟）
2. 阅读 [`development-deployment-outline/01-开发路线图.md`](development-deployment-outline/01-开发路线图.md)（10 分钟）
3. 按照 [`development-deployment-outline/05-开发指南/环境配置.md`](development-deployment-outline/05-开发指南/环境配置.md) 配置环境（15 分钟）
4. 了解 [`/.guard/`](../.guard/) 中的通用开发规范

#### 👀 项目经理或架构师
1. 阅读 [`development-deployment-outline/项目主体核心概述.md`](development-deployment-outline/) - 项目定位和核心功能
2. 查看 [`development-deployment-outline/01-开发路线图.md`](development-deployment-outline/01-开发路线图.md) - 完整路线图和里程碑
3. 查看 [`deployment.md`](deployment.md) - 部署历史和变更
4. 查看 [`progress.md`](progress.md) - 实时进度

#### 🚀 发布工程师
1. 查看 [`development-deployment-outline/02-发布计划.md`](development-deployment-outline/02-发布计划.md) - 版本策略
2. 查看 [`changelog.md`](changelog.md) - 版本变更历史
3. 查看 [`deployment.md`](deployment.md) - 部署流程记录

---

## 📋 任务执行与文档同步规则

### ✅ 任务完成后必须同步的文件

当完成任何开发任务（新功能、Bug 修复、重构、文档更新）时，**必须**按照以下规则更新对应的文档文件：

#### 1️⃣ 任务开始前 → 检查这些文件

在开始任何任务前，必须阅读：

```
□ docs/progress.md          - 当前项目处于哪个阶段
□ docs/task-tracking.md     - 有哪些待办任务、优先级、依赖关系
□ docs/deployment.md        - 最近的部署变更是什么
□ docs/changelog.md         - 最近的版本变更记录
```

#### 2️⃣ 任务完成后 → 更新这些文件

**所有任务完成后都必须更新这三个追踪文件**（无一例外）：

##### 📝 **docs/progress.md** - 项目进度追踪

**何时更新**：任何任务完成（特别是 Phase 完成）

**更新内容**：
- 标记当前处于哪个 Phase 和哪个子任务
- 记录新完成的功能和工作项
- 更新百分比进度
- 标记任何阻挡因素

**示例**：
```markdown
## Phase X - [阶段名称]

**状态**：进行中 / 已完成

**完成时间**：YYYY-MM-DD HH:MM

### 完成内容
- ✅ 功能 1
- ✅ 功能 2

### 下一步
- ⏳ 功能 3（优先级：高）
```

##### 📝 **docs/task-tracking.md** - 待办任务管理

**何时更新**：
- 创建新任务时（添加到列表）
- 任务完成时（标记完成或移到历史）
- 任务优先级/依赖关系变化时

**更新内容**：
- 将完成的任务从"待办"移到"已完成"或按日期归档
- 更新优先级或依赖关系
- 添加新发现的任务

**示例**：
```markdown
### 待办任务

| 任务 | 优先级 | 依赖 | 负责人 | 状态 |
|---|---|---|---|---|
| 功能 A 开发 | 高 | - | 开发者 | 进行中 |
| Bug B 修复 | 中 | 功能 A | AI | ⏳ 待开始 |

### 已完成任务 (2026-07-15)

- ✅ 功能 C 完成
- ✅ 文档 D 更新
```

##### 📝 **docs/deployment.md** - 部署变更记录

**何时更新**：
- 完成功能开发并准备部署时
- 创建新版本时
- 执行部署时

**更新内容**：
- 新增或修改的功能
- 配置变更
- 数据库迁移
- 部署步骤

**示例**：
```markdown
## 部署版本 V1.0.1 (2026-07-15)

### 新增功能
- 代理自动重连机制

### 修复内容
- 修复端口泄漏 Bug
- 改进性能 20%

### 部署步骤
1. 备份 data/config.json
2. 更新 YLproxy.exe
3. 重启服务
```

##### 📝 **docs/changelog.md** - 版本变更历史（自动生成或手动维护）

**何时更新**：
- 发布新版本时（将部署记录转化为版本记录）

**更新内容**：
- 版本号
- 发布日期
- 主要变更（Features / Fixes / Docs）
- 已知问题

---

## 📚 完整文档导航

### Phase A - 快速入门与项目概述

| 文档 | 用途 | 受众 |
|---|---|---|
| [00-快速开始.md](development-deployment-outline/00-快速开始.md) | 5 分钟快速上手 | 所有人 |
| [01-开发路线图.md](development-deployment-outline/01-开发路线图.md) | 完整的开发计划和里程碑 | 项目管理、架构师 |
| [02-发布计划.md](development-deployment-outline/02-发布计划.md) | 版本发布策略 | 发布工程师 |

### Phase B - 架构设计

| 文档 | 用途 | 受众 |
|---|---|---|
| [03-架构设计/后端架构.md](development-deployment-outline/03-架构设计/后端架构.md) | 后端服务架构 | 后端开发 |
| [03-架构设计/前端架构.md](development-deployment-outline/03-架构设计/前端架构.md) | MVVM 前端架构 | 前端开发 |
| [03-架构设计/3proxy集成.md](development-deployment-outline/03-架构设计/3proxy集成.md) | 3proxy 代理引擎集成 | 系统集成 |
| [03-架构设计/数据存储.md](development-deployment-outline/03-架构设计/数据存储.md) | 数据模型和持久化 | 数据库架构师 |

### Phase C - 核心功能

| 文档 | 用途 | 受众 |
|---|---|---|
| [04-核心功能/代理转换原理.md](development-deployment-outline/04-核心功能/代理转换原理.md) | HTTP 代理协议和转换 | 核心开发 |
| [04-核心功能/端口管理策略.md](development-deployment-outline/04-核心功能/端口管理策略.md) | 端口分配和冲突处理 | 系统设计 |
| [04-核心功能/状态监控机制.md](development-deployment-outline/04-核心功能/状态监控机制.md) | 实时状态监控 | 系统监控 |
| [04-核心功能/安全加密方案.md](development-deployment-outline/04-核心功能/安全加密方案.md) | DPAPI 加密和密钥管理 | 安全工程师 |

### Phase D - 开发指南

| 文档 | 用途 | 受众 |
|---|---|---|
| [05-开发指南/环境配置.md](development-deployment-outline/05-开发指南/环境配置.md) | 开发环境设置 | 新开发者 |
| [05-开发指南/代码规范.md](development-deployment-outline/05-开发指南/代码规范.md) | 编码标准（项目特定） | 所有开发者 |
| [05-开发指南/测试策略.md](development-deployment-outline/05-开发指南/测试策略.md) | 测试计划和覆盖率 | 测试工程师 |
| [05-开发指南/调试技巧.md](development-deployment-outline/05-开发指南/调试技巧.md) | Visual Studio 调试 | 开发者 |

### Phase E - 部署与运维

| 文档 | 用途 | 受众 |
|---|---|---|
| [06-运维部署/安装包制作.md](development-deployment-outline/06-运维部署/安装包制作.md) | 打包和安装程序 | 发布工程师 |
| [06-运维部署/部署流程.md](development-deployment-outline/06-运维部署/部署流程.md) | 生产部署步骤 | 运维工程师 |
| [06-运维部署/更新策略.md](development-deployment-outline/06-运维部署/更新策略.md) | 版本更新机制 | 运维工程师 |

### 项目追踪文件

| 文件 | 内容 | 更新频率 |
|---|---|---|
| [progress.md](progress.md) | 项目阶段进度 | 每个任务完成时 |
| [task-tracking.md](task-tracking.md) | 待办任务列表 | 实时 |
| [deployment.md](deployment.md) | 部署变更历史 | 每次部署时 |
| [changelog.md](changelog.md) | 版本变更记录 | 每次发布时 |

---

## 🔗 与其他规范的关系

### 三层规范结构

```
🔴 第 1 层：.agent (AI 唯一执行规则)
   │
   ├─→ 指向本文件 (docs/README.md) 了解项目特定规则
   │
   └─→ 指向 .guard/ 了解通用开发方法论

🟡 第 2 层：.guard/ (通用开发守护协议)
   │
   └─→ 所有项目都可复用的规范

🟢 第 3 层：docs/ (项目特定执行手册)
   │
   ├─→ development-deployment-outline/ (功能文档)
   │
   └─→ 本文件 + 追踪文件 (执行和追踪)
```

### 文档关系

- **`.agent`** → 告诉 AI 代理应该遵循什么（指向下面两层）
- **`.guard/`** → 提供通用的方法论（如何做）
- **`docs/`** → 提供项目特定的规则（做什么 + 何时更新文档）

---

## 💡 常见问题

**Q: 我新增了一个功能，应该更新哪些文件？**  
A: 至少更新这三个：`progress.md`（标记完成）、`task-tracking.md`（移到已完成）、`changelog.md`（如果是新功能）。

**Q: 我应该何时读 `.guard/` 中的规范？**  
A: 执行任何开发任务时。`.guard/workflow.md` 定义了 10 步流程，其他文件定义了编码规范和测试标准。

**Q: 为什么任务完成后要更新这么多文档？**  
A: 这确保了项目进度的可追踪性。任何人查看这些文件都能立即了解项目状态、已完成内容、待办事项。

**Q: 哪些文件是我必须读的？**  
A: 所有人都应该读 `/.agent` 和本文件 (docs/README.md)。不同角色还需要读对应的功能文档。

---

## 🎯 下一步行动

### 如果你是开发者
→ 进入 [开发指南](development-deployment-outline/05-开发指南/) 准备开发环境

### 如果你是 AI 代理
→ 按 `.agent` 中的 10 步流程执行任何任务，完成后更新追踪文件

### 如果你是项目管理员
→ 定期查看 `progress.md` 和 `task-tracking.md` 了解项目状态

---

**相关文档**：
- [`.agent`](../.agent) - AI 执行规则
- [`.guard/README.md`](../.guard/README.md) - 通用规范说明
- [`progress.md`](progress.md) - 实时进度

**最后更新**：2026-07-15  
**下次审阅**：2026-08-15
