# GB28181 视频管理平台 — 头脑风暴记录

> 日期：2026-03-28
> 参与者：用户 + Claude AI

---

## 一、原始需求

实现一个 GB28181 管理平台：
- 摄像机通过 GB28181 协议注册到平台
- Web 网页预览摄像机视频
- 检测摄像机状态，离线后自动诊断（ping → Web端口 → Agent Browser 访问摄像机网页检查国标配置）
- 全程记录诊断日志
- 开发 AI Agent 问答功能，询问"为什么摄像机离线了"，大模型回答诊断结果

---

## 二、关键问答记录

### Q1: 技术栈选择？
> **问**：这个新的 GB28181 管理平台，你希望基于哪个技术栈来开发？
> - 选项：Go 扩展 / Python FastAPI / Node.js / Java 重构
>
> **答**：**C# .NET Core 做管理平台**

### Q2: 前端框架？
> **问**：Web 前端用什么技术？
> - 选项：Blazor / Vue 3 + Element Plus / React + Ant Design / Angular
>
> **答**：**Vue 3 + Element Plus**

### Q3: 视频预览方式？
> **问**：Web 端视频预览用什么方式？这决定了流媒体服务的架构。
> - 选项：WebRTC / HTTP-FLV / HLS / ZLMediaKit
>
> **答**：**WebRTC**（低延迟 <500ms）

### Q4: AI 大模型选择？
> **问**：AI 诊断 Agent 用哪个大模型？
> - 选项：本地 Ollama / 云端 API / 两者都支持
>
> **答**：**公司自建的 Qwen 3.5 大模型**（OpenAI 兼容 API）

### Q5: 摄像机品牌？
> **问**：摄像机品牌主要是哪些？这影响浏览器自动化访问摄像机网页的实现。
> - 选项：海康 / 大华 / 多品牌混合
>
> **答**：**多品牌混合**（海康、大华、宇视、华为等）

### Q6: 管理规模？
> **问**：平台需要管理多少路摄像机？这影响架构设计。
> - 选项：<100路 / 100-500路 / 500-2000路 / >2000路
>
> **答**：**中型（100-500路）**

### Q7: 浏览器自动化方案？
> **问**：浏览器自动化访问摄像机网页检查配置，你倾向哪种方案？
> - 选项：Playwright + AI 视觉 / 按品牌写脚本 / HTTP API 直调
>
> **答**：**用 Agent Browser 直接访问网页**（最灵活，适配多品牌）

### Q8: 架构方案选择？
> **问**：你对这个架构方向怎么看？选择方案 C（.NET + ZLMediaKit）还是方案 B（复用 Go 服务）？
>
> **答**：**方案 C: .NET + ZLMediaKit**

---

## 三、方案对比与决策

### 三种方案

| 维度 | 方案 A: 纯 .NET 全新构建 | 方案 B: Go + .NET 混合 | 方案 C: .NET + ZLMediaKit |
|------|---|---|---|
| SIP 服务器 | .NET sipsorcery 自研 | 复用 Go JydCMS | .NET sipsorcery 自研 |
| 流媒体 | .NET 自研（RTP→WebRTC） | 复用 Go JydSMS | ZLMediaKit（C++，业界主流） |
| 技术栈统一 | 高（全.NET） | 低（Go+C#双语言） | 中（C# + C++组件） |
| 开发工作量 | 极大（流媒体自研风险高） | 最小（核心复用） | 中等 |
| 流媒体成熟度 | 低 | 高 | 极高 |
| 推荐度 | 不推荐 | 备选（快速交付） | **推荐** |

### 最终决策：方案 C

---

## 四、确定的架构设计

### 核心架构

```
Vue 3 + Element Plus（前端）
        │ HTTP / WebSocket / WebRTC
        ▼
ASP.NET Core Web API + SignalR
├── GB28181 SIP Server (sipsorcery UAS) ── 端口 5060
│   接收摄像机注册/心跳/INVITE/目录查询
├── 设备管理 + 状态监控
├── 诊断引擎 (Ping → 端口 → Agent Browser)
├── AI Agent (Qwen 3.5 Function Calling)
└── 流媒体代理 (调用 ZLMediaKit API)
        │ REST API + Hook 回调
        ▼
ZLMediaKit (C++ 流媒体服务器)
├── RTP 收流 (GB28181 PS流)
├── WebRTC 输出 (浏览器直接拉流)
└── RTSP/FLV/HLS 兼容输出
        │
PostgreSQL + Redis
```

### 分层架构

- **Layer 5 - Presentation**: Vue 3 SPA + ASP.NET Core Controllers + SignalR Hubs
- **Layer 4 - Application**: 设备管理 / 流媒体 / 诊断 / AI Agent / 用户认证
- **Layer 3 - Domain**: GB28181 SIP Server / 诊断引擎 / AI Agent 引擎
- **Layer 2 - Infrastructure**: EF Core + Redis + ZLMediaKit Client + Playwright + Qwen API
- **Layer 1 - External**: ZLMediaKit / Redis / PostgreSQL / Qwen 3.5 LLM

### .NET 项目结构

```
GB28181Platform/
├── src/
│   ├── GB28181Platform.Domain/          # 领域层（实体、枚举、仓储接口、事件）
│   ├── GB28181Platform.Application/     # 应用层（AppService、DTO）
│   ├── GB28181Platform.SipServer/       # SIP 服务（sipsorcery UAS）
│   ├── GB28181Platform.Diagnostic/      # 诊断引擎
│   ├── GB28181Platform.AiAgent/         # AI Agent
│   ├── GB28181Platform.Infrastructure/  # 基础设施（EF Core、Redis、ZLM客户端）
│   └── GB28181Platform.WebApi/          # Web API 入口
├── web/                                 # Vue 3 前端
└── docs/                               # 设计文档
```

### 离线诊断流程

```
摄像机离线 (心跳超时)
    ↓
Step 1: ICMP Ping
    ├── 不通 → 记录"网络不可达"，结束
    └── 通 → 继续
        ↓
Step 2: TCP 端口检测 (80/443)
    ├── 不通 → 记录"Web服务不可达"，结束
    └── 通 → 继续
        ↓
Step 3: Agent Browser 访问摄像机网页
    ├── 截图 + AI 分析国标配置页面
    └── 记录诊断结论（配置错误/正常等）
        ↓
全部结果写入 DiagnosticLog 表
```

### AI Agent 设计

- 接口：OpenAI 兼容 API → 公司 Qwen 3.5
- 方式：Function Calling + RAG（诊断日志作为上下文）
- 用户问"为什么摄像机离线"→ Agent 检索诊断日志 → 大模型生成回答

---

## 五、已有可复用资源

| 资源 | 路径 | 可复用部分 |
|------|------|-----------|
| sipsorcery 8.0.23 | VMS/sipsorcery-8.0.23/ | SIP UAS 能力、SDP 解析、SIP 认证 |
| VmsClient.Protocol | VmsClient/src/VmsClient.Protocol/ | MANSCDP XML Builder/Parser |
| JydCMS (Go) | Platform/jydjbs/jydcms/ | SIP 处理逻辑参考蓝本、数据模型 |
| JydSMS (Go) | Platform/jydjbs/jydsms/ | 流媒体处理参考 |
| backend-architecture.md | VmsClient/src/backend-architecture.md | Clean Architecture 规范 |

---

## 六、Phase 1 实施结果

Phase 1 基础框架已完成：
- **后端 (.NET 9)**：7 个分层项目，Clean Architecture，编译通过
- **SIP 服务器**：基于 sipsorcery UAS，支持 REGISTER（Digest 认证）、心跳、MESSAGE
- **设备监控**：BackgroundService 定时检测心跳超时，自动标记离线
- **数据库**：SqlSugar CodeFirst，自动创建 7 张表
- **API**：DeviceController / DiagnosticController / AiAgentController
- **SignalR Hub**：设备状态实时推送
- **前端 (Vue 3)**：4 个页面（设备列表、实时预览、诊断面板、AI 问答）
- **文档**：7 份设计文档（需求、技术选型、架构、流程、数据模型、部署、实施计划）
