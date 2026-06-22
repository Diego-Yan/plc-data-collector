<template>
  <div>
    <el-card>
      <template #header>
        <span>实时监控</span>
        <el-button size="small" style="float:right" @click="toggleAutoRefresh">
          {{ autoRefresh ? '停止刷新' : '自动刷新' }}
        </el-button>
      </template>
      <el-select v-model="selectedDevice" placeholder="选择设备" style="margin-bottom:15px" @change="onDeviceChange">
        <el-option v-for="d in devices" :key="d.id" :label="d.name" :value="d.id" />
      </el-select>
      <el-table :data="realtimeData" stripe v-loading="loading">
        <el-table-column prop="pointId" label="点位ID" width="80" />
        <el-table-column label="数值" width="150">
          <template #default="{ row }">
            <span :style="{ color: row.quality === 0 ? '#67c23a' : '#f56c6c', fontWeight: 'bold' }">
              {{ row.value ?? '--' }}
            </span>
          </template>
        </el-table-column>
        <el-table-column prop="timestamp" label="时间" />
        <el-table-column label="质量" width="80">
          <template #default="{ row }">
            <el-tag :type="row.quality === 0 ? 'success' : 'danger'" size="small">
              {{ row.quality === 0 ? '正常' : '异常' }}
            </el-tag>
          </template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { ElMessage } from 'element-plus'
import { deviceApi, dataApi } from '@/api'

const devices = ref<any[]>([])
const selectedDevice = ref<number | null>(null)
const realtimeData = ref<any[]>([])
const loading = ref(false)
const autoRefresh = ref(false)
let refreshTimer: ReturnType<typeof setInterval> | null = null
let lastErrorTs = 0

onMounted(async () => {
  try {
    const res = await deviceApi.list()
    devices.value = res.data.items || []
  } catch (e: any) {
    ElMessage.error('加载设备列表失败: ' + (e?.message || '未知错误'))
  }
})

onUnmounted(() => {
  stopAutoRefresh()
})

async function onDeviceChange(id: number) {
  selectedDevice.value = id
  lastErrorTs = 0
  await fetchRealtime()
  if (autoRefresh.value) {
    stopAutoRefresh()
    startAutoRefresh()
  }
}

async function fetchRealtime() {
  if (!selectedDevice.value) return
  loading.value = true
  try {
    const res = await dataApi.getRealtime(selectedDevice.value)
    realtimeData.value = res.data || []
    lastErrorTs = 0
  } catch (e: any) {
    const now = Date.now()
    if (now - lastErrorTs > 10000) {
      ElMessage.warning('获取实时数据失败，将在 10 秒后重试')
      lastErrorTs = now
    }
  } finally {
    loading.value = false
  }
}

function toggleAutoRefresh() {
  autoRefresh.value = !autoRefresh.value
  if (autoRefresh.value && selectedDevice.value) {
    lastErrorTs = 0
    startAutoRefresh()
  } else {
    stopAutoRefresh()
  }
}

function startAutoRefresh() {
  if (refreshTimer) return
  refreshTimer = setInterval(fetchRealtime, 1000)
}

function stopAutoRefresh() {
  if (refreshTimer) {
    clearInterval(refreshTimer)
    refreshTimer = null
  }
}
</script>
