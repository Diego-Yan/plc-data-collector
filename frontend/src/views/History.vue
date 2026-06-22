<template>
  <div>
    <el-card>
      <template #header>历史数据查询</template>
      <el-form :model="query" inline>
        <el-form-item label="设备">
          <el-select v-model="query.deviceId" placeholder="选择设备" @change="onDeviceChange">
            <el-option v-for="d in devices" :key="d.id" :label="d.name" :value="d.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="开始">
          <el-date-picker v-model="query.from" type="datetime" />
        </el-form-item>
        <el-form-item label="结束">
          <el-date-picker v-model="query.to" type="datetime" />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="search" :loading="searching">查询</el-button>
        </el-form-item>
      </el-form>
      <el-table :data="historyData" stripe height="400" v-loading="searching">
        <el-table-column prop="pointId" label="点位" width="80" />
        <el-table-column prop="value" label="数值" width="120" />
        <el-table-column prop="timestamp" label="时间" width="180" />
        <el-table-column prop="quality" label="质量" width="80">
          <template #default="{ row }">
            <el-tag :type="row.quality === 0 ? 'success' : 'danger'" size="small">
              {{ row.quality === 0 ? '正常' : '异常' }}
            </el-tag>
          </template>
        </el-table-column>
      </el-table>
      <el-button style="margin-top:15px" @click="exportExcel">导出 Excel</el-button>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, reactive } from 'vue'
import { ElMessage } from 'element-plus'
import { deviceApi, dataApi, pointApi } from '@/api'

const devices = ref<any[]>([])
const historyData = ref<any[]>([])
const searching = ref(false)

interface HistoryQuery {
  deviceId: number | null
  from: Date | null
  to: Date | null
}

const query = reactive<HistoryQuery>({ deviceId: null, from: null, to: null })

onMounted(async () => {
  try {
    const res = await deviceApi.list()
    devices.value = res.data.items || []
  } catch (e: any) {
    ElMessage.error('加载设备列表失败: ' + (e?.message || '未知错误'))
  }
})

async function onDeviceChange(deviceId: number) {
  query.deviceId = deviceId
}

async function search() {
  if (!query.deviceId) {
    ElMessage.warning('请选择设备')
    return
  }
  searching.value = true
  historyData.value = []
  try {
    const pts = await pointApi.list(query.deviceId)
    const pointIdArr = (pts.data || []).map((p: any) => p.id)

    if (pointIdArr.length === 0) {
      ElMessage.info('该设备下无点位')
      searching.value = false
      return
    }

    const res = await dataApi.getHistory(query.deviceId, {
      pointIds: pointIdArr.join(','),
      from: query.from?.toISOString(),
      to: query.to?.toISOString(),
    })
    historyData.value = res.data || []

    if (historyData.value.length === 0)
      ElMessage.info('查询结果为空')
  } catch (e: any) {
    ElMessage.error('查询失败: ' + (e?.message || '未知错误'))
  } finally {
    searching.value = false
  }
}

function exportExcel() {
  ElMessage.info('Excel 导出功能需要在后端实现')
}
</script>
