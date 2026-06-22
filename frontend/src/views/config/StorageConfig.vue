<template>
  <div>
    <el-card><template #header>存储配置</template>
      <el-tabs>
        <el-tab-pane label="时序库 (TimescaleDB)">
          <el-form :model="tsForm" label-width="120px">
            <el-form-item label="主机"><el-input v-model="tsForm.host" /></el-form-item>
            <el-form-item label="端口"><el-input v-model="tsForm.port" /></el-form-item>
            <el-form-item label="数据库"><el-input v-model="tsForm.database" /></el-form-item>
            <el-form-item><el-button type="primary" @click="saveTs" :loading="savingTs">保存</el-button></el-form-item>
          </el-form>
        </el-tab-pane>
        <el-tab-pane label="关系库">
          <el-form :model="relForm" label-width="120px">
            <el-form-item label="主机"><el-input v-model="relForm.host" /></el-form-item>
            <el-form-item label="端口"><el-input v-model="relForm.port" /></el-form-item>
            <el-form-item label="数据库"><el-input v-model="relForm.database" /></el-form-item>
            <el-form-item><el-button type="primary" @click="saveRel" :loading="savingRel">保存</el-button></el-form-item>
          </el-form>
        </el-tab-pane>
      </el-tabs>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { configApi } from '@/api'

const tsForm = reactive({ host: '127.0.0.1', port: '5432', database: 'plc_data' })
const relForm = reactive({ host: '127.0.0.1', port: '5432', database: 'plc_data_forward' })
const savingTs = ref(false)
const savingRel = ref(false)

onMounted(async () => {
  try { const r = await configApi.getTimeScaleDb(); Object.assign(tsForm, r.data) }
  catch (e: any) { ElMessage.error('加载时序库配置失败: ' + (e?.message || '未知错误')) }
  try { const r = await configApi.getRelational(); Object.assign(relForm, r.data) }
  catch (e: any) { ElMessage.error('加载关系库配置失败: ' + (e?.message || '未知错误')) }
})

async function saveTs() {
  if (savingTs.value) return
  savingTs.value = true
  try { await configApi.setTimeScaleDb(tsForm); ElMessage.success('时序库配置已保存') }
  catch (e: any) { ElMessage.error('保存失败: ' + (e?.message || '未知错误')) }
  finally { savingTs.value = false }
}

async function saveRel() {
  if (savingRel.value) return
  savingRel.value = true
  try { await configApi.setRelational(relForm); ElMessage.success('关系库配置已保存') }
  catch (e: any) { ElMessage.error('保存失败: ' + (e?.message || '未知错误')) }
  finally { savingRel.value = false }
}
</script>
