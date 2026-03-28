# AI 助手协作指南

> 适用于 Codex、Kiro、Cursor 及其他 AI 助手。
> 完整项目上下文在 `CLAUDE.md`（同目录），请先阅读。

## 新会话快速上手

1. **阅读 `CLAUDE.md`** — 项目概述、技术栈、目录结构、当前进度
2. **阅读 `docs/superpowers/plans/2026-03-28-gb28181-platform.md`** — 17 个 Task 的完整实施计划（含所有代码）
3. **执行 `git log --oneline -10`** — 查看哪些 Task 已通过 commit 完成
4. **执行 `git status`** — 检查是否有上个会话未完成的改动，如有则继续完成
5. **从下一个未完成的 Task 开始执行**，严格按计划实现

## Token 管理

- **开始新 Task 前先评估剩余 token 是否够用**（含实现 + 编译验证 + commit）
- 如果不够，**不要开始**，提示用户："剩余 token 不足以完成 Task N，建议新开会话继续"
- 原则：要么完整做完一个 Task，要么不做。避免半成品交给下一个会话

## 每个 Task 执行清单

- [ ] 从计划文件中阅读对应 Task 的完整内容
- [ ] 按计划中的代码精确实现（代码已提供，不要自由发挥）
- [ ] 后端任务执行 `dotnet build GB28181Platform.sln` 验证编译通过
- [ ] 使用计划中指定的 commit message 提交
- [ ] 继续下一个 Task

## 工作目录

`d:/Project/RailTransit/轨道交通_智慧城市/SPI/02 源码/branches/fengjianhui/VMS/Platform/newvmsplat`

## 解决方案文件

`GB28181Platform.sln`（7 个项目 + 1 个待创建的测试项目）
