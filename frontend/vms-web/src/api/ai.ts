import request from './request'

export function chat(message: string, sessionId?: string) {
  return request.post('/aiagent/chat', { message, sessionId })
}
