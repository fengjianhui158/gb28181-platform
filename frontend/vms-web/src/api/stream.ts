import request from './request'

export function playStream(deviceId: string, channelId: string) {
  return request.post('/stream/play', { deviceId, channelId })
}

export function stopStream(deviceId: string, channelId: string) {
  return request.post('/stream/stop', { deviceId, channelId })
}
