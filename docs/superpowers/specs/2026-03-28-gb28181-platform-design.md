# GB28181 智能视频管理平台 — 设计规格书

> 版本: 1.0
> 日期: 2026-03-28
> 状态: 待审核

---

## 1. 项目概述

### 1.1 背景

轨道交通视频监控项目需要一个基于 Web 的 GB28181 管理平台，用于统一管理摄像机设备，并具备智能诊断和 AI 问答能力。

### 1.2 核心功能

| # | 功能 | 描述 |
|---|------|------|
| F1 | GB28181 设备注册 | 摄像机通过 SIP 协议注册到平台（UAS），支持 Digest 认证 |
| F2 | Web 视频预览 | Vue 3 网页通过 WebRTC 实时预览视频，延迟 < 500ms |
| F3 | 设备状态监控 | 基于 SIP 心跳超时判断在线/离线，SignalR 实时推送 |
| F4 | 离线自动诊断 | Ping → Web 端口 → Agent Browser 检查国标配置，全程记录日志 |
| F5 | AI 问答 Agent | 用户询问离线原因，大模型基于诊断日志回答并给出修复建议 |

### 1.3 非功能需求

| 需求 | 描述 |
|------|------|
| 规模 | 100-500 路摄像机 |
| 品牌 | 多品牌混合（海康、大华、宇视、华为等） |
| 延迟 | 视频预览 < 500ms |
| 可用性 | 7x24 运行 |

---

## 2. 技术选型

### 2.1 选型结果

| 组件 | 技术 | 说明 |
|------|------|------|
| 后端 | C# .NET 9 (ASP.NET Core) | 统一后端语言 |
| 前端 | Vue 3 + Element Plus + TypeScript | 国内主流方案 |
| SIP 库 | sipsorcery 10.0.3 | 纯 C# SIP 库，支持 UAC/UAS |
| 流媒体 | ZLMediaKit | GB28181 RTP→WebRTC，业界最成熟 |
| 数据库 | MySQL + SqlSugar ORM | CodeFirst 自动建表 |
| 缓存 | Redis | 设备在线状态、事件发布 |
| 大模型 | 公司自建 Qwen 3.5 | OpenAI 兼容 API，Function Calling |
| 浏览器自动化 | Playwright (Headless Chromium) | Agent Browser 访问摄像机网页 |
| 实时推送 | SignalR | 设备状态变更推送 |
| 日志 | Serilog | 结构化日志 |

### 2.2 方案决策过程

评估了 3 种架构方案，最终选择方案 C：

| 维度 | A: 纯 .NET 全新 | B: Go + .NET 混合 | **C: .NET + ZLMediaKit** |
|------|---|---|---|
| SIP 服务器 | sipsorcery 自研 | 复用 Go JydCMS | sipsorcery 自研 |
| 流媒体 | .NET 自研 RTP→WebRTC | 复用 Go JydSMS | ZLMediaKit (C++) |
| 技术栈统一 | 高 | 低 | 中 |
| 开发工作量 | 极大 | 最小 | 中等 |
| 流媒体成熟度 | 低 | 高 | 极高 |
| 结论 | 不推荐 | 备选 | **选定** |

**选择方案 C 的理由**:
1. .NET 统一后端语言，SIP 和 Web API 深度集成
2. ZLMediaKit 原生支持 GB28181 PS 解封装 → WebRTC，单机支撑数千路
3. sipsorcery 完全支持 UAS 模式
4. 可复用 VmsClient.Protocol 的 MANSCDP XML Builder/Parser

### 2.3 可复用的已有资源

| 资源 | 路径 | 复用方式 |
|------|------|---------|
| sipsorcery 8.0.23 | `VMS/sipsorcery-8.0.23/` | SIP UAS 能力、SDP 解析、Digest 认证 |
| VmsClient.Protocol | `VmsClient/src/VmsClient.Protocol/` | MANSCDP XML Builder/Parser 模式参考 |
| JydCMS (Go) | `Platform/jydjbs/jydcms/` | SIP 处理逻辑参考蓝本、数据模型参考 |
| JydSMS (Go) | `Platform/jydjbs/jydsms/` | 流媒体转发逻辑参考 |
| backend-architecture.md | `VmsClient/src/backend-architecture.md` | Clean Architecture 分层规范参考 |

---

## 3. 系统架构

### 3.1 整体架构

```
┌─────────────────────────────────────────────────────────────┐
│                  Vue 3 + Element Plus (前端 SPA)             │
│           WebRTC Player / 设备管理 / 诊断面板 / AI 问答       │
└───────────────────────┬─────────────────────────────────────┘
                        │ HTTP / WebSocket / WebRTC
┌───────────────────────▼─────────────────────────────────────┐
│                ASP.NET Core Web API + SignalR                │
│  ┌──────────────┬──────────────┬───────────────┬──────────┐ │
│  │ DeviceMgmt   │ DiagEngine   │ AI Agent      │ Stream   │ │
│  │ 设备/通道CRUD │ Ping/TCP/    │ Qwen 3.5      │ Proxy    │ │
│  │ 状态监控      │ BrowserAgent │ FunctionCall  │ ZLM API  │ │
│  └──────────────┴──────────────┴───────────────┴──────────┘ │
│                                                              │
│  ┌──────────────────────────────────────────────────────────┐│
│  │          GB28181 SIP Server (sipsorcery UAS)             ││
│  │   REGISTER / 心跳 / INVITE / MESSAGE / 目录查询           ││
│  │   端口: 5060 UDP+TCP                                     ││
│  └──────────────────────────────────────────────────────────┘│
├──────────────────────────────────────────────────────────────┤
│             MySQL (SqlSugar)      │         Redis            │
│    设备/通道/诊断日志/用户          │  在线状态/实时事件        │
└──────────────────────┬────────────┴──────────────────────────┘
                       │ REST API (Hook回调 + 主动调用)
┌──────────────────────▼──────────────────────────────────────┐
│                    ZLMediaKit (C++)                          │
│   RTP Server (:30000-30249) ─ 接收 GB28181 PS 流            │
│   WebRTC (:8443)             ─ WHEP 输出给浏览器             │
│   RTSP (:554)                ─ 兼容传统客户端                 │
│   HTTP-FLV / HLS              ─ 兼容移动端                   │
│   Hook 回调 → .NET API        ─ 流状态同步                   │
└─────────────────────────────────────────────────────────────┘
```

### 3.2 分层架构

```
Layer 5: Presentation   — Vue 3 SPA + ASP.NET Core Controllers + SignalR Hubs
Layer 4: Application    — DeviceAppService / StreamAppService / DiagnosticAppService / AiAgentAppService
Layer 3: Domain Service — GB28181 SIP Server / DiagnosticEngine / AI Agent Engine
Layer 2: Infrastructure — SqlSugar(MySQL) / Redis / ZLMediaKit Client / Qwen HTTP Client / Playwright
Layer 1: External       — ZLMediaKit / Redis Server / MySQL Server / Qwen 3.5 LLM
```

### 3.3 .NET 项目结构

```
GB28181Platform/
├── src/
│   ├── GB28181Platform.Domain/          # 领域层 (实体、枚举、通用类)
│   │   ├── Entities/                    # Device, Channel, DiagnosticLog, DiagnosticTask,
│   │   │                               # AlarmRecord, User, AiConversation
│   │   ├── Enums/                       # DeviceStatus, DiagnosticStepType, StreamTransport
│   │   └── Common/                      # ApiResponse, PagedResult
│   │
│   ├── GB28181Platform.Application/     # 应用层 (DTO, AppService)
│   │   ├── Devices/                     # 设备管理
│   │   ├── Streams/                     # 流媒体 (调 ZLMediaKit)
│   │   ├── Diagnostics/                 # 诊断服务
│   │   └── AiAgent/                     # AI 问答
│   │
│   ├── GB28181Platform.Sip/            # SIP 信令服务层
│   │   ├── Server/                      # Gb28181SipServer (BackgroundService)
│   │   ├── Handlers/                    # Register, Keepalive, Invite, Message, Catalog
│   │   ├── Xml/                         # MANSCDP XML 构建器/解析器
│   │   ├── Auth/                        # SIP Digest 认证
│   │   └── Sessions/                    # DeviceSessionManager (在线设备管理)
│   │
│   ├── GB28181Platform.Diagnostic/      # 诊断引擎
│   │   ├── Engine/                      # DiagnosticPipeline
│   │   ├── Steps/                       # PingCheck / PortCheck / BrowserCheck
│   │   └── Browser/                     # CameraBrowserAgent (Playwright + Qwen 视觉)
│   │
│   ├── GB28181Platform.AiAgent/         # AI Agent 服务
│   │   ├── QwenClient.cs                # OpenAI 兼容 HTTP 客户端
│   │   ├── Functions/                   # Function Calling 定义与执行
│   │   └── Prompts/                     # System Prompt 模板
│   │
│   ├── GB28181Platform.Infrastructure/  # 基础设施层
│   │   ├── Database/                    # SqlSugar 初始化 + CodeFirst
│   │   ├── Redis/                       # Redis 服务
│   │   └── MediaServer/                 # ZLMediaKit REST 客户端
│   │
│   └── GB28181Platform.Api/            # 表现层 (ASP.NET Core)
│       ├── Controllers/                 # Device, Channel, Stream, Diagnostic, AiAgent, Auth
│       ├── Hubs/                        # DeviceStatusHub (SignalR)
│       ├── BackgroundServices/          # SipServer, DeviceMonitor, DiagnosticScheduler
│       └── Program.cs
│
├── frontend/vms-web/                    # Vue 3 前端项目
├── docs/                                # 设计文档
└── tests/                               # 单元/集成测试
```

### 3.4 项目依赖关系

```
Domain ← Application ← Api
Domain ← Sip ← Api
Domain ← Diagnostic ← Api
Domain ← AiAgent ← Api
Domain ← Infrastructure ← Api
Application ← Infrastructure
```

---

## 4. 核心流程设计

### 4.1 设备注册流程

```
摄像机                          SIP Server (.NET)                    MySQL
  │                                 │                                  │
  │──REGISTER(无Auth)──────────────>│                                  │
  │<─401 Unauthorized(nonce)────────│                                  │
  │──REGISTER(Digest Auth)─────────>│                                  │
  │                                 │──验证密码 + INSERT/UPDATE───────>│
  │                                 │──SignalR 推送"设备上线"──> 前端    │
  │<─200 OK(Expires=3600)───────────│                                  │
  │──Keepalive(每60s)──────────────>│──更新心跳时间──────────────────────>│
```

关键点:
- 首次 REGISTER 无认证 → 401 + nonce
- 二次 REGISTER 携带 Digest → 200 OK
- Expires=0 表示注销
- 注册成功后标记 Online，SignalR 推送

### 4.2 视频预览流程

```
前端 (Vue)          .NET API           ZLMediaKit          摄像机
  │─POST /api/stream/play─>│               │                  │
  │                    │─openRtpServer────>│                  │
  │                    │<─返回RTP端口──────│                  │
  │                    │──INVITE(SDP=ZLM)──────────────────>│
  │                    │<─200 OK(SDP)──────────────────────│
  │                    │──ACK──────────────────────────────>│
  │                    │                   │<──RTP PS流────────│
  │<─返回WebRTC播放地址─│                   │                  │
  │────WebRTC WHEP SDP 交换──────────────>│                  │
  │<───WebRTC 视频流──────────────────────│                  │
```

关键点:
- .NET 先向 ZLMediaKit 申请 RTP 端口
- INVITE SDP 媒体地址指向 ZLMediaKit
- ZLMediaKit 自动解封装 PS → WebRTC
- 前端通过 WHEP 拉流

### 4.3 离线诊断流程

```
DeviceMonitorService (每30s检查)
  │── 扫描所有在线设备，检查心跳超时 (默认 300s)
  │
  ├─ 超时设备:
  │    │── 标记 Offline + SignalR 推送
  │    │── 创建诊断任务 → diagnostic_tasks
  │    │
  │    │── Step 1: ICMP Ping
  │    │    ├─ 不通 → "网络不可达"，结束
  │    │    └─ 通 → 继续
  │    │
  │    │── Step 2: TCP 端口检查 (80/443)
  │    │    ├─ 不通 → "Web服务不可达"，结束
  │    │    └─ 通 → 继续
  │    │
  │    │── Step 3: Agent Browser 检查
  │    │    │── Playwright 打开摄像机网页
  │    │    │── 登录 → 导航国标配置页面
  │    │    │── 截图 + 提取 GB28181 配置
  │    │    │── 对比平台配置 → 找出差异
  │    │    └── 记录结果
  │    │
  │    └── 生成诊断结论 → diagnostic_tasks.conclusion
```

多品牌适配策略 (Agent Browser 方案):
- 使用 Playwright 打开摄像机网页并截图
- 将截图发送给 Qwen 视觉模型，由 AI 识别页面中的国标配置信息（SIP 服务器 IP、端口、设备编码等）
- AI 对比平台期望配置与截图中的实际配置，输出差异
- 此方案无需按品牌写死规则，天然适配多品牌

### 4.4 AI 问答流程

```
用户: "为什么摄像机 34020000001320000001 离线了？"
  │
  ▼
AI Agent Service
  1. 构建 messages (System Prompt + User Message + Functions 定义)
  2. 发送到 Qwen 3.5 API
  3. Qwen function_call: get_device_status → 执行
  4. Qwen function_call: get_diagnostic_logs → 执行
  5. Qwen 综合分析，生成自然语言回答
  │
  ▼
回答: "该摄像机从 14:30 开始离线。诊断显示 Ping 可达，Web 端口开放，
      但国标配置中 SIP 服务器 IP 错误 (配置为 192.168.1.100，
      应为 188.18.35.225)。建议修正 SIP 服务器 IP。"
```

Function Calling 定义:

| 函数 | 描述 | 参数 |
|------|------|------|
| `get_device_status` | 查询设备状态和最后心跳时间 | deviceId |
| `get_diagnostic_logs` | 查询最近诊断日志 | deviceId, limit? |
| `get_device_info` | 查询设备详细信息 | deviceId |
| `run_diagnostic` | 立即执行一次诊断 | deviceId |
| `list_offline_devices` | 列出所有离线设备 | — |
| `get_alarm_records` | 查询报警记录 | deviceId, hours? |

---

## 5. 数据模型

### 5.1 核心实体

#### devices (设备表)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | long | 主键 |
| DeviceId | string(50) | GB28181 设备编码 (唯一) |
| DeviceName | string(100) | 设备名称 |
| Manufacturer | string(50) | 厂商 |
| Model | string(50) | 型号 |
| IpAddress | string(50) | 设备 IP |
| Port | int | SIP 端口 |
| WebPort | int | Web 管理端口，默认 80 |
| WebUsername | string(50) | Web 登录用户名 |
| WebPassword | string(100) | Web 登录密码 (加密存储) |
| Status | int | 0=Unknown, 1=Online, 2=Offline |
| Transport | string(10) | UDP/TCP |
| RegisterTime | DateTime | 最后注册时间 |
| KeepaliveTime | DateTime | 最后心跳时间 |
| SipPassword | string(100) | SIP 认证密码 |
| CreatedAt | DateTime | 创建时间 |
| UpdatedAt | DateTime | 更新时间 |

#### channels (通道表)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | long | 主键 |
| ChannelId | string(50) | GB28181 通道编码 |
| DeviceId | string(50) | 所属设备编码 |
| ChannelName | string(100) | 通道名称 |
| Status | int | 在线状态 |
| PtzType | int | PTZ 类型 |
| Longitude | double | 经度 |
| Latitude | double | 纬度 |

#### diagnostic_tasks (诊断任务表)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | long | 主键 |
| DeviceId | string(50) | 设备编码 |
| Status | int | 0=Pending, 1=Running, 2=Completed, 3=Failed |
| TriggerType | int | 0=Auto, 1=Manual |
| Conclusion | string(2000) | 诊断结论 |
| CreatedAt | DateTime | 创建时间 |
| CompletedAt | DateTime | 完成时间 |

#### diagnostic_logs (诊断日志表)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | long | 主键 |
| TaskId | long | 关联诊断任务 |
| DeviceId | string(50) | 设备编码 |
| StepType | int | 0=Ping, 1=Port, 2=Browser |
| StepName | string(50) | 步骤名称 |
| Success | bool | 是否成功 |
| Result | string(2000) | 结果详情 |
| Duration | int | 耗时(ms) |
| ScreenshotPath | string(500) | 截图路径 (Browser 步骤) |
| CreatedAt | DateTime | 创建时间 |

#### ai_conversations (AI 对话表)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | long | 主键 |
| SessionId | string(50) | 会话 ID |
| DeviceId | string(50) | 关联设备 (可选) |
| Role | string(20) | user/assistant/system |
| Content | string(5000) | 消息内容 |
| CreatedAt | DateTime | 创建时间 |

#### alarm_records (报警记录表)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | long | 主键 |
| DeviceId | string(50) | 设备编码 |
| ChannelId | string(50) | 通道编码 |
| AlarmType | int | 报警类型 |
| AlarmLevel | int | 报警级别 |
| Description | string(500) | 报警描述 |
| CreatedAt | DateTime | 创建时间 |

#### users (用户表)

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | long | 主键 |
| Username | string(50) | 用户名 |
| PasswordHash | string(200) | 密码哈希 |
| DisplayName | string(100) | 显示名 |
| Role | string(20) | 角色 |
| IsEnabled | bool | 是否启用 |
| CreatedAt | DateTime | 创建时间 |

---

## 6. 部署架构

### 6.1 端口规划

| 服务 | 端口 | 协议 | 开放范围 |
|------|------|------|----------|
| Nginx | 80/443 | TCP | 用户浏览器 |
| .NET Core | 5000 | TCP | Nginx 反代 |
| .NET Core SIP | 5060 | UDP+TCP | 摄像机网段 |
| ZLMediaKit API | 8080 | TCP | 内部 |
| ZLMediaKit RTP | 30000-30249 | UDP+TCP | 摄像机网段 |
| ZLMediaKit WebRTC | 8443 | TCP+UDP | 用户浏览器 |
| MySQL | 3306 | TCP | 内部 |
| Redis | 6379 | TCP | 内部 |

### 6.2 部署拓扑

```
┌─────────────────────────────────────────────────────────┐
│                  服务器 (Linux / Windows)                  │
│                                                          │
│  Nginx (:80/:443)                                        │
│    ├── / → Vue 3 SPA 静态文件                             │
│    ├── /api → 反代 .NET :5000                             │
│    └── /hubs → 反代 .NET :5000 (WebSocket)                │
│                                                          │
│  .NET Core App                                           │
│    ├── :5000 HTTP API + SignalR                           │
│    └── :5060 SIP Server (UDP+TCP)                        │
│                                                          │
│  ZLMediaKit                                              │
│    ├── :8080 REST API                                    │
│    ├── :30000-30249 RTP 收流                              │
│    └── :8443 WebRTC                                      │
│                                                          │
│  MySQL :3306  │  Redis :6379                              │
│                                                          │
│  Qwen 3.5 LLM (公司自建，独立服务器/内网)                  │
└─────────────────────────────────────────────────────────┘
```

### 6.3 防火墙规则

对外开放:
- **80/443** — 浏览器访问
- **5060 UDP+TCP** — 摄像机 SIP 信令
- **30000-30249 UDP+TCP** — 摄像机 RTP 推流
- **8443 TCP+UDP** — 浏览器 WebRTC

内部端口 (不对外): 5000, 8080, 3306, 6379

---

## 7. 前端页面

### 7.1 页面清单

| 页面 | 路由 | 功能 |
|------|------|------|
| 设备列表 | /devices | 设备 CRUD、在线状态实时显示、搜索筛选 |
| 实时预览 | /preview | WebRTC 视频播放、多画面分屏 |
| 诊断面板 | /diagnostics | 诊断任务列表、日志详情、手动触发诊断 |
| AI 问答 | /ai-chat | 对话式问答，询问设备离线原因 |

### 7.2 前端技术栈

- Vue 3 + Composition API
- Element Plus UI 组件库
- TypeScript
- Pinia 状态管理
- Axios HTTP 客户端
- Vite 构建工具
- SignalR 客户端 (实时推送)
