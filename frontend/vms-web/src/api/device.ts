import request from './request'

export function getDevices(status?: string) {
  return request.get('/device', { params: { status } })
}

export function getDevice(id: string) {
  return request.get(`/device/${id}`)
}

export function getChannels(deviceId: string) {
  return request.get(`/device/${deviceId}/channels`)
}

export function updateDevice(id: string, data: any) {
  return request.put(`/device/${id}`, data)
}

export function deleteDevice(id: string) {
  return request.delete(`/device/${id}`)
}
