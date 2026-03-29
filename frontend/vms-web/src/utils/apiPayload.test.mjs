import assert from 'node:assert/strict'

import { unwrapApiPayload, unwrapArrayPayload } from './apiPayload.js'

const payload = { success: true, webRtcUrl: 'http://127.0.0.1/index/api/webrtc' }
assert.deepEqual(unwrapApiPayload(payload), payload)

const legacyPayload = { data: { id: 1, status: 'COMPLETED' } }
assert.deepEqual(unwrapApiPayload(legacyPayload), legacyPayload.data)

assert.deepEqual(unwrapArrayPayload({ success: true }), [])
