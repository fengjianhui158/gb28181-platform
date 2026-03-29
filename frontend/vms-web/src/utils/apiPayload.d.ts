export function unwrapApiPayload<T>(response: T | { data: T } | null | undefined): T | null | undefined
export function unwrapArrayPayload<T>(response: T[] | { data: T[] } | null | undefined): T[]
