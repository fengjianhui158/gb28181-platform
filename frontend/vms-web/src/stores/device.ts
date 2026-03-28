import { defineStore } from 'pinia'
import { ref } from 'vue'
import { getDevices } from '../api/device'

export interface Device {
  id: string
  name: string
  manufacturer: string
  model: string
  remoteIp: string
  remotePort: number
  webPort: number
  status: string
  lastRegisterAt: string
  lastKeepaliveAt: string
}

export const useDeviceStore = defineStore('device', () => {
  const devices = ref<Device[]>([])
  const loading = ref(false)

  async function fetchDevices(status?: string) {
    loading.value = true
    try {
      const res: any = await getDevices(status)
      devices.value = res.data || []
    } finally {
      loading.value = false
    }
  }

  function updateStatus(deviceId: string, status: string) {
    const device = devices.value.find(d => d.id === deviceId)
    if (device) {
      device.status = status
    }
  }

  return { devices, loading, fetchDevices, updateStatus }
})
