# YLproxy 文档中心

> YLproxy 项目文档统一入口 | 最后更新：2026-07-21

---

## 📂 文档导航

### 🔴 待执行

当前需要执行的任务和计划

- **[债务清偿执行方案](pending/debt-remediation-execution-plan-20260719.md)**
  - Phase C1: P0 阻塞性债务清偿 (7-11h)
  - Phase C2: P1 重要性债务清偿 (30h)
  - Phase C3: P2 优化性债务清偿 (46.5h)
  - 预计总工时：90h (约11周)

- **[任务追踪](pending/task-tracking.md)**
  - Phase A3/A4 已完成
  - Phase B1/B2/B5 已完成
  - 待执行任务清单

- **[验收清单](pending/acceptance-checklist.md)**
  - 外部代理供应商端到端验收
  - 预检和验收场景

- **[发布计划](pending/02-发布计划.md)**
  - 版本策略
  - 发布流程
  - 版本规划

- **[TODO 清单](pending/TODO.md)**
  - Phase 历史任务记录
  - 已完成和进行中的任务

### 🟡 待完善

需要进一步完善的领域和规划

- **[架构设计分析](incomplete/architecture-analysis-report-20260716.md)**
  - 与 Nginx Proxy Manager / Caddy Proxy Manager 对比
  - 架构优化方案
  - P0-P3 优先级矩阵

- **[SQLite 架构设计](incomplete/sqlite-schema-design.md)**
  - 数据库表设计
  - 迁移策略
  - 待决策：JSON vs SQLite

- **[开发路线图](incomplete/01-开发路线图.md)**
  - Phase P1-P4 规划
  - REST API 预留
  - 长期架构演进

- **[架构设计文档](incomplete/03-架构设计/)**
  - 详细架构设计文档
  - 技术选型说明

### 🟢 项目已部署

已完成的部署和发布

- **[部署摘要](deployed/deployment-summary-20260717.md)**
  - 部署历史记录
  - 版本发布状态

- **[部署文档](deployed/deployment.md)**
  - 部署流程
  - 环境配置
  - 回滚方案

- **[迁移报告](deployed/migration-report-20260714.md)**
  - 数据迁移记录
  - 配置变更历史

- **[DPAPI 迁移策略](deployed/dpapi-migration-strategy.md)**
  - 凭据加密方案
  - 迁移执行记录

- **[运维部署文档](deployed/06-运维部署/)**
  - 部署规范
  - 用户手册
  - 运维指南

### 🟠 项目问题

已知问题和缺陷

- **[完整债务分析](issues/comprehensive-debt-analysis-20260719.md)**
  - 工程债分析 (基础设施、配置管理、安全)
  - 技术债分析 (代码质量、架构、测试)
  - 债务优先级矩阵

- **[技术债偿还方案](issues/tech-debt-remediation-plan.md)**
  - Phase B1-B6 执行方案
  - 已完成：B1/B2/B5
  - 待执行：B3/B4/B6

### 🔵 风险点

风险评估和缓解措施

- **[日志策略](risks/logging-strategy.md)**
  - 日志级别规范
  - 日志轮转策略
  - 日志存储安全

- **[变更日志](risks/changelog.md)**
  - 版本变更历史
  - 破坏性变更记录
  - 升级风险评估

### 📚 开发文档

开发指南和参考文档

- **[项目执行指南](development/README.md)**
  - 快速导航
  - 任务执行规则
  - 文档同步规范

- **[进度记录](development/progress.md)**
  - Phase 完成状态
  - 构建测试结果
  - 里程碑追踪

- **[快速开始](development/00-快速开始.md)**
  - 环境准备
  - 项目构建
  - 快速启动

- **[开发指南](development/05-开发指南/)**
  - 环境配置
  - 开发规范
  - 调试配置

- **[核心功能文档](development/04-核心功能/)**
  - 功能说明
  - 使用指南
  - API 文档

- **[开发部署大纲](development/development-deployment-outline-README.md)**
  - 开发部署总览
  - 项目结构说明

---

## 📊 项目状态总览

### 当前版本
- **版本号**: v0.2.0 (Phase 7)
- **.NET 版本**: 10.0.200
- **构建状态**: ✅ 75/75 tests passed
- **发布状态**: 🟡 本地部署，待发布自动化

### 债务状态
- **P0 阻塞性**: 🔴 2 项 (7-11h)
- **P1 重要性**: 🟡 4 项 (30h)
- **P2 优化性**: 🟢 8 项 (46.5h)
- **总债务**: 14 项，预计 90h

### 完成进度
- **Phase A (DI + God Class 拆分)**: ✅ 100%
- **Phase B (技术债偿还)**: 🟡 40% (B1/B2/B5 完成)
- **Phase C (综合债务清偿)**: 🔴 0% (待启动)

### 风险等级
- **架构风险**: 🟢 低
- **安全风险**: 🟡 中 (空 catch 块)
- **性能风险**: 🟢 低
- **维护风险**: 🟡 中 (MainViewModel 职责过重)

---

## 🔍 快速查找

### 按角色查找

**AI 代理**
1. [/.agent](../.agent) - AI 执行规则
2. [项目执行指南](development/README.md) - 任务执行规范
3. [债务清偿执行方案](pending/debt-remediation-execution-plan-20260719.md) - 当前任务

**新开发者**
1. [快速开始](development/00-快速开始.md)
2. [开发路线图](incomplete/01-开发路线图.md)
3. [环境配置](development/05-开发指南/环境配置.md)

**项目经理**
1. [完整债务分析](issues/comprehensive-debt-analysis-20260719.md)
2. [进度记录](development/progress.md)
3. [任务追踪](pending/task-tracking.md)

**发布工程师**
1. [部署文档](deployed/deployment.md)
2. [变更日志](risks/changelog.md)
3. [债务清偿执行方案](pending/debt-remediation-execution-plan-20260719.md)

### 按主题查找

**架构设计**
- [架构分析报告](incomplete/architecture-analysis-report-20260716.md)
- [SQLite 架构设计](incomplete/sqlite-schema-design.md)

**技术债**
- [完整债务分析](issues/comprehensive-debt-analysis-20260719.md)
- [技术债偿还方案](issues/tech-debt-remediation-plan.md)
- [债务清偿执行方案](pending/debt-remediation-execution-plan-20260719.md)

**部署运维**
- [部署摘要](deployed/deployment-summary-20260717.md)
- [部署文档](deployed/deployment.md)
- [DPAPI 迁移策略](deployed/dpapi-migration-strategy.md)

**测试验收**
- [验收清单](pending/acceptance-checklist.md)
- [进度记录](development/progress.md)

---

## 📝 文档维护规范

### 文档更新规则
1. 任务完成后必须同步更新对应文档
2. 文档更新时更新"最后更新"日期
3. 重大变更需更新 INDEX.md
4. 过时文档及时归档或删除

### 文档分类原则
- **pending/**: 待执行的任务和计划
- **incomplete/**: 待完善的领域和规划
- **deployed/**: 已完成的部署和发布
- **issues/**: 已知问题和缺陷
- **risks/**: 风险评估和缓解措施
- **development/**: 开发指南和参考文档

### 文档命名规范
- 使用英文连字符分隔
- 包含日期标识重要版本
- 文件名描述清晰准确
- 避免使用中文文件名

---

## 📞 联系方式

- **项目仓库**: https://github.com/lygg6699/YLproxy
- **问题反馈**: GitHub Issues
- **技术讨论**: GitHub Discussions

---

*最后更新：2026-07-21 | 维护方：YLproxy 开发团队*

---

## 📝 文档完善记录

**2026-07-19 文档重组与完善:**
- ✅ 重组文档目录结构（pending/incomplete/deployed/issues/risks/development）
- ✅ 更新开发路线图（添加 Phase A/B/C 状态）
- ✅ 完善架构分析报告（添加完成状态）
- ✅ 完善 SQLite schema 设计（添加决策状态）
- ✅ 完善架构设计文档（添加实现状态）
- ✅ 创建文档中心入口（INDEX.md + README.md）
