<template>
  <div class="diagnostic-page">
    <!-- 顶部操作栏 -->
    <div class="tech-card action-bar">
      <el-input v-model="deviceId" placeholder="输入设备编码" class="device-input" clearable />
      <el-button type="primary" @click="runManualDiag" :disabled="!deviceId" :loading="running">
        手动诊断
      </el-button>
      <el-button @click="loadTasks">刷新</el-button>
    </div>

    <!-- 诊断任务列表 -->
    <div class="tech-card task-list">
      <div class="panel-title">诊断任务</div>
      <el-table :data="tasks" v-loading="loading" stripe>
        <el-table-column prop="id" label="ID" width="70">
          <template #default="{ row }">
            <span class="tech-mono">{{ row.id }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="deviceId" label="设备编码" width="220">
          <template #default="{ row }">
            <span class="tech-mono">{{ row.deviceId }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="triggerType" label="触发" width="80">
          <template #default="{ row }">
            <el-tag :type="row.triggerType === 'AUTO' ? 'warning' : 'info'" size="small" effect="dark">
              {{ row.triggerType }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="status" label="状态" width="110">
          <template #default="{ row }">
            <span :class="['status-badge', `status-${row.status?.toLowerCase()}`]">
              {{ row.status }}
            </span>
          </template>
        </el-table-column>
        <el-table-column label="诊断结论">
          <template #default="{ row }">
            <span class="tech-mono conclusion">{{ row.conclusion || '-' }}</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="100">
          <template #default="{ row }">
            <el-button size="small" @click="viewDetail(row.id)">详情</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 诊断详情时间线 -->
    <div v-if="detail" class="tech-card detail-panel">
      <div class="panel-title">
        诊断详情 #{{ detail.id }}
        <el-button size="small" @click="detail = null" style="float:right">关闭</el-button>
      </div>
      <div class="timeline">
        <div v-for="log in detail.logs" :key="log.id" class="timeline-item">
          <div :class="['timeline-dot', log.success ? 'dot-success' : 'dot-fail']" />
          <div class="timeline-content">
            <div class="step-header">
              <span class="step-name">{{ log.stepName }}</span>
              <span class="step-duration tech-mono">{{ log.durationMs }}ms</span>
            </div>
            <div class="step-detail">{{ log.detail }}</div>
            <img v-if="log.screenshotPath" :src="'/' + log.screenshotPath" class="screenshot" />
          </div>
        </div>
      </div>
      <div v-if="detail.conclusion" class="conclusion-box tech-mono">
        {{ detail.conclusion }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { getDiagnosticTasks, getDiagnosticTask, runDiagnostic } from '../api/diagnostic'

const route = useRoute()
const deviceId = ref(route.query.deviceId as string || '')
const tasks = ref<any[]>([])
const detail = ref<any>(null)
const loading = ref(false)
const running = ref(false)

async function loadTasks() {
  loading.value = true
  try {
    const res: any = await getDiagnosticTasks(deviceId.value || undefined)
    tasks.value = res.data || []
  } finally {
    loading.value = false
  }
}

async function runManualDiag() {
  if (!deviceId.value) return
  running.value = true
  try {
    await runDiagnostic(deviceId.value)
    ElMessage.success('诊断任务已创建')
    await loadTasks()
  } finally {
    running.value = false
  }
}

async function viewDetail(taskId: number) {
  const res: any = await getDiagnosticTask(taskId)
  detail.value = res.data
}

onMounted(() => loadTasks())
</script>

<style scoped>
.diagnostic-page { padding: 16px; display: flex; flex-direction: column; gap: 16px; }
.action-bar { display: flex; gap: 12px; padding: 16px; align-items: center; }
.device-input { width: 280px; }
.task-list { padding: 16px; }
.panel-title { font-size: 14px; color: var(--tech-text-secondary); margin-bottom: 12px; padding-bottom: 8px; border-bottom: 1px solid var(--tech-border); }
.conclusion { font-size: 12px; color: var(--tech-text-secondary); white-space: pre-line; }

.status-badge { padding: 2px 8px; border-radius: 4px; font-size: 12px; font-weight: 600; }
.status-completed { background: rgba(0, 255, 136, 0.15); color: var(--tech-online); }
.status-running { background: rgba(0, 212, 255, 0.15); color: var(--tech-primary); }
.status-pending { background: rgba(255, 170, 0, 0.15); color: var(--tech-warning); }
.status-failed { background: rgba(255, 68, 68, 0.15); color: var(--tech-offline); }

.detail-panel { padding: 16px; }
.timeline { padding: 8px 0; }
.timeline-item { display: flex; gap: 12px; margin-bottom: 16px; }
.timeline-dot { width: 12px; height: 12px; border-radius: 50%; margin-top: 4px; flex-shrink: 0; }
.dot-success { background: var(--tech-online); box-shadow: 0 0 8px var(--tech-online); }
.dot-fail { background: var(--tech-offline); box-shadow: 0 0 8px var(--tech-offline); }
.timeline-content { flex: 1; }
.step-header { display: flex; justify-content: space-between; margin-bottom: 4px; }
.step-name { font-weight: 600; color: var(--tech-text-primary); }
.step-duration { color: var(--tech-primary); font-size: 12px; }
.step-detail { font-size: 13px; color: var(--tech-text-secondary); }
.screenshot { max-width: 400px; margin-top: 8px; border: 1px solid var(--tech-border); border-radius: 4px; }
.conclusion-box { margin-top: 16px; padding: 12px; background: var(--tech-bg-elevated); border: 1px solid var(--tech-border); border-radius: 6px; font-size: 12px; white-space: pre-line; color: var(--tech-text-secondary); }
</style>
