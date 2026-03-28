import request from './request'

export function getDiagnosticTasks(deviceId?: string, limit = 20) {
  return request.get('/diagnostic/tasks', { params: { deviceId, limit } })
}

export function getDiagnosticTask(taskId: number) {
  return request.get(`/diagnostic/tasks/${taskId}`)
}

export function getDiagnosticLogs(deviceId: string, limit = 50) {
  return request.get('/diagnostic/logs', { params: { deviceId, limit } })
}

export function runDiagnostic(deviceId: string) {
  return request.post(`/diagnostic/run/${deviceId}`)
}
