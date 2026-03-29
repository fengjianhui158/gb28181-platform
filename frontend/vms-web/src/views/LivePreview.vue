<template>
  <div class="preview-page">
    <div class="preview-layout">
      <!-- 左侧设备列表 -->
      <div class="tech-card device-panel">
        <div class="panel-title">设备列表</div>
        <div class="device-tree">
          <div
            v-for="device in devices"
            :key="device.id"
            class="device-item"
          >
            <div class="device-name">
              <span :class="['status-dot', device.status === 1 ? 'status-dot--online' : 'status-dot--offline']" />
              {{ device.name || device.id }}
            </div>
            <div
              v-for="ch in device.channels || [device]"
              :key="ch.id"
              class="channel-item"
              @click="handlePlay(device.id, ch.id || device.id)"
            >
              <span class="channel-icon">▶</span>
              {{ ch.name || ch.id || device.id }}
            </div>
          </div>
          <el-empty v-if="devices.length === 0" description="暂无设备" />
        </div>
      </div>

      <!-- 右侧视频区域 2x2 -->
      <div class="video-grid">
        <div
          v-for="(slot, idx) in videoSlots"
          :key="idx"
          class="tech-card video-cell"
        >
          <div v-if="slot.playing" class="video-header">
            <span class="tech-mono">{{ slot.streamId }}</span>
            <el-button size="small" type="danger" @click="handleStop(idx)">停止</el-button>
          </div>
          <video
            :ref="(el) => setVideoRef(idx, el as HTMLVideoElement)"
            autoplay
            muted
            playsinline
            class="video-player"
          />
          <div v-if="!slot.playing" class="video-placeholder">
            <span>等待连接</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onUnmounted } from 'vue'
import { getDevices } from '../api/device'
import { playStream, stopStream } from '../api/stream'
import { unwrapApiPayload, unwrapArrayPayload } from '../utils/apiPayload.js'

interface VideoSlot {
  playing: boolean
  streamId: string
  pc: RTCPeerConnection | null
  deviceId: string
  channelId: string
}

const devices = ref<any[]>([])
const videoRefs = ref<(HTMLVideoElement | null)[]>([null, null, null, null])
const videoSlots = reactive<VideoSlot[]>([
  { playing: false, streamId: '', pc: null, deviceId: '', channelId: '' },
  { playing: false, streamId: '', pc: null, deviceId: '', channelId: '' },
  { playing: false, streamId: '', pc: null, deviceId: '', channelId: '' },
  { playing: false, streamId: '', pc: null, deviceId: '', channelId: '' },
])

function setVideoRef(idx: number, el: HTMLVideoElement) {
  videoRefs.value[idx] = el
}

async function fetchDevices() {
  try {
    const res = await getDevices()
    devices.value = unwrapArrayPayload(res)
  } catch {
    devices.value = []
  }
}

function findEmptySlot(): number {
  return videoSlots.findIndex(s => !s.playing)
}

async function startWebRTC(webrtcUrl: string, videoEl: HTMLVideoElement): Promise<RTCPeerConnection> {
  const pc = new RTCPeerConnection()
  pc.addTransceiver('video', { direction: 'recvonly' })
  pc.addTransceiver('audio', { direction: 'recvonly' })

  pc.ontrack = (event) => {
    videoEl.srcObject = event.streams[0]
  }

  const offer = await pc.createOffer()
  await pc.setLocalDescription(offer)

  const resp = await fetch(webrtcUrl, {
    method: 'POST',
    headers: { 'Content-Type': 'application/sdp' },
    body: offer.sdp
  })
  const answerSdp = await resp.text()
  await pc.setRemoteDescription({ type: 'answer', sdp: answerSdp })

  return pc
}

async function handlePlay(deviceId: string, channelId: string) {
  const idx = findEmptySlot()
  if (idx === -1) {
    alert('所有窗口已占用，请先停止一个')
    return
  }

  try {
    const res = await playStream(deviceId, channelId)
    const data = unwrapApiPayload(res)
    if (!data.success) {
      alert(data.message || '拉流失败')
      return
    }

    const videoEl = videoRefs.value[idx]
    if (!videoEl) return

    const pc = await startWebRTC(data.webRtcUrl, videoEl)
    const slot = videoSlots[idx]
    slot.playing = true
    slot.streamId = `${deviceId}_${channelId}`
    slot.pc = pc
    slot.deviceId = deviceId
    slot.channelId = channelId
  } catch (e) {
    console.error('播放失败', e)
  }
}

async function handleStop(idx: number) {
  const slot = videoSlots[idx]
  if (!slot.playing) return

  try {
    await stopStream(slot.deviceId, slot.channelId)
  } catch { /* ignore */ }

  slot.pc?.close()
  slot.pc = null
  slot.playing = false
  slot.streamId = ''
  slot.deviceId = ''
  slot.channelId = ''

  const videoEl = videoRefs.value[idx]
  if (videoEl) videoEl.srcObject = null
}

onMounted(() => {
  fetchDevices()
})

onUnmounted(() => {
  videoSlots.forEach((slot, _idx) => {
    if (slot.pc) {
      slot.pc.close()
      slot.pc = null
    }
  })
})
</script>

<style scoped>
.preview-page {
  padding: 16px;
}

.preview-layout {
  display: flex;
  gap: 16px;
  height: calc(100vh - 120px);
}

.device-panel {
  width: 260px;
  min-width: 260px;
  padding: 16px;
  overflow-y: auto;
}

.panel-title {
  font-size: 14px;
  color: var(--tech-text-secondary);
  margin-bottom: 12px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--tech-border);
}

.device-item {
  margin-bottom: 8px;
}

.device-name {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 8px;
  font-size: 13px;
  color: var(--tech-text-primary);
}

.channel-item {
  padding: 4px 8px 4px 28px;
  font-size: 12px;
  color: var(--tech-text-secondary);
  cursor: pointer;
  border-radius: 4px;
  transition: background 0.2s;
}

.channel-item:hover {
  background: var(--tech-bg-hover);
  color: var(--tech-primary);
}

.channel-icon {
  margin-right: 4px;
  color: var(--tech-primary);
}

.video-grid {
  flex: 1;
  display: grid;
  grid-template-columns: 1fr 1fr;
  grid-template-rows: 1fr 1fr;
  gap: 8px;
}

.video-cell {
  position: relative;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  border-color: rgba(0, 212, 255, 0.25);
}

.video-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 6px 10px;
  font-size: 12px;
  background: var(--tech-bg-elevated);
  border-bottom: 1px solid var(--tech-border);
}

.video-player {
  flex: 1;
  width: 100%;
  height: 100%;
  object-fit: contain;
  background: #000;
}

.video-placeholder {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--tech-text-muted);
  font-size: 14px;
  background: rgba(0, 0, 0, 0.6);
}
</style>
