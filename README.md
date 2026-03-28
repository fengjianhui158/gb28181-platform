# GB28181 智能视频管理平台

基于 .NET 9 + Vue 3 + ZLMediaKit 的 GB28181 视频监控管理平台，支持设备注册、实时预览、离线智能诊断和 AI 问答。

## 功能特性

- **GB28181 设备注册** — SIP 服务器 (UAS)，支持 Digest 认证、心跳检测、目录查询
- **Web 视频预览** — WebRTC 低延迟播放 (<500ms)，通过 ZLMediaKit 转发
- **设备状态监控** — 实时检测在线/离线，SignalR 推送状态变更
- **离线自动诊断** — Ping → Web 端口检测 → Agent Browser 检查国标配置
- **AI 问答 Agent** — 基于 Qwen 大模型，分析诊断日志回答离线原因

## 技术栈

| 层 | 技术 |
|---|------|
| 后端 | C# .NET 9 / ASP.NET Core / SignalR |
| 前端 | Vue 3 / Element Plus / TypeScript / Vite |
| SIP | sipsorcery (UAS/UAC) |
| 流媒体 | ZLMediaKit (RTP → WebRTC/RTSP/FLV/HLS) |
| 数据库 | MySQL + SqlSugar ORM (CodeFirst) |
| 缓存 | Redis |
| AI | Qwen 3.5 (OpenAI 兼容 API / Function Calling) |
| 浏览器自动化 | Playwright (Headless Chromium) |

## 项目结构

```
├── src/
│   ├── GB28181Platform.Domain/          # 领域层 (实体、枚举)
│   ├── GB28181Platform.Application/     # 应用层 (DTO、AppService)
│   ├── GB28181Platform.Sip/             # SIP 信令服务
│   ├── GB28181Platform.Diagnostic/      # 诊断引擎
│   ├── GB28181Platform.AiAgent/         # AI Agent
│   ├── GB28181Platform.Infrastructure/  # 基础设施 (DB、Redis、ZLM)
│   └── GB28181Platform.Api/             # Web API 入口
├── frontend/vms-web/                    # Vue 3 前端
└── docs/                                # 设计文档
```

## 架构概览

```
Vue 3 + Element Plus
        │ HTTP / WebSocket / WebRTC
        ▼
ASP.NET Core Web API + SignalR
├── GB28181 SIP Server (sipsorcery) ── :5060
├── 设备管理 + 状态监控
├── 诊断引擎 (Ping → 端口 → Agent Browser)
├── AI Agent (Qwen 3.5 Function Calling)
└── 流媒体代理 (ZLMediaKit API)
        │
        ▼
ZLMediaKit ── RTP 收流 → WebRTC/RTSP/FLV 输出
        │
MySQL + Redis
```

## 快速开始

### 后端

```bash
cd src/GB28181Platform.Api
dotnet restore
dotnet run
```

### 前端

```bash
cd frontend/vms-web
npm install
npm run dev
```

### 依赖服务

- MySQL 8.0+
- Redis 7.0+
- ZLMediaKit ([安装指南](https://github.com/ZLMediaKit/ZLMediaKit))
- Qwen 3.5 LLM (OpenAI 兼容 API)

## 文档

- [需求概述](docs/01-需求概述.md)
- [技术选型](docs/02-技术选型.md)
- [系统架构](docs/03-系统架构.md)
- [核心流程设计](docs/04-核心流程设计.md)
- [部署架构](docs/06-部署架构.md)
- [设计规格书](docs/superpowers/specs/2026-03-28-gb28181-platform-design.md)

## 许可证

MIT
