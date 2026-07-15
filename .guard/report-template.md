# 开发任务标准报告模板（通用）

> 所有任务完成后输出的标准化报告格式

版本：V1.0  
生效日期：2026-07-15

---

## 📋 标准报告格式

### 报告头

```
==============================
Development Task Report
==============================

Report Date: 2026-07-15 14:30:00
Task ID: TASK-001
Reporter: [开发者或 AI 名称]
```

### 基本信息

```
Task:
[任务简要说明，1-2 行]
示例：优化数据处理服务性能，将处理耗时从 5s 降低到 3s

Scope:
[修改范围，简要说明]
示例：仅修改 ProxyTester.cs 的测试逻辑，其他模块无影响

Risk Level:
[低/中/高]
示例：低（仅修改内部实现）
```

### 验证结果

```
Build Status:
✅ PASS / ❌ FAIL

Unit Test Status:
✅ PASS (25/25 tests) / ❌ FAIL (23/25 tests, 2 failures)

Integration Test (Smoke Test):
✅ PASS / ❌ FAIL

Regression Test:
✅ PASS (未发现回归) / ⚠️ WARNING (发现 1 个潜在回归)
```

### 修改清单

```
Modified Files:
- ProxyTester.cs (性能优化：并行测试)
- test-rules.md (文档更新：补充性能指标)

New Files:
- None

Deleted Files:
- None

Configuration Changes:
- AppSettings.json: 新增 ParallelTestCount 配置 (默认=4)
```

### 代码质量

```
Code Quality Metrics:
- Compiler Warnings: 0 (无新增警告)
- Code Coverage: 85% (目标: ≥80%, 状态: ✅ 达标)
- Cyclomatic Complexity: 正常（无超复杂方法）

Coding Standards:
- Naming Conventions: ✅ 符合规范
- Exception Handling: ✅ 完整
- Logging: ✅ 适当
- Comments: ✅ 清晰
```

### 日志摘要

```
Key Logs:
[INFO] ProxyTester 已初始化
[DEBUG] 开始执行代理测试，目标: 美国代理-01
[DEBUG] 性能优化启用，使用 4 个并行线程
[DEBUG] 代理测试完成，耗时: 2.8s (原: 5.1s，优化: 45%)
[INFO] 性能优化成功

Error/Warning Summary:
- 0 个 ERROR 日志
- 0 个 WARNING 日志
```

### 性能指标（如适用）

```
Performance Metrics:
- 测试耗时：5.1s → 2.8s (优化 45%)
- 内存占用：稳定在 150MB
- CPU 使用率：< 30%
- 无明显性能下降
```

### 测试细节

```
Test Results:

Smoke Test (功能验证):
├─ ✅ 应用启动
├─ ✅ 配置加载
├─ ✅ 代理添加
├─ ✅ 代理测试 (新优化逻辑已验证)
├─ ✅ 代理启动
├─ ✅ 代理停止
└─ ✅ 应用退出

Unit Tests (回归验证):
├─ ✅ ProxyTesterTests (5/5)
├─ ✅ ProxyServiceTests (8/8)
├─ ✅ MainViewModelTests (12/12)
└─ ✅ 总计: 25/25

Regression Analysis:
└─ ✅ 未发现回归问题
```

### 风险分析

```
Risk Analysis:
- 直接影响：ProxyTester 类的性能
- 间接影响：UI 响应性能提升
- 回滚难度：低（仅逻辑变更，无数据格式变更）
- 监控项：性能指标、并行线程稳定性

Mitigations:
- 新增详细日志便于问题追踪
- 所有测试都通过确保功能正确
```

### 建议和注意事项

```
Recommendations:
- 生产环境建议逐步灰度发布
- 建议监控实际环境中的性能数据
- 如发现异常，可通过 AppSettings.json 禁用并行测试

Known Issues / Limitations:
- 当代理响应不稳定时，可能出现超时（已在异常处理中解决）
- 不建议将 ParallelTestCount 设置超过 CPU 核数
```

### 总结

```
Summary:
所有任务已完成，代码通过编译、单元测试、集成测试。
性能优化达到预期目标（45% 性能提升）。
无回归问题，可安全部署。

Action Items:
- [ ] 代码审查（可选）
- [ ] 部署到 beta 环境
- [x] 监控生产性能
```

### 结论

```
Status: ✅ READY FOR PRODUCTION

Approval:
- Code Review: ✅ Approved (by reviewer)
- QA Sign-off: ✅ Approved
- Release Checklist: ✅ All items checked

Next Steps:
1. 合并到 main 分支
2. 生成发布版本
3. 部署到生产环境
```

### 页脚

```
==============================
Report Generated: 2026-07-15 14:35:00
Report ID: RPT-001-20260715
Reporter: GitHub Copilot
Documentation: .guard/report-template.md
==============================
```

---

## 📝 简化版报告（快速验证）

如果时间紧张，可以使用简化版报告：

```
==============================
Quick Report
==============================

Task: 优化数据处理性能

Build: ✅ PASS
Tests: ✅ PASS (25/25)
Smoke: ✅ PASS
Regression: ✅ PASS

Modified:
- DataService.cs （示例）
- unit-tests.md （示例）

Risk: 低
Status: ✅ READY

==============================
```

---

## 🔍 报告检查清单

生成报告后，确认以下项目：

```
□ 报告标题清晰
□ 任务描述准确
□ 所有测试结果已包含
□ 修改文件清单完整
□ 没有拼写错误
□ 状态结论明确（PASS/FAIL/PARTIAL）
□ 建议或注意事项清晰
□ 报告日期和时间准确
```

---

## 📚 相关文档

- [guard.md](guard.md) - 开发守护协议
- [workflow.md](workflow.md) - 开发流程
- [review-rules.md](review-rules.md) - 自检规范

---

**最后更新**：2026-07-15
