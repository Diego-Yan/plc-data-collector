<template>
  <div>
    <el-card>
      <template #header>实时监控</template>
      <el-select v-model="selectedDevice" placeholder="选择设备" style="margin-bottom:15px" @change="onDeviceChange">
        <el-option v-for="d in devices" :key="d.id" :label="d.name" :value="d.id" />
      </el-select>
      <el-table :data="realtimeData" stripe>
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
import { ref, onMounted } from 'vue'
import { deviceApi, dataApi } from '@/api'

const devices = ref<any[]>([])
const selectedDevice = ref<number | null>(null)
const realtimeData = ref<any[]>([])

onMounted(async () => {
  const res = await deviceApi.list()
  devices.value = res.data.items || []
})

async function onDeviceChange(id: number) {
  selectedDevice.value = id
  const res = await dataApi.getRealtime(id)
  realtimeData.value = res.data || []
}
</script>
