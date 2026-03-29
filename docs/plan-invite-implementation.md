# SIP INVITE 完整实现计划

## 目标
用户在前端点击摄像机通道 → 浏览器显示实时视频流

## 验收标准
- [ ] 调用 `POST /api/stream/play` 后，平台向摄像机发送 SIP INVITE
- [ ] 摄像机收到 INVITE 后推 RTP 流到 ZLMediaKit 分配的端口
- [ ] 前端通过 WebRTC (WHEP) 播放视频
- [ ] 调用 `POST /api/stream/stop` 后，平台发送 BYE，摄像机停止推流，ZLMediaKit 释放端口
- [ ] 编译 0 错误 0 警告，现有 9 个测试继续通过

## 现状分析

### 已有的
- `Gb28181SipServer` 暴露 `Transport` 属性 (SIPTransport) 和 `SendRequestAsync` 方法
- `DeviceSessionManager` 存储了设备的 `RemoteEndPoint`、`RemoteIp`、`RemotePort`
- `InviteHandler.SendInviteAsync` 已有 SDP 构建逻辑（格式正确）
- `StreamAppService` 已有完整的 OpenRtpServer → INVITE → GetWebRtcPlayUrl 流程
- sipsorcery 10.0.3 提供 `UACInviteTransaction` 处理 INVITE 事务状态机

### 缺失的
- InviteHandler 没有真正发送 SIP INVITE（TODO 空壳）
- 没有 INVITE 事务处理（100 Trying / 200 OK / ACK 三次握手）
- 没有活跃会话(Dialog)管理，无法发 BYE 挂断
- StreamAppService.StopAsync 调用了 ZLM CloseRtpServer 但没有发 BYE

## 实现步骤

### Step 1: 修改 InviteHandler — 发送真正的 INVITE

**文件**: `src/GB28181Platform.Sip/Handlers/InviteHandler.cs`

**改动**:
1. 添加活跃会话字典 `ConcurrentDictionary<string, SIPDialogue>` 用于跟踪会话
2. 构建 SIP INVITE 请求:
   - From: `sip:{ServerId}@{ListenIp}:{Port}`
   - To: `sip:{channelId}@{RemoteIp}:{RemotePort}`
   - Contact: `sip:{ServerId}@{LocalIp}:{Port}`
   - Content-Type: `APPLICATION/SDP`
   - Subject: `{channelId}:{ssrc},{ServerId}:0` (GB28181 规范)
3. 使用 `UACInviteTransaction` 发送 INVITE 并处理响应:
   - 收到 200 OK → 发 ACK → 记录 Dialog → 返回 true
   - 超时或失败 → 返回 false
4. 添加 `SendByeAsync(string streamKey)` 方法:
   - 从字典取出 Dialog
   - 构建 BYE 请求并发送
   - 清理字典

### Step 2: 修改 StreamAppService — stop 时发 BYE

**文件**: `src/GB28181Platform.Application/Streams/StreamAppService.cs`

**改动**:
1. `StopAsync` 中先调用 `_invite.SendByeAsync(streamId)` 发 BYE
2. 再调用 `_zlm.CloseRtpServerAsync(streamId)` 释放端口

### Step 3: InviteHandler 接口补充

**文件**: `src/GB28181Platform.Sip/Handlers/InviteHandler.cs`

添加 `SendByeAsync` 方法签名，被 StreamAppService 调用。

## SIP INVITE 交互流程

```
平台 (SIP Server)              摄像机 (SIP Client)
    |                               |
    |--- INVITE (SDP: recvonly) --->|
    |<-- 100 Trying ---------------|
    |<-- 200 OK (SDP: sendonly) ---|
    |--- ACK ---------------------->|
    |                               |
    |   摄像机开始推 RTP 流到 ZLMediaKit
    |                               |
    |--- BYE ---------------------->|  (用户点击停止)
    |<-- 200 OK -------------------|
    |                               |
    |   摄像机停止推流
```

## 不做的事
- 不实现 INVITE 鉴权（摄像机作为被叫方不需要）
- 不实现 re-INVITE（暂不支持切换媒体参数）
- 不修改前端（LivePreview.vue 已完整）
- 不修改 ZlmClient（API 调用已正确）
