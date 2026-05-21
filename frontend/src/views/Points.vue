<template>
  <div>
    <el-card style="margin-bottom:15px">
      <template #header>设备: {{ device?.name }} — 点位管理</template>
      <el-button type="primary" size="small" @click="showAdd">新增点位</el-button>
      <el-button size="small" @click="batchImport">批量导入</el-button>
    </el-card>
    <el-table :data="points" stripe>
      <el-table-column prop="code" label="编码" width="100" />
      <el-table-column prop="name" label="名称" />
      <el-table-column prop="address" label="地址" width="150" />
      <el-table-column prop="dataType" label="类型" width="80" />
      <el-table-column prop="unit" label="单位" width="60" />
      <el-table-column label="操作" width="160">
        <template #default="{ row }">
          <el-button size="small" @click="edit(row)">编辑</el-button>
          <el-button size="small" type="danger" @click="remove(row.id)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" :title="editing ? '编辑点位' : '新增点位'" width="500px">
      <el-form :model="form" label-width="100px">
        <el-form-item label="编码"><el-input v-model="form.code" /></el-form-item>
        <el-form-item label="名称"><el-input v-model="form.name" /></el-form-item>
        <el-form-item label="地址"><el-input v-model="form.address" placeholder="如 DB1.DBD0" /></el-form-item>
        <el-form-item label="数据类型">
          <el-select v-model="form.dataType">
            <el-option label="Real" value="Real" />
            <el-option label="Int" value="Int" />
            <el-option label="Bool" value="Bool" />
            <el-option label="Word" value="Word" />
          </el-select>
        </el-form-item>
        <el-form-item label="单位"><el-input v-model="form.unit" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="save">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
// TAG: fixed — added ElMessage import
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { deviceApi, pointApi } from '@/api'

const route = useRoute()
const deviceId = Number(route.params.id)
const device = ref<any>(null)
const points = ref<any[]>([])
const dialogVisible = ref(false)
const editing = ref(false)
// TAG: fixed — added ElMessage import, id to form type
const form = ref({ id: 0, code: '', name: '', address: 'DB1.DBD0', dataType: 'Real', unit: '' })

onMounted(async () => {
  const devRes = await deviceApi.get(deviceId)
  device.value = devRes.data
  const ptRes = await pointApi.list(deviceId)
  points.value = ptRes.data || []
})

function showAdd() {
  editing.value = false
  form.value = { id: 0, code: '', name: '', address: 'DB1.DBD0', dataType: 'Real', unit: '' }
  dialogVisible.value = true
}

function edit(row: any) {
  editing.value = true
  form.value = { ...row }
  dialogVisible.value = true
}

async function save() {
  if (editing.value) {
    await pointApi.update(form.value.id, form.value)
  } else {
    await pointApi.create(deviceId, form.value)
  }
  dialogVisible.value = false
  const res = await pointApi.list(deviceId)
  points.value = res.data || []
}

async function remove(id: number) {
  await pointApi.delete(id)
  points.value = points.value.filter((p: any) => p.id !== id)
}

async function batchImport() {
  await pointApi.batchImport(deviceId, [])
  ElMessage.info('批量导入功能需要在后端实现')
}
</script>
