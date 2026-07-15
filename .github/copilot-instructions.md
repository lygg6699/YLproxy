# YLproxy Copilot 执行指引

> AI 代理在 YLproxy 项目中的唯一规范源和执行约束

在本项目中，**所有 AI 代理操作前必须先读取并严格遵循** `/.agent` **文件中的规则**。

---

## 🎯 三层规范体系（按优先级）

### 🔴 第 1 层：`.agent` - **AI 唯一执行规则** ← **最高优先级**

**位置**：`/.agent`  
**用途**：AI 代理在本项目中的唯一决策框架  
**内容**：
- AI 代理的核心角色定位
- 5 个不可违反的核心约束
- 10 步执行流程（必须按序，不可跳步）
- 强制目录树和文件放置规则
- 任务完成输出标准
- 文档导航

**必读部分**：
1. 📍 核心角色定位 - 了解 AI 在项目中的职责
2. 🚫 核心约束 - 特别是 Constraint 1（10 步流程）
3. 📂 目录树规则 - 了解文件应该放在哪里
4. ✅ 输出标准 - 了解任务完成后如何报告

---

### 🟡 第 2 层：`.guard/` - **通用开发守护协议** 

**位置**：`/.guard/`  
**用途**：提供通用的开发方法论（可复用到其他项目）  
**包含文件**：

| 文件 | 用途 | 何时参考 |
|---|---|---|
| `guard.md` | 开发守护原则和四层验证系统 | 制定开发约束时 |
| `workflow.md` | 10 步标准开发流程详解 | 执行任何开发任务 |
| `coding-rules.md` | C# 编码规范（通用） | 审查代码质量 |
| `test-rules.md` | 测试规范和 Smoke Test 框架 | 验证功能 |
| `review-rules.md` | 任务完成前的自检清单 | 任务完成前检查 |
| `report-template.md` | 标准报告格式 | 生成任务报告 |
| `README.md` | `.guard/` 的通用性说明 | 了解复用规则 |

**参考方式**：
- 执行开发任务时，参考 `workflow.md` 的 10 步流程
- 检查代码质量时，参考 `coding-rules.md` 和 `review-rules.md`
- 验证功能时，参考 `test-rules.md`
- 生成报告时，参考 `report-template.md`

---

### 🟢 第 3 层：`docs/` - **项目执行手册**

**位置**：`/docs/`  
**用途**：YLproxy 项目特定的执行规则和任务追踪  
**关键文件**：

| 文件 | 用途 |
|---|---|
| `README.md` | 项目执行指南 + 任务完成后的文档更新规则 |
| `progress.md` | 项目进度追踪（**任务完成后必更新**） |
| `task-tracking.md` | 待办任务管理（**任务完成后必更新**） |
| `deployment.md` | 部署变更记录（**完成部署任务后必更新**） |
| `changelog.md` | 版本变更历史（**发布新版本时必更新**） |
| `development-deployment-outline/` | 功能文档（Phase A-E） |

**关键规则**：参见 `docs/README.md` 中的"任务执行与文档同步规则"

---

## ⚡ AI 代理执行清单

### 开始任何任务前

```
□ 1. 阅读 .agent 第一遍，了解项目规则
□ 2. 检查 docs/progress.md - 了解当前阶段
□ 3. 检查 docs/task-tracking.md - 了解待办任务
□ 4. 分析当前任务的影响范围和风险
□ 5. 参考 .guard/workflow.md 制定 10 步方案
□ 6. 等待用户确认 "开始执行"
```

### 执行任务中

```
□ 1. 按照 .agent 的 10 步流程逐步执行
□ 2. 参考 .guard/coding-rules.md 保证代码质量
□ 3. 参考 .guard/test-rules.md 验证功能
□ 4. 参考 .guard/review-rules.md 自检
□ 5. 按照 .guard/report-template.md 格式输出报告
```

### 任务完成后

```
□ 1. 更新 docs/progress.md（标记完成进度）
□ 2. 更新 docs/task-tracking.md（移到已完成）
□ 3. 更新 docs/deployment.md（如果涉及部署）
□ 4. 更新 docs/changelog.md（如果是新功能或修复）
□ 5. 输出标准报告（见 .agent 中的输出标准）
```

---

## 🚀 快速参考

### 编译和测试

```powershell
# 完整验证（编译 + 测试）
dotnet clean
dotnet build
dotnet test

# 或使用自动化脚本（如果存在）
./scripts/full-check.ps1
```

### 禁止事项

```
❌ 编译有警告或失败就标记完成
❌ 跳过单元测试
❌ 不更新任何文档
❌ 硬编码配置或路径
❌ 空的异常处理块
❌ 未授权状态下修改文件
```

---

## 📚 完整文档导航

**必读文件**（所有代理都应该读）：
1. `/.agent` - AI 唯一执行规则
2. `/docs/README.md` - 项目执行指南

**参考文件**（根据任务类型参考）：
- `/.guard/workflow.md` - 任何开发任务
- `/.guard/coding-rules.md` - 代码审查
- `/.guard/test-rules.md` - 功能验证
- `/.guard/review-rules.md` - 任务完成检查
- `/.guard/report-template.md` - 输出报告

**追踪文件**（了解项目状态）：
- `/docs/progress.md` - 项目进度
- `/docs/task-tracking.md` - 待办任务
- `/docs/deployment.md` - 部署历史
- `/docs/changelog.md` - 版本历史

---

## 🎯 核心原则

| 原则 | 说明 |
|---|---|
| **质量第一** | 所有构建、测试、验证必须 100% 通过 |
| **文档同步** | 代码改了，文档必须相应更新 |
| **追踪透明** | 所有完成的工作都应该记录在 docs/ 中 |
| **规范遵循** | 严格遵循 `.agent` 中的 5 个核心约束 |
| **授权执行** | 在未授权状态下只能分析和建议，不能修改 |

---

## 🔗 示例工作流

### 场景：新增一个功能

1. **需求理解** → 查看 `/docs/task-tracking.md` 了解任务
2. **方案制定** → 参考 `/.guard/workflow.md` 制定 10 步方案
3. **获得授权** → 等待用户确认"开始执行"
4. **执行开发** → 按 10 步逐步执行，参考 `/.guard/coding-rules.md`
5. **验证功能** → 参考 `/.guard/test-rules.md`，运行 `dotnet test`
6. **自检** → 参考 `/.guard/review-rules.md` 检查所有项
7. **更新文档** → 按 `/docs/README.md` 的映射更新相关文档
8. **输出报告** → 参考 `/.guard/report-template.md` 格式输出

---

## ⚠️ 常见错误

**❌ 错误 1**: 参考旧的 `AGENTS.md` 或 `.instructions.md`  
✅ 正确做法：这些文件已删除，改为参考 `/.agent`

**❌ 错误 2**: 任务完成后不更新 docs/ 中的文档  
✅ 正确做法：参考 `/docs/README.md` 的映射规则更新相关文件

**❌ 错误 3**: 在修改代码前进行冗长的分析  
✅ 正确做法：快速分析后立即等待用户授权，不要自行推进

**❌ 错误 4**: 使用项目特定的代码示例参考 `.guard/` 中的规范  
✅ 正确做法：`.guard/` 中的示例是通用的，需要映射到项目实际情况

---

## 🎓 对不同角色的指引

### 👤 新 AI 代理首次接触项目

1. 阅读 `/.agent` （必读）
2. 阅读 `/docs/README.md` （必读）
3. 查看 `/docs/progress.md` 了解当前进度
4. 查看 `/docs/task-tracking.md` 了解待办任务
5. 等待分配具体任务

### 👨‍💻 新开发者

1. 阅读 `/docs/README.md` 的"新开发者快速上手"部分
2. 按照 `/docs/development-deployment-outline/00-快速开始.md` 配置环境
3. 参考 `/docs/development-deployment-outline/05-开发指南/` 中的开发指南

### 🚀 发布工程师

1. 查看 `/docs/deployment.md` 了解历史部署
2. 查看 `/docs/changelog.md` 了解版本历史
3. 参考 `/docs/development-deployment-outline/02-发布计划.md` 的版本策略

---

**最后更新**：2026-07-15  
**版本**：V1.0  
**维护者**：YLproxy 团队
