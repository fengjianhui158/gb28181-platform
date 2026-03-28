<template>
  <div class="ai-chat">
    <el-card>
      <template #header>AI 智能问答</template>

      <div class="chat-messages" ref="messagesRef">
        <div v-for="(msg, i) in messages" :key="i" :class="['message', msg.role]">
          <div class="bubble">{{ msg.content }}</div>
        </div>
      </div>

      <div class="chat-input">
        <el-input
          v-model="inputText"
          placeholder="输入问题，例如：为什么摄像机 xxx 离线了？"
          @keyup.enter="sendMessage"
          :disabled="loading"
        >
          <template #append>
            <el-button type="primary" @click="sendMessage" :loading="loading">发送</el-button>
          </template>
        </el-input>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, nextTick } from 'vue'
import { chat } from '../api/ai'

interface Message {
  role: 'user' | 'assistant'
  content: string
}

const messages = ref<Message[]>([])
const inputText = ref('')
const loading = ref(false)
const sessionId = ref('')
const messagesRef = ref<HTMLElement>()

async function sendMessage() {
  const text = inputText.value.trim()
  if (!text || loading.value) return

  messages.value.push({ role: 'user', content: text })
  inputText.value = ''
  loading.value = true

  try {
    const res: any = await chat(text, sessionId.value)
    sessionId.value = res.data?.sessionId || sessionId.value
    messages.value.push({ role: 'assistant', content: res.data?.reply || '暂无回复' })
  } catch {
    messages.value.push({ role: 'assistant', content: '请求失败，请稍后重试' })
  } finally {
    loading.value = false
    await nextTick()
    messagesRef.value?.scrollTo({ top: messagesRef.value.scrollHeight, behavior: 'smooth' })
  }
}
</script>

<style scoped>
.chat-messages {
  height: 500px;
  overflow-y: auto;
  padding: 16px;
  background: #f5f5f5;
  border-radius: 8px;
  margin-bottom: 16px;
}
.message {
  margin-bottom: 12px;
  display: flex;
}
.message.user {
  justify-content: flex-end;
}
.bubble {
  max-width: 70%;
  padding: 10px 14px;
  border-radius: 8px;
  line-height: 1.5;
  white-space: pre-wrap;
}
.message.user .bubble {
  background: #409eff;
  color: white;
}
.message.assistant .bubble {
  background: white;
  border: 1px solid #e4e7ed;
}
</style>
