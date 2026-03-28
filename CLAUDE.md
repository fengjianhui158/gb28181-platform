# GB28181 智能视频管理平台 — AI 协作上下文

> 本文件供所有 AI 助手（Claude Code / Codex / Kiro / Cursor 等）自动读取，了解项目全貌和当前进度。

## 项目概述

GB28181 国标视频监控管理平台，面向轨道交通场景，管理 100-500 路摄像机。
核心功能：设备注册、视频预览、离线自动诊断、AI 智能问答。

## 技术栈

| 层级 | 技术 |
|------|------|
| 后端 | .NET 8 LTS, ASP.NET Core Web API |
| SIP 信令 | sipsorcery 10.0.3 (REGISTER/INVITE/心跳/Digest Auth) |
| 流媒体 | ZLMediaKit (C++ 媒体服务器, RTP→WebRTC/RTSP/FLV) |
| ORM | SqlSugarCore 5.1.4.214 + MySQL (CodeFirst) |
| 缓存 | Redis 5.0.14 (Windows x64) |
| AI | Qwen 3.5 (公司自建, OpenAI 兼容 API, Function Calling) |
| 浏览器自动化 | Playwright (Headless Chrome 截图 + Qwen 视觉分析) |
| 前端 | Vue 3 + TypeScript + Element Plus + 科技风暗色主题 |
| 实时推送 | SignalR |
| 日志 | Serilog (Console + File rolling) |

## 项目结构

```
src/
├── GB28181Platform.Domain/          # 实体 + 枚举（7个表: Device, Channel, DiagnosticTask, DiagnosticLog, AlarmRecord, AiConversation, User）
├── GB28181Platform.Application/     # 应用服务层（设备/流媒体 AppService）
├── GB28181Platform.Sip/             # SIP 服务器（REGISTER/心跳/认证/INVITE）
├── GB28181Platform.Diagnostic/      # 诊断引擎（IDiagnosticStep 流水线: Ping→Port→Browser）
├── GB28181Platform.AiAgent/         # AI Agent（Qwen Client + Function Calling + FunctionRegistry）
├── GB28181Platform.Infrastructure/  # 基础设施（DbContext, Redis, ZLMediaKit 客户端）
└── GB28181Platform.Api/             # Web API 入口 + BackgroundServices

frontend/vms-web/                    # Vue 3 SPA（4页面: 设备管理/实时预览/诊断面板/AI问答）

tests/GB28181Platform.Tests/         # xUnit + NSubstitute 单元测试（待创建）
```

## 关键设计文件

- **设计规格书**: `docs/superpowers/specs/2026-03-28-gb28181-platform-design.md`
- **实施计划**: `docs/superpowers/plans/2026-03-28-gb28181-platform.md` ← **17个Task的完整代码**
- **头脑风暴记录**: `docs/brainstorming-record.md`

## 会话接力规则

> **核心原则**：进度以 git commit 为准，新会话无需人工提醒。

1. 新会话启动时自动读取本文件 + 执行 `git log --oneline -10` 确定进度
2. 如果某个 Task **已 commit** → 跳过，做下一个
3. **开始新 Task 前评估剩余 token**：如果不足以完成该 Task（含实现 + 编译验证 + commit），则**不要开始**，告知用户"剩余 token 不足以完成 Task N，建议新开会话继续"
4. 每个 Task 完成 commit 后，更新下方"已完成"表（加 commit hash）

## 当前进度

### 已完成 ✅

| Task | Commit | 内容 |
|------|--------|------|
| Task 1 | `68d3927` | .NET 9 → .NET 8 降级 + Convert.ToHexStringLower 兼容修复 |
| Task 2 | `943c798` | 科技风全局主题 (theme.css + animations.css + App.vue 导航) |
| Task 3 | `6a7806d` | 设备列表页改造 (统计卡片 + 状态灯 + 搜索筛选) |

### 下一步 → Task 4 起

| Task | 内容 | 复杂度 | 依赖 |
|------|------|--------|------|
| **4** | ZLMediaKit REST 客户端 (IZlmClient) | 中 | 无 |
| **5** | SIP INVITE + StreamAppService + StreamController | 高 | Task 4 |
| **6** | WebRTC 实时预览前端 (WHEP 连接) | 中 | Task 5 |
| **7** | 诊断引擎核心框架 (IDiagnosticStep + DiagnosticEngine) | 高 | 无 |
| **8** | PingCheckStep + PortCheckStep | 低 | Task 7 |
| **9** | BrowserCheckStep (Playwright + Qwen 视觉) | 高 | Task 7 |
| **10** | 诊断引擎接入 API + 设备离线自动触发 | 中 | Task 7-9 |
| **11** | Qwen 客户端 + IAgentFunction + FunctionRegistry | 高 | 无 |
| **12** | AiAgentService (Function Calling 循环, max 5次) | 高 | Task 11 |
| **13** | 创建 xUnit 测试项目 + 补全 DiagnosticLog 实体字段 | 低 | Task 7-12 |
| **14** | 诊断引擎单元测试 (3个测试) | 中 | Task 13 |
| **15** | AI Agent 单元测试 (6个测试: Registry + Service) | 中 | Task 13 |
| **16** | 科技风改造诊断面板 + AI 问答页 | 中 | Task 10, 12 |
| **17** | 集成测试 + 最终验证 (预留给 Claude Code 做) | — | 全部 |

### 可并行的任务组

- **组A**: Task 4 → 5 → 6（流媒体链）
- **组B**: Task 7 → 8 → 9 → 10（诊断引擎链）
- **组C**: Task 11 → 12（AI Agent 链）
- 组A/B/C 之间互不依赖，可并行
- Task 13-15 在组B+C完成后执行
- Task 16 在组B+C完成后执行
- Task 17 最后执行

## 重要注意事项

### 每个 Task 的完整代码在计划文件中

**请先阅读** `docs/superpowers/plans/2026-03-28-gb28181-platform.md`，每个 Task 的所有代码（接口定义、实现、commit message）都已写好，照着执行即可。

### 已知的类型注意点

1. **DiagnosticTask.Id 是 int**（不是 long），计划中 DiagnosticEngine 的 `RunDiagnosticAsync(long taskId, ...)` 应改为 `int taskId`
2. **DiagnosticTask.Status 是 string**（"PENDING"/"RUNNING"/"COMPLETED"），不是 int。SqlSugar 更新时用 `SetColumns(t => t.Status == "RUNNING")` 语法
3. **DiagnosticLog 需要补全字段**：`StepType (int)` 和 `ScreenshotPath (string?)` — 在 Task 13 中处理
4. **AiConversation 实体**已有 UserId 但计划代码用 DeviceId — 注意使用已有字段名

### 编码规范

- 后端：C# 文件级 namespace、nullable 启用、async/await
- 前端：Vue 3 `<script setup lang="ts">`、使用 stores/ 目录的 Pinia store
- **每个 Task 完成后必须 commit**，commit message 已在计划文件中写好，直接使用
- 后端任务 commit 前必须 `dotnet build GB28181Platform.sln` 编译验证通过
- commit 格式：`feat(scope): description` / `test(scope): description`

### 前端开发

- `cd frontend/vms-web && npm run dev` 启动开发服务器
- 科技风主题变量在 `src/styles/theme.css` 中，直接用 `var(--tech-xxx)` 引用
- Element Plus CSS 变量已覆盖，无需手动添加暗色
