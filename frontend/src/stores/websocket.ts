import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useWebSocketStore = defineStore('websocket', () => {
  const ws = ref<WebSocket | null>(null)
  const connected = ref(false)
  let handlers: Map<string, (data: any) => void> = new Map()
  let reconnectTimer: ReturnType<typeof setTimeout> | null = null

  function connect(url: string) {
    if (ws.value?.readyState === WebSocket.OPEN) return
    ws.value = new WebSocket(url)

    ws.value.onopen = () => {
      connected.value = true
      if (reconnectTimer) { clearTimeout(reconnectTimer); reconnectTimer = null }
    }

    ws.value.onclose = () => {
      connected.value = false
      reconnectTimer = setTimeout(() => connect(url), 3000)
    }

    ws.value.onmessage = (event) => {
      try {
        const data = JSON.parse(event.data)
        handlers.forEach((handler) => handler(data))
      } catch { }
    }
  }

  function disconnect() {
    ws.value?.close()
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
    const key = `handler_${Date.now()}`
    handlers.set(key, handler)
    return () => handlers.delete(key)
  }

  return { ws, connected, connect, disconnect, subscribe, unsubscribe, onMessage }
})
