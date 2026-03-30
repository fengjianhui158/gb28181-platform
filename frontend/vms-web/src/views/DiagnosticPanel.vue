<template>
  <div class="diag-page">
    <!-- 顶部操作栏 -->
    <div class="tech-card diag-toolbar">
      <div class="toolbar-left">
        <span class="toolbar-title">诊断中心</span>
      </div>
      <div class="toolbar-right">
        <el-input v-model="deviceId" placeholder="设备编码" class="device-input" clearable>
          <template #prefix>
            <el-icon><Search /></el-icon>
          </template>
        </el-input>
        <el-button type="primary" @click="runManualDiag" :disabled="!deviceId" :loading="running">
          运行诊断
        </el-button>
        <el-button @click="loadTasks" plain>
          <el-icon><Refresh /></el-icon>
        </el-button>
      </div>
    </div>

    <!-- 任务列表 -->
    <div class="tech-card diag-table-card">
      <el-table
        :data="tasks"
        v-loading="loading"
        :row-class-name="tableRowClass"
        @row-click="(row: any) => viewDetail(row.id)"
        highlight-current-row
        style="width: 100%"
      >
        <el-table-column prop="id" label="#" width="60" align="center">
          <template #default="{ row }">
            <span class="cell-id">{{ row.id }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="deviceId" label="设备编码" min-width="200">
          <template #default="{ row }">
            <span class="cell-device-id">{{ row.deviceId }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="triggerType" label="触发方式" width="100" align="center">
          <template #default="{ row }">
            <span :class="['trigger-tag', row.triggerType === 'AUTO' ? 'trigger-auto' : 'trigger-manual']">
              {{ row.triggerType === 'AUTO' ? '自动' : '手动' }}
            </span>
          </template>
        </el-table-column>
        <el-table-column prop="status" label="状态" width="110" align="center">
          <template #default="{ row }">
            <span :class="['status-chip', `chip-${row.status?.toLowerCase()}`]">
              <span class="chip-dot" />
              {{ formatStatus(row.status) }}
            </span>
          </template>
        </el-table-column>
        <el-table-column prop="createdAt" label="时间" min-width="160">
          <template #default="{ row }">
            <span class="cell-time">{{ formatTime(row.createdAt) }}</span>
          </template>
        </el-table-column>
        <el-table-column label="" width="80" align="center">
          <template #default="{ row }">
            <el-button link type="primary" @click.stop="viewDetail(row.id)">
              详情 →
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 诊断详情 -->
    <transition name="slide-up">
      <div v-if="detail" class="tech-card diag-detail">
        <div class="detail-header">
          <div class="detail-title">
            <span class="detail-label">诊断报告</span>
            <span class="detail-id">#{{ detail.id }}</span>
            <span :class="['status-chip', `chip-${detail.status?.toLowerCase()}`]" style="margin-left: 12px">
              <span class="chip-dot" />
              {{ formatStatus(detail.status) }}
            </span>
          </div>
          <el-button link @click="detail = null">
            <el-icon :size="18"><Close /></el-icon>
          </el-button>
        </div>

        <div class="detail-steps">
          <div v-for="log in detail.logs" :key="log.id" class="step-row">
            <div :class="['step-indicator', log.success ? 'ind-pass' : 'ind-fail']">
              {{ log.success ? '✓' : '✗' }}
            </div>
            <div class="step-body">
              <div class="step-top">
                <span class="step-name">{{ log.stepName }}</span>
                <span class="step-ms">{{ log.durationMs }}ms</span>
              </div>
              <div class="step-detail-text">{{ log.detail }}</div>
            </div>
          </div>
        </div>
      </div>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Search, Refresh, Close } from '@element-plus/icons-vue'
import { getDiagnosticTasks, getDiagnosticTask, runDiagnostic } from '../api/diagnostic'
import { unwrapApiPayload, unwrapArrayPayload } from '../utils/apiPayload.js'

const route = useRoute()
const deviceId = ref(route.query.deviceId as string || '')
const tasks = ref<any[]>([])
const detail = ref<any>(null)
const loading = ref(false)
const running = ref(false)

function formatStatus(s: string) {
  const map: Record<string, string> = { COMPLETED: '完成', RUNNING: '运行中', PENDING: '等待', FAILED: '失败' }
  return map[s] || s
}

function formatTime(t: string) {
  if (!t) return '-'
  const d = new Date(t)
  return `${d.getMonth()+1}/${d.getDate()} ${d.getHours().toString().padStart(2,'0')}:${d.getMinutes().toString().padStart(2,'0')}:${d.getSeconds().toString().padStart(2,'0')}`
}

function tableRowClass({ rowIndex }: { rowIndex: number }) {
  return rowIndex % 2 === 0 ? 'row-even' : 'row-odd'
}

async function loadTasks() {
  loading.value = true
  try {
    const res: any = await getDiagnosticTasks(deviceId.value || undefined)
    tasks.value = unwrapArrayPayload(res)
  } finally { loading.value = false }
}

async function runManualDiag() {
  if (!deviceId.value) return
  running.value = true
  try {
    await runDiagnostic(deviceId.value)
    ElMessage.success('诊断任务已创建')
    await loadTasks()
  } finally { running.value = false }
}

async function viewDetail(taskId: number) {
  const res: any = await getDiagnosticTask(taskId)
  detail.value = unwrapApiPayload(res)
}

onMounted(() => loadTasks())
</script>

<style scoped>
.diag-page {
  padding: 20px 24px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

/* 工具栏 */
.diag-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14px 20px;
}
.toolbar-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--tech-text-primary);
  letter-spacing: 0.5px;
}
.toolbar-right {
  display: flex;
  gap: 10px;
  align-items: center;
}
.device-input { width: 240px; }

/* 表格卡片 */
.diag-table-card {
  padding: 0;
  overflow: hidden;
}
.diag-table-card :deep(.el-table) {
  font-size: 13px;
}
.diag-table-card :deep(.el-table th.el-table__cell) {
  font-size: 12px;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--tech-text-muted);
  padding: 10px 0;
}
.diag-table-card :deep(.el-table td.el-table__cell) {
  padding: 12px 0;
}
.diag-table-card :deep(.el-table .row-even) {
  background: var(--tech-bg-card);
}
.diag-table-card :deep(.el-table .row-odd) {
  background: rgba(255,255,255,0.02);
}
.diag-table-card :deep(.el-table__body tr) {
  cursor: pointer;
  transition: background 0.2s;
}
.diag-table-card :deep(.el-table__body tr:hover > td) {
  background: var(--tech-bg-hover) !important;
}

/* 单元格 */
.cell-id {
  font-family: 'JetBrains Mono', monospace;
  color: var(--tech-text-muted);
  font-size: 12px;
}
.cell-device-id {
  font-family: 'JetBrains Mono', monospace;
  font-size: 13px;
  color: var(--tech-text-primary);
}
.cell-time {
  font-family: 'JetBrains Mono', monospace;
  font-size: 12px;
  color: var(--tech-text-secondary);
}

/* 触发标签 */
.trigger-tag {
  display: inline-block;
  padding: 2px 10px;
  border-radius: 10px;
  font-size: 11px;
  font-weight: 500;
  letter-spacing: 0.3px;
}
.trigger-manual {
  background: rgba(0, 212, 255, 0.1);
  color: var(--tech-primary);
  border: 1px solid rgba(0, 212, 255, 0.25);
}
.trigger-auto {
  background: rgba(255, 170, 0, 0.1);
  color: var(--tech-warning);
  border: 1px solid rgba(255, 170, 0, 0.25);
}

/* 状态标签 */
.status-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 3px 10px;
  border-radius: 10px;
  font-size: 12px;
  font-weight: 500;
  white-space: nowrap;
}
.chip-dot {
  width: 6px; height: 6px;
  border-radius: 50%;
  display: inline-block;
}
.chip-completed {
  background: rgba(0, 255, 136, 0.1);
  color: var(--tech-online);
}
.chip-completed .chip-dot { background: var(--tech-online); box-shadow: 0 0 6px var(--tech-online); }
.chip-running {
  background: rgba(0, 212, 255, 0.1);
  color: var(--tech-primary);
}
.chip-running .chip-dot { background: var(--tech-primary); box-shadow: 0 0 6px var(--tech-primary); animation: pulse 1.5s infinite; }
.chip-pending {
  background: rgba(255, 170, 0, 0.1);
  color: var(--tech-warning);
}
.chip-pending .chip-dot { background: var(--tech-warning); }
.chip-failed {
  background: rgba(255, 68, 68, 0.1);
  color: var(--tech-offline);
}
.chip-failed .chip-dot { background: var(--tech-offline); box-shadow: 0 0 6px var(--tech-offline); }

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.4; }
}

/* 详情面板 */
.diag-detail {
  padding: 20px 24px;
}
.detail-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
  padding-bottom: 14px;
  border-bottom: 1px solid var(--tech-border);
}
.detail-title {
  display: flex;
  align-items: center;
  gap: 8px;
}
.detail-label {
  font-size: 15px;
  font-weight: 600;
  color: var(--tech-text-primary);
}
.detail-id {
  font-family: 'JetBrains Mono', monospace;
  font-size: 14px;
  color: var(--tech-primary);
}

/* 步骤列表 */
.detail-steps {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.step-row {
  display: flex;
  gap: 14px;
  padding: 14px 16px;
  background: var(--tech-bg-elevated);
  border-radius: 8px;
  border: 1px solid transparent;
  transition: border-color 0.2s;
}
.step-row:hover {
  border-color: var(--tech-border);
}

.step-indicator {
  width: 28px; height: 28px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  font-weight: 700;
  flex-shrink: 0;
  margin-top: 2px;
}
.ind-pass {
  background: rgba(0, 255, 136, 0.15);
  color: var(--tech-online);
  border: 1px solid rgba(0, 255, 136, 0.3);
}
.ind-fail {
  background: rgba(255, 68, 68, 0.15);
  color: var(--tech-offline);
  border: 1px solid rgba(255, 68, 68, 0.3);
}
.step-body { flex: 1; min-width: 0; }
.step-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
}
.step-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--tech-text-primary);
}
.step-ms {
  font-family: 'JetBrains Mono', monospace;
  font-size: 12px;
  color: var(--tech-primary);
  background: rgba(0, 212, 255, 0.08);
  padding: 2px 8px;
  border-radius: 4px;
}
.step-detail-text {
  font-size: 13px;
  line-height: 1.6;
  color: var(--tech-text-secondary);
  white-space: pre-wrap;
  word-break: break-all;
}

/* 动画 */
.slide-up-enter-active { transition: all 0.3s ease-out; }
.slide-up-leave-active { transition: all 0.2s ease-in; }
.slide-up-enter-from { opacity: 0; transform: translateY(16px); }
.slide-up-leave-to { opacity: 0; transform: translateY(8px); }
</style>
