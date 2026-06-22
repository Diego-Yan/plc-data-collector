import { defineStore } from 'pinia'
import { ref } from 'vue'

const RECONNECT_DELAY_MS = 3000

export const useWebSocketStore = defineStore('websocket', () => {
  const ws = ref<WebSocket | null>(null)
  const connected = ref(false)
  let handlers = new Map<string, (data: any) => void>()
  let reconnectTimer: ReturnType<typeof setTimeout> | null = null
  let currentUrl: string = ''

  function clearTimer() {
    if (reconnectTimer) {
      clearTimeout(reconnectTimer)
      reconnectTimer = null
    }
  }

  function connect(url: string) {
    clearTimer()
    currentUrl = url

    if (ws.value?.readyState === WebSocket.OPEN || ws.value?.readyState === WebSocket.CONNECTING)
      return

    if (ws.value) {
      try { ws.value.close() } catch {}
    }

    ws.value = new WebSocket(url)

    ws.value.onopen = () => {
      connected.value = true
      clearTimer()
    }

    ws.value.onclose = () => {
      connected.value = false
      if (currentUrl) {
        reconnectTimer = setTimeout(() => connect(currentUrl), RECONNECT_DELAY_MS)
      }
    }

    ws.value.onerror = () => {}

    ws.value.onmessage = (event) => {
      try {
        const data = JSON.parse(event.data)
        handlers.forEach((handler) => handler(data))
      } catch {}
    }
  }

  function disconnect() {
    clearTimer()
    currentUrl = ''
    try { ws.value?.close() } catch {}
    ws.value = null
    connected.value = false
  }

  function subscribe(deviceId: string, pointIds: string[]) {
    if (ws.value?.readyState === WebSocket.OPEN) {
      ws.value.send(JSON.stringify({
        type: 'subscribe', deviceId, pointIds,
      }))
    }
  }

  function unsubscribe(deviceId: string, pointIds: string[]) {
    if (ws.value?.readyState === WebSocket.OPEN) {
      ws.value.send(JSON.stringify({
        type: 'unsubscribe', deviceId, pointIds,
      }))
    }
  }

  function onMessage(handler: (data: any) => void) {
    const key = `handler_${Date.now()}_${Math.random().toString(36).slice(2)}`
    handlers.set(key, handler)
    return () => handlers.delete(key)
  }

  return { ws, connected, connect, disconnect, subscribe, unsubscribe, onMessage }
})
