<template>
  <div>
    <el-row :gutter="20" style="margin-bottom:20px">
      <el-col :span="6"><el-card><h3>{{ stats.total }}</h3><p>总设备数</p></el-card></el-col>
      <el-col :span="6"><el-card><h3 style="color:#67c23a">{{ stats.online }}</h3><p>在线</p></el-card></el-col>
      <el-col :span="6"><el-card><h3 style="color:#f56c6c">{{ stats.offline }}</h3><p>离线</p></el-card></el-col>
      <el-col :span="6"><el-card><h3>{{ stats.points }}</h3><p>点位总数</p></el-card></el-col>
    </el-row>
    <el-card>
      <template #header><span>设备列表</span></template>
      <el-table :data="devices" stripe>
        <el-table-column prop="id" label="ID" width="60" />
        <el-table-column prop="name" label="名称" />
        <el-table-column prop="ipAddress" label="IP地址" />
        <el-table-column label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="row.isOnline ? 'success' : 'danger'">{{ row.isOnline ? '在线' : '离线' }}</el-tag>
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
// TAG: fixed — added ElMessage import
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { deviceApi } from '@/api'

const devices = ref<any[]>([])
const stats = ref({ total: 0, online: 0, offline: 0, points: 0 })

async function load() {
  const res = await deviceApi.list()
  devices.value = res.data.items || []
  stats.value.total = res.data.total || 0
  stats.value.online = devices.value.filter((d: any) => d.isOnline).length
  stats.value.offline = devices.value.filter((d: any) => !d.isOnline).length
}

async function reconnect(id: number) {
  await deviceApi.reconnect(id)
  ElMessage.success('重连已触发')
}

onMounted(load)
</script>
