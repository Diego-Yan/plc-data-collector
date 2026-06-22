<template>
  <div>
    <el-row :gutter="20" style="margin-bottom:20px">
      <el-col :span="6"><el-card><h3>{{ stats.total }}</h3><p>总设备数</p></el-card></el-col>
      <el-col :span="6"><el-card><h3 style="color:#67c23a">{{ stats.online }}</h3><p>在线</p></el-card></el-col>
      <el-col :span="6"><el-card><h3 style="color:#f56c6c">{{ stats.offline }}</h3><p>离线</p></el-card></el-col>
      <el-col :span="6"><el-card><h3>{{ stats.points }}</h3><p>点位总数</p></el-card></el-col>
    </el-row>
    <el-card>
      <template #header>
        <span>设备列表</span>
        <el-button size="small" style="float:right" @click="load" :loading="loading">刷新</el-button>
      </template>
      <el-table :data="devices" stripe v-loading="loading">
        <el-table-column prop="id" label="ID" width="60" />
        <el-table-column prop="name" label="名称" />
        <el-table-column prop="ipAddress" label="IP地址" />
        <el-table-column label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="row.isOnline ? 'success' : 'danger'">{{ row.isOnline ? '在线' : '离线' }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="点位" width="60">
          <template #default="{ row }">
            {{ pointCounts[row.id] ?? '?' }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="200">
          <template #default="{ row }">
            <el-button size="small" @click="$router.push(`/devices/${row.id}/points`)">点位</el-button>
            <el-button size="small" type="primary" @click="reconnect(row.id)">重连</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { deviceApi, pointApi } from '@/api'

const devices = ref<any[]>([])
const pointCounts = ref<Record<number, number>>({})
const stats = ref({ total: 0, online: 0, offline: 0, points: 0 })
const loading = ref(false)

async function load() {
  loading.value = true
  try {
    const res = await deviceApi.list()
    devices.value = res.data.items || []
    stats.value.total = res.data.total || 0
    stats.value.online = devices.value.filter((d: any) => d.isOnline).length
    stats.value.offline = devices.value.filter((d: any) => !d.isOnline).length

    let totalPoints = 0
    const counts: Record<number, number> = {}
    for (const device of devices.value) {
      try {
        const pts = await pointApi.list(device.id)
        const count = (pts.data || []).length
        counts[device.id] = count
        totalPoints += count
      } catch {
        counts[device.id] = 0
      }
    }
    pointCounts.value = counts
    stats.value.points = totalPoints
  } catch (e: any) {
    ElMessage.error('加载设备列表失败: ' + (e?.message || '未知错误'))
  } finally {
    loading.value = false
  }
}

async function reconnect(id: number) {
  try {
    await deviceApi.reconnect(id)
    ElMessage.success('重连已触发')
  } catch (e: any) {
    ElMessage.error('重连失败: ' + (e?.message || '未知错误'))
  }
}

onMounted(load)
</script>
