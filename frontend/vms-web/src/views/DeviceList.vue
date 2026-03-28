<template>
  <div class="device-list">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>设备列表</span>
          <el-select v-model="statusFilter" placeholder="全部状态" clearable style="width: 140px" @change="loadDevices">
            <el-option label="在线" value="Online" />
            <el-option label="离线" value="Offline" />
          </el-select>
        </div>
      </template>

      <el-table :data="deviceStore.devices" v-loading="deviceStore.loading" stripe>
        <el-table-column prop="id" label="设备编号" width="200" />
        <el-table-column prop="name" label="名称" />
        <el-table-column prop="remoteIp" label="IP 地址" width="140" />
        <el-table-column prop="manufacturer" label="厂商" width="100" />
        <el-table-column prop="status" label="状态" width="90">
          <template #default="{ row }">
            <el-tag :type="row.status === 'Online' ? 'success' : 'danger'" size="small">
              {{ row.status === 'Online' ? '在线' : '离线' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="lastKeepaliveAt" label="最后心跳" width="180" />
        <el-table-column label="操作" width="200">
          <template #default="{ row }">
            <el-button size="small" type="primary" @click="goPreview(row.id)">预览</el-button>
            <el-button size="small" type="warning" @click="goDiagnostic(row.id)">诊断</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useDeviceStore } from '../stores/device'

const deviceStore = useDeviceStore()
const router = useRouter()
const statusFilter = ref('')

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
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
</style>
