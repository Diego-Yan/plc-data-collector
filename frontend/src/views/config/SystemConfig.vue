<template>
  <el-card>
    <template #header>系统设置</template>
    <el-form :model="form" label-width="150px">
      <el-form-item label="采集周期(ms)">
        <el-input-number v-model="form.collectInterval" :min="100" :max="60000" />
      </el-form-item>
      <el-form-item label="超时(ms)">
        <el-input-number v-model="form.timeout" :min="100" :max="30000" />
      </el-form-item>
      <el-form-item label="重连间隔(秒)">
        <el-input-number v-model="form.reconnectInterval" :min="1" :max="300" />
      </el-form-item>
      <el-form-item label="日志保留天数">
        <el-input-number v-model="form.logRetentionDays" :min="1" :max="365" />
      </el-form-item>
      <el-form-item>
        <el-button type="primary" @click="save">保存设置</el-button>
      </el-form-item>
    </el-form>
  </el-card>
</template>

<script setup lang="ts">
// TAG: fixed — added ElMessage import
import { reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { configApi } from '@/api'

const form = reactive({ collectInterval: 1000, timeout: 3000, reconnectInterval: 10, logRetentionDays: 30 })

onMounted(async () => {
  try {
    const res = await configApi.getSystem()
    if (res.data) Object.assign(form, res.data)
  } catch {}
})

async function save() {
  // TAG: fixed — stringify values for backend Dictionary<string,string>
  const data: Record<string, string> = {}
  for (const [k, v] of Object.entries(form)) {
    data[k] = String(v)
  }
  await configApi.setSystem(data)
  ElMessage.success('系统设置已保存')
}
</script>
