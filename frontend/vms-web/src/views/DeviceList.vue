<template>
  <div class="device-page animate-fade-in-up">
    <!-- 统计卡片 -->
    <div class="stats-row">
      <div class="tech-card stat-card">
        <div class="stat-label">设备总数</div>
        <div class="tech-metric">{{ deviceStore.devices.length }}</div>
      </div>
      <div class="tech-card stat-card">
        <div class="stat-label">在线</div>
        <div class="tech-metric" style="color: var(--tech-online)">{{ onlineCount }}</div>
      </div>
      <div class="tech-card stat-card">
        <div class="stat-label">离线</div>
        <div class="tech-metric" style="color: var(--tech-offline)">{{ offlineCount }}</div>
      </div>
    </div>

    <!-- 筛选栏 -->
    <div class="tech-card filter-bar">
      <el-select v-model="statusFilter" placeholder="状态筛选" clearable @change="loadDevices">
        <el-option label="在线" value="Online" />
        <el-option label="离线" value="Offline" />
      </el-select>
      <el-input v-model="searchText" placeholder="搜索设备ID/名称/IP" clearable class="search-input" />
    </div>

    <!-- 设备表格 -->
    <div class="tech-card" style="padding: 0">
      <el-table :data="filteredDevices" v-loading="deviceStore.loading" stripe>
        <el-table-column prop="id" label="设备编码" width="220">
          <template #default="{ row }">
            <span class="tech-mono">{{ row.id }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="name" label="名称" />
        <el-table-column prop="remoteIp" label="IP 地址">
          <template #default="{ row }">
            <span class="tech-mono">{{ row.remoteIp }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="manufacturer" label="厂商" width="100" />
        <el-table-column prop="status" label="状态" width="100">
          <template #default="{ row }">
            <span class="status-indicator">
              <span :class="['status-dot', row.status === 'Online' ? 'status-dot--online' : 'status-dot--offline']" />
              {{ row.status === 'Online' ? '在线' : '离线' }}
            </span>
          </template>
        </el-table-column>
        <el-table-column prop="lastKeepaliveAt" label="最后心跳" width="180">
          <template #default="{ row }">
            <span class="tech-mono">{{ row.lastKeepaliveAt || '-' }}</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="200">
          <template #default="{ row }">
            <el-button size="small" @click="goPreview(row.id)">预览</el-button>
            <el-button size="small" @click="goDiagnostic(row.id)">诊断</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useDeviceStore } from '../stores/device'

const deviceStore = useDeviceStore()
const router = useRouter()
const statusFilter = ref('')
const searchText = ref('')

const onlineCount = computed(() =>
  deviceStore.devices.filter(d => d.status === 'Online').length
)

const offlineCount = computed(() =>
  deviceStore.devices.filter(d => d.status !== 'Online').length
)

const filteredDevices = computed(() => {
  if (!searchText.value) return deviceStore.devices
  const q = searchText.value.toLowerCase()
  return deviceStore.devices.filter(d =>
    d.id?.toLowerCase().includes(q) ||
    d.name?.toLowerCase().includes(q) ||
    d.remoteIp?.toLowerCase().includes(q)
  )
})

function loadDevices() {
  deviceStore.fetchDevices(statusFilter.value || undefined)
}

function goPreview(deviceId: string) {
  router.push({ name: 'LivePreview', query: { deviceId } })
}

function goDiagnostic(deviceId: string) {
  router.push({ name: 'DiagnosticPanel', query: { deviceId } })
}

onMounted(() => loadDevices())
</script>

<style scoped>
.device-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.stats-row {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
}

.stat-card {
  padding: 20px 24px;
  text-align: center;
}

.stat-label {
  color: var(--tech-text-secondary);
  font-size: 14px;
  margin-bottom: 8px;
}

.filter-bar {
  display: flex;
  gap: 12px;
  padding: 16px;
  align-items: center;
}

.search-input {
  max-width: 300px;
}

.status-indicator {
  display: flex;
  align-items: center;
  gap: 6px;
}
</style>
