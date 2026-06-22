<template>
  <div>
    <el-card>
      <template #header>
        <span>数据转发配置</span>
      </template>
      <el-form :model="form" label-width="120px">
        <el-form-item label="转发间隔(秒)">
          <el-input-number v-model="form.interval" :min="1" :max="3600" />
        </el-form-item>
        <el-form-item label="数据库主机">
          <el-input v-model="form.host" />
        </el-form-item>
        <el-form-item label="端口">
          <el-input-number v-model="form.port" :min="1" :max="65535" />
        </el-form-item>
        <el-form-item label="数据库名">
          <el-input v-model="form.database" />
        </el-form-item>
        <el-form-item label="用户名">
          <el-input v-model="form.username" />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="save" :loading="saving">保存配置</el-button>
        </el-form-item>
      </el-form>
      <el-divider />
      <p style="color:#909399;font-size:13px">
        转发服务将定时从实时缓存中聚合各设备的点位快照，以 JSONB 宽表格式写入关系数据库。支持外部系统通过 SQL 直接查询。
      </p>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { configApi } from '@/api'

const form = reactive({ interval: 10, host: '', port: 5432, database: '', username: '' })
const saving = ref(false)

onMounted(async () => {
  try {
    const res = await configApi.getRelational()
    const d = res.data
    form.host = d.host || ''
    form.port = Number(d.port) || 5432
    form.database = d.database || ''
    form.username = d.username || ''
  } catch (e: any) {
    ElMessage.error('加载配置失败: ' + (e?.message || '未知错误'))
  }
})

async function save() {
  saving.value = true
  try {
    await configApi.setRelational({
      host: form.host,
      port: String(form.port),
      database: form.database,
      username: form.username
    })
    ElMessage.success('配置已保存')
  } catch (e: any) {
    ElMessage.error('保存失败: ' + (e?.message || '未知错误'))
  } finally {
    saving.value = false
  }
}
</script>
