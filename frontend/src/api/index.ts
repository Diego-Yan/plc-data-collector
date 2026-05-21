import axios from 'axios'

const api = axios.create({
  baseURL: '/api',
  timeout: 10000,
})

export const deviceApi = {
  list: (params?: any) => api.get('/devices', { params }),
  get: (id: number) => api.get(`/devices/${id}`),
  create: (data: any) => api.post('/devices', data),
  update: (id: number, data: any) => api.put(`/devices/${id}`, data),
  delete: (id: number) => api.delete(`/devices/${id}`),
  reconnect: (id: number) => api.post(`/devices/${id}/reconnect`),
  setStatus: (id: number, enabled: boolean) => api.put(`/devices/${id}/status`, { enabled }),
}

export const pointApi = {
  list: (deviceId: number) => api.get(`/devices/${deviceId}/points`),
  create: (deviceId: number, data: any) => api.post(`/devices/${deviceId}/points`, data),
  update: (id: number, data: any) => api.put(`/points/${id}`, data),
  delete: (id: number) => api.delete(`/points/${id}`),
  // TAG: fixed — route now matches backend /api/devices/{deviceId}/points/batch
  batchImport: (deviceId: number, data: any[]) => api.post(`/devices/${deviceId}/points/batch`, data),
  batchDelete: (ids: number[]) => api.post('/points/batch-delete', ids),
}

export const dataApi = {
  getRealtime: (deviceId: number) => api.get(`/devices/${deviceId}/realtime`),
  getPointRealtime: (pointId: number) => api.get(`/points/${pointId}/realtime`),
  getHistory: (deviceId: number, params: any) => api.get(`/devices/${deviceId}/history`, { params }),
}

export const configApi = {
  // TAG: removed — Redis config no longer needed (replaced by MemoryCache)
  getTimeScaleDb: () => api.get('/config/timescaledb'),
  setTimeScaleDb: (data: any) => api.put('/config/timescaledb', data),
  getRelational: () => api.get('/config/relational'),
  setRelational: (data: any) => api.put('/config/relational', data),
  getSystem: () => api.get('/config/system'),
  setSystem: (data: any) => api.put('/config/system', data),
}

// TAG: removed logApi — no backend /api/logs endpoint exists
export default api
