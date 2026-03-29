export function unwrapApiPayload(response) {
  if (response == null) {
    return response
  }

  if (typeof response === 'object' && 'data' in response) {
    return response.data
  }

  return response
}

export function unwrapArrayPayload(response) {
  const payload = unwrapApiPayload(response)
  return Array.isArray(payload) ? payload : []
}
