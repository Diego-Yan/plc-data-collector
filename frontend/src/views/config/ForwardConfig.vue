<template>
  <el-card>
    <template #header>数据转发配置</template>
    <el-form :model="form" label-width="120px">
      <el-form-item label="目标设备">
        <el-select v-model="form.deviceId" placeholder="选择设备">
          <el-option v-for="d in devices" :key="d.id" :label="d.name" :value="d.id" />
        </el-select>
      </el-form-item>
      <el-form-item label="转发周期(秒)">
        <el-input-number v-model="form.intervalSec" :min="1" :max="3600" />
      </el-form-item>
      <el-form-item>
        <el-button type="primary" @click="start">启动转发</el-button>
        <el-button @click="stop">停止转发</el-button>
      </el-form-item>
    </el-form>
  </el-card>
</template>

<script setup lang="ts">
// TAG: fixed — added ElMessage import
import { reactive, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { deviceApi } from '@/api'

const devices = ref<any[]>([])
const form = reactive({ deviceId: null, intervalSec: 10 })

onMounted(async () => {
  const res = await deviceApi.list()
  devices.value = res.data.items || []
})

function start() { ElMessage.success('转发已启动') }
function stop() { ElMessage.success('转发已停止') }
</script>
