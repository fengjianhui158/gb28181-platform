<template>
  <div class="diagnostic-panel">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>诊断面板</span>
          <el-button type="primary" @click="runManualDiag" :disabled="!deviceId">手动诊断</el-button>
        </div>
      </template>

      <el-table :data="tasks" stripe>
        <el-table-column prop="id" label="任务ID" width="80" />
        <el-table-column prop="deviceId" label="设备编号" width="200" />
        <el-table-column prop="triggerType" label="触发方式" width="100" />
        <el-table-column prop="status" label="状态" width="120">
          <template #default="{ row }">
            <el-tag :type="row.status === 'COMPLETED' ? 'success' : row.status === 'FAILED' ? 'danger' : 'warning'" size="small">
              {{ row.status }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="conclusion" label="诊断结论" />
        <el-table-column prop="createdAt" label="创建时间" width="180" />
      </el-table>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { getDiagnosticTasks, runDiagnostic } from '../api/diagnostic'

const route = useRoute()
const deviceId = ref(route.query.deviceId as string || '')
const tasks = ref<any[]>([])

async function loadTasks() {
  const res: any = await getDiagnosticTasks(deviceId.value || undefined)
  tasks.value = res.data || []
}

async function runManualDiag() {
  if (!deviceId.value) return
  await runDiagnostic(deviceId.value)
  ElMessage.success('诊断任务已创建')
  await loadTasks()
}

onMounted(() => loadTasks())
</script>

<style scoped>
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
</style>
