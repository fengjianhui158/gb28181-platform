<template>
  <div class="chat-page">
    <aside class="slogan-panel">
      <div class="slogan-shell">
        <div class="slogan-kicker">X-Link AI Manifesto</div>
        <div class="slogan-list">
          <div
            v-for="(line, index) in slogans"
            :key="line"
            class="slogan-line"
            :style="{ '--line-index': index }"
          >
            {{ line }}
          </div>
        </div>
      </div>
    </aside>

    <div class="tech-card chat-container">
      <div class="chat-header">
        <div class="panel-title">X-Link 智能体</div>
        <div class="panel-subtitle">
          <span class="subtitle-label">智能脉冲</span>
          <transition name="subtitle-fade" mode="out-in">
            <span :key="activeSlogan" class="subtitle-text">{{ activeSlogan }}</span>
          </transition>
        </div>
        <div v-if="boundDeviceId" class="device-context">
          当前设备：{{ boundDeviceId }}
        </div>
      </div>

      <div class="quick-actions">
        <el-button size="small" @click="askQuick('当前有哪些离线设备？')">离线设备列表</el-button>
        <el-button size="small" @click="askQuick('系统整体运行状态如何？')">系统状态</el-button>
        <el-button v-if="boundDeviceId" size="small" @click="askQuick(`请检查设备 ${boundDeviceId} 当前状态`)">
          当前设备状态
        </el-button>
      </div>

      <div class="messages" ref="messagesRef">
        <div v-for="msg in messages" :key="msg.id" :class="['msg', `msg-${msg.role}`]">
          <div class="msg-label tech-mono">{{ msg.role === 'user' ? 'YOU' : 'AI' }}</div>
          <div class="msg-bubble">
            <span v-if="msg.typing" class="typing-cursor">{{ msg.content }}</span>
            <span v-else>{{ msg.content }}</span>
            <div v-if="msg.meta" class="msg-meta">{{ msg.meta }}</div>
          </div>
        </div>
        <div v-if="loading" class="msg msg-assistant">
          <div class="msg-label tech-mono">AI</div>
          <div class="msg-bubble">
            <span class="typing-cursor">思考中...</span>
          </div>
        </div>
      </div>

      <div class="input-area">
        <el-input
          v-model="inputText"
          placeholder="输入问题，例如：为什么摄像机离线了？"
          @keyup.enter="sendMessage"
          :disabled="loading"
          size="large"
        />
        <el-button type="primary" @click="sendMessage" :loading="loading" size="large">
          发送
        </el-button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { chat, type AgentChatRequest, type AgentChatResponse, type ApiResponse } from '../api/ai'

interface Message {
  id: string
  role: 'user' | 'assistant'
  content: string
  typing?: boolean
  meta?: string
}

const route = useRoute()
const slogans = [
  '一智在手，天下少有',
  '一智在手，万联所有',
  '一智在手，你有我有',
  '一智在手，未来皆有'
]

const messages = ref<Message[]>([])
const inputText = ref('')
const loading = ref(false)
const conversationId = ref('')
const clientId = ref('')
const messagesRef = ref<HTMLElement>()
const activeSloganIndex = ref(0)
const boundDeviceId = computed(() => typeof route.query.deviceId === 'string' ? route.query.deviceId : '')
let sloganTimer: ReturnType<typeof setInterval> | null = null

const activeSlogan = computed(() => slogans[activeSloganIndex.value])
const clientIdStorageKey = 'vms-ai-chat-client-id'
const conversationStateStorageKey = computed(() =>
  boundDeviceId.value
    ? `vms-ai-chat-state:${boundDeviceId.value}`
    : 'vms-ai-chat-state:global')

function createMessageId(prefix: string) {
  return `${prefix}-${Date.now()}-${Math.random().toString(16).slice(2, 10)}`
}

function getOrCreateClientId() {
  if (typeof window === 'undefined') {
    return createMessageId('browser')
  }

  const existing = window.localStorage.getItem(clientIdStorageKey)
  if (existing) {
    return existing
  }

  const created = createMessageId('browser')
  window.localStorage.setItem(clientIdStorageKey, created)
  return created
}

function restoreConversationState() {
  if (typeof window === 'undefined') {
    return
  }

  const raw = window.localStorage.getItem(conversationStateStorageKey.value)
  if (!raw) {
    return
  }

  try {
    const parsed = JSON.parse(raw) as {
      conversationId?: string
      messages?: Message[]
    }

    conversationId.value = parsed.conversationId ?? ''
    messages.value = (parsed.messages ?? []).map(message => ({
      ...message,
      typing: false
    }))
  } catch {
    window.localStorage.removeItem(conversationStateStorageKey.value)
  }
}

function persistConversationState() {
  if (typeof window === 'undefined') {
    return
  }

  const snapshot = {
    conversationId: conversationId.value,
    messages: messages.value.map(message => ({
      ...message,
      typing: false
    }))
  }

  window.localStorage.setItem(conversationStateStorageKey.value, JSON.stringify(snapshot))
}

function typewriterEffect(text: string, index: number) {
  let i = 0
  messages.value[index].typing = true
  messages.value[index].content = ''
  const interval = setInterval(() => {
    messages.value[index].content = text.slice(0, ++i)
    scrollToBottom()
    if (i >= text.length) {
      clearInterval(interval)
      messages.value[index].typing = false
    }
  }, 20)
}

function scrollToBottom() {
  nextTick(() => {
    messagesRef.value?.scrollTo({ top: messagesRef.value.scrollHeight, behavior: 'smooth' })
  })
}

function askQuick(text: string) {
  inputText.value = text
  void sendMessage()
}

function extractResponsePayload(result: ApiResponse<AgentChatResponse> | AgentChatResponse) {
  if ('data' in result) {
    return result.data
  }

  return result
}

function extractAssistantText(response: AgentChatResponse) {
  const textBlocks = response.contentItems
    .filter(item => item.kind === 'text' && item.text)
    .map(item => item.text!.trim())
    .filter(Boolean)

  return textBlocks.length > 0 ? textBlocks.join('\n\n') : '暂无回复'
}

function buildAssistantMeta(response: AgentChatResponse) {
  const parts: string[] = []

  if (response.model) {
    parts.push(`模型：${response.model}`)
  }

  if (response.toolCalls.length > 0) {
    parts.push(`工具：${response.toolCalls.join('、')}`)
  }

  if (response.usage?.totalTokens) {
    parts.push(`Tokens：${response.usage.totalTokens}`)
  }

  return parts.join(' | ')
}

async function sendMessage() {
  const text = inputText.value.trim()
  if (!text || loading.value) return

  messages.value.push({
    id: createMessageId('user'),
    role: 'user',
    content: text
  })
  inputText.value = ''
  loading.value = true
  scrollToBottom()

  try {
    const payload: AgentChatRequest = {
      conversationId: conversationId.value || undefined,
      deviceId: boundDeviceId.value || undefined,
      clientId: clientId.value || undefined,
      clientMessageId: createMessageId('client'),
      contentItems: [
        {
          kind: 'text',
          text
        }
      ]
    }

    const result = await chat(payload)
    const response = extractResponsePayload(result)
    conversationId.value = response.conversationId || conversationId.value

    const reply = extractAssistantText(response)
    const assistantMessage: Message = {
      id: response.messageId || createMessageId('assistant'),
      role: 'assistant',
      content: '',
      typing: true,
      meta: buildAssistantMeta(response) || undefined
    }

    messages.value.push(assistantMessage)
    typewriterEffect(reply, messages.value.length - 1)
  } catch {
    messages.value.push({
      id: createMessageId('assistant-error'),
      role: 'assistant',
      content: '请求失败，请稍后重试'
    })
  } finally {
    loading.value = false
    scrollToBottom()
  }
}

onMounted(() => {
  clientId.value = getOrCreateClientId()
  restoreConversationState()
  sloganTimer = setInterval(() => {
    activeSloganIndex.value = (activeSloganIndex.value + 1) % slogans.length
  }, 2400)
})

onBeforeUnmount(() => {
  if (sloganTimer) {
    clearInterval(sloganTimer)
  }
})

watch([messages, conversationId], () => {
  persistConversationState()
}, { deep: true })
</script>

<style scoped>
.chat-page {
  position: relative;
  height: calc(100vh - 120px);
  padding: 16px 16px 16px 300px;
}

.slogan-panel {
  position: absolute;
  left: 16px;
  top: 16px;
  bottom: 16px;
  width: 240px;
  overflow: hidden;
  border-radius: 20px;
  background:
    radial-gradient(circle at top, rgba(0, 212, 255, 0.18), transparent 42%),
    linear-gradient(180deg, rgba(8, 14, 28, 0.96), rgba(4, 8, 18, 0.98));
  border: 1px solid rgba(0, 212, 255, 0.12);
  box-shadow:
    inset 0 0 0 1px rgba(255, 255, 255, 0.03),
    0 20px 44px rgba(0, 0, 0, 0.3);
}

.slogan-panel::before {
  content: '';
  position: absolute;
  inset: -24%;
  background: linear-gradient(180deg, transparent 0%, rgba(0, 212, 255, 0.08) 45%, transparent 100%);
  transform: rotate(8deg);
  animation: slogan-scan 6s linear infinite;
}

.slogan-shell {
  position: relative;
  z-index: 1;
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: 22px;
  height: 100%;
  padding: 28px 22px;
}

.slogan-kicker {
  color: rgba(153, 238, 255, 0.7);
  font-size: 11px;
  letter-spacing: 0.28em;
  text-transform: uppercase;
}

.slogan-list {
  display: flex;
  flex-direction: column;
  gap: 18px;
}

.slogan-line {
  position: relative;
  padding-left: 14px;
  color: #dff8ff;
  font-size: 20px;
  line-height: 1.45;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-shadow:
    0 0 14px rgba(0, 212, 255, 0.28),
    0 0 26px rgba(0, 212, 255, 0.14);
  opacity: calc(1 - var(--line-index) * 0.12);
}

.slogan-line::before {
  content: '';
  position: absolute;
  left: 0;
  top: 0.35em;
  width: 4px;
  height: 1.1em;
  border-radius: 999px;
  background: linear-gradient(180deg, var(--tech-primary-light), rgba(0, 255, 136, 0.7));
  box-shadow: 0 0 12px rgba(0, 212, 255, 0.45);
}

.chat-container {
  display: flex;
  flex-direction: column;
  min-width: 0;
  height: 100%;
  padding: 16px;
}

.chat-header {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin-bottom: 12px;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--tech-border);
}

.panel-title {
  color: var(--tech-text-primary);
  font-size: 18px;
  font-weight: 700;
  letter-spacing: 0.04em;
}

.panel-subtitle {
  display: flex;
  align-items: center;
  gap: 12px;
  min-height: 24px;
}

.subtitle-label {
  color: rgba(153, 238, 255, 0.68);
  font-size: 11px;
  letter-spacing: 0.24em;
  text-transform: uppercase;
}

.subtitle-text {
  color: var(--tech-primary-light);
  font-size: 14px;
  font-weight: 600;
  text-shadow: 0 0 12px rgba(0, 212, 255, 0.18);
}

.device-context {
  color: rgba(153, 238, 255, 0.78);
  font-size: 12px;
  letter-spacing: 0.04em;
}

.quick-actions {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
  flex-wrap: wrap;
}

.messages {
  flex: 1;
  overflow-y: auto;
  padding: 8px 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.msg {
  display: flex;
  gap: 10px;
}

.msg-user {
  flex-direction: row-reverse;
}

.msg-label {
  font-size: 11px;
  color: var(--tech-text-muted);
  padding-top: 6px;
  min-width: 24px;
}

.msg-user .msg-label {
  text-align: right;
}

.msg-bubble {
  max-width: 75%;
  padding: 10px 14px;
  border-radius: 8px;
  font-size: 14px;
  line-height: 1.6;
  white-space: pre-wrap;
}

.msg-user .msg-bubble {
  background: rgba(0, 212, 255, 0.12);
  border: 1px solid var(--tech-border-glow);
  color: var(--tech-text-primary);
}

.msg-assistant .msg-bubble {
  background: var(--tech-bg-elevated);
  border: 1px solid var(--tech-border);
  color: var(--tech-text-primary);
}

.msg-meta {
  margin-top: 10px;
  padding-top: 8px;
  border-top: 1px solid rgba(153, 238, 255, 0.12);
  color: var(--tech-text-muted);
  font-size: 12px;
}

.input-area {
  display: flex;
  gap: 8px;
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px solid var(--tech-border);
}

.input-area .el-input {
  flex: 1;
}

.typing-cursor::after {
  content: '|';
  margin-left: 2px;
  color: var(--tech-primary-light);
  animation: blink 1s steps(1) infinite;
}

.subtitle-fade-enter-active,
.subtitle-fade-leave-active {
  transition: opacity 0.35s ease, transform 0.35s ease;
}

.subtitle-fade-enter-from,
.subtitle-fade-leave-to {
  opacity: 0;
  transform: translateY(6px);
}

@keyframes blink {
  50% {
    opacity: 0;
  }
}

@keyframes slogan-scan {
  0% {
    transform: translateY(-32%) rotate(8deg);
  }
  100% {
    transform: translateY(32%) rotate(8deg);
  }
}

@media (max-width: 1280px) {
  .chat-page {
    padding-left: 272px;
  }

  .slogan-panel {
    width: 208px;
  }

  .slogan-line {
    font-size: 18px;
  }
}

@media (max-width: 980px) {
  .chat-page {
    padding: 16px;
    height: auto;
  }

  .slogan-panel {
    position: relative;
    left: auto;
    top: auto;
    bottom: auto;
    width: 100%;
    min-height: 220px;
    margin-bottom: 16px;
  }

  .chat-container {
    min-height: 620px;
  }
}

@media (max-width: 768px) {
  .panel-subtitle {
    flex-direction: column;
    align-items: flex-start;
    gap: 6px;
  }

  .slogan-line {
    font-size: 16px;
  }

  .msg-bubble {
    max-width: 88%;
  }

  .input-area {
    flex-direction: column;
  }
}
</style>
