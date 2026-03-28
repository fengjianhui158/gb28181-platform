<template>
  <div class="chat-page">
    <div class="tech-card chat-container">
      <div class="panel-title">AI 智能运维助手</div>

      <!-- 快捷问题 -->
      <div class="quick-actions">
        <el-button size="small" @click="askQuick('当前有哪些离线设备？')">离线设备列表</el-button>
        <el-button size="small" @click="askQuick('系统整体运行状态如何？')">系统状态</el-button>
      </div>

      <!-- 消息区域 -->
      <div class="messages" ref="messagesRef">
        <div v-for="(msg, i) in messages" :key="i" :class="['msg', `msg-${msg.role}`]">
          <div class="msg-label tech-mono">{{ msg.role === 'user' ? 'YOU' : 'AI' }}</div>
          <div class="msg-bubble">
            <span v-if="msg.typing" class="typing-cursor">{{ msg.content }}</span>
            <span v-else>{{ msg.content }}</span>
          </div>
        </div>
        <div v-if="loading" class="msg msg-assistant">
          <div class="msg-label tech-mono">AI</div>
          <div class="msg-bubble">
            <span class="typing-cursor">思考中...</span>
          </div>
        </div>
      </div>

      <!-- 输入区域 -->
      <div class="input-area">
        <el-input
          v-model="inputText"
          placeholder="输入问题，例如：为什么摄像机 xxx 离线了？"
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
import { ref, nextTick } from 'vue'
import { chat } from '../api/ai'

interface Message {
  role: 'user' | 'assistant'
  content: string
  typing?: boolean
}

const messages = ref<Message[]>([])
const inputText = ref('')
const loading = ref(false)
const sessionId = ref('')
const messagesRef = ref<HTMLElement>()

function typewriterEffect(text: string, msg: Message) {
  let i = 0
  msg.typing = true
  msg.content = ''
  const interval = setInterval(() => {
    msg.content = text.slice(0, ++i)
    scrollToBottom()
    if (i >= text.length) {
      clearInterval(interval)
      msg.typing = false
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
  sendMessage()
}

async function sendMessage() {
  const text = inputText.value.trim()
  if (!text || loading.value) return

  messages.value.push({ role: 'user', content: text })
  inputText.value = ''
  loading.value = true
  scrollToBottom()

  try {
    const res: any = await chat(text, sessionId.value)
    sessionId.value = res.data?.sessionId || sessionId.value
    const reply = res.data?.reply || '暂无回复'
    const msg: Message = { role: 'assistant', content: '', typing: true }
    messages.value.push(msg)
    typewriterEffect(reply, msg)
  } catch {
    messages.value.push({ role: 'assistant', content: '请求失败，请稍后重试' })
  } finally {
    loading.value = false
    scrollToBottom()
  }
}
</script>

<style scoped>
.chat-page { padding: 16px; height: calc(100vh - 120px); display: flex; }
.chat-container { flex: 1; display: flex; flex-direction: column; padding: 16px; }
.panel-title { font-size: 14px; color: var(--tech-text-secondary); margin-bottom: 12px; padding-bottom: 8px; border-bottom: 1px solid var(--tech-border); }

.quick-actions { display: flex; gap: 8px; margin-bottom: 12px; }

.messages { flex: 1; overflow-y: auto; padding: 8px 0; display: flex; flex-direction: column; gap: 12px; }

.msg { display: flex; gap: 10px; }
.msg-user { flex-direction: row-reverse; }
.msg-label { font-size: 11px; color: var(--tech-text-muted); padding-top: 6px; min-width: 24px; }
.msg-user .msg-label { text-align: right; }

.msg-bubble { max-width: 75%; padding: 10px 14px; border-radius: 8px; font-size: 14px; line-height: 1.6; white-space: pre-wrap; }
.msg-user .msg-bubble { background: rgba(0, 212, 255, 0.12); border: 1px solid var(--tech-border-glow); color: var(--tech-text-primary); }
.msg-assistant .msg-bubble { background: var(--tech-bg-elevated); border: 1px solid var(--tech-border); color: var(--tech-text-primary); }

.input-area { display: flex; gap: 8px; margin-top: 12px; padding-top: 12px; border-top: 1px solid var(--tech-border); }
.input-area .el-input { flex: 1; }
</style>
