import request from './request'

export interface AgentContentItemDto {
  kind: string
  text?: string
  fileName?: string
  mediaType?: string
  base64Data?: string
}

export interface AgentChatRequest {
  conversationId?: string
  deviceId?: string
  clientMessageId?: string
  contentItems: AgentContentItemDto[]
}

export interface AgentExecutionUsage {
  promptTokens: number
  completionTokens: number
  totalTokens: number
}

export interface AgentChatResponse {
  conversationId: string
  messageId: string
  model: string
  contentItems: AgentContentItemDto[]
  toolCalls: string[]
  citations: string[]
  usage: AgentExecutionUsage
}

export interface ApiResponse<T> {
  code: number
  message: string
  data: T
}

export async function chat(payload: AgentChatRequest): Promise<ApiResponse<AgentChatResponse>> {
  const response = await request.post<ApiResponse<AgentChatResponse>>('/AiAgent/chat', payload)
  const normalized = response as unknown as { data?: ApiResponse<AgentChatResponse> }
  return normalized.data ?? (response as unknown as ApiResponse<AgentChatResponse>)
}
