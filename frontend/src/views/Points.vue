<template>
  <div>
    <el-row style="margin-bottom:15px">
      <el-button type="primary" @click="showDialog()">新增点位</el-button>
      <el-button @click="batchImport">批量导入</el-button>
    </el-row>
    <el-table :data="points" stripe v-loading="loading">
      <el-table-column prop="id" label="ID" width="60" />
      <el-table-column prop="code" label="编码" width="120" />
      <el-table-column prop="name" label="名称" />
      <el-table-column prop="address" label="地址" width="140" />
      <el-table-column prop="dataType" label="类型" width="80" />
      <el-table-column prop="unit" label="单位" width="80" />
      <el-table-column label="操作" width="160">
        <template #default="{ row }">
          <el-button size="small" @click="showDialog(row)">编辑</el-button>
          <el-button size="small" type="danger" @click="remove(row.id)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" :title="editing ? '编辑点位' : '新增点位'" width="500px">
      <el-form :model="form" label-width="80px">
        <el-form-item label="编码" required>
          <el-input v-model="form.code" placeholder="如 pressure_01" />
        </el-form-item>
        <el-form-item label="名称">
          <el-input v-model="form.name" placeholder="如 压力传感器1" />
        </el-form-item>
        <el-form-item label="地址" required>
          <el-input v-model="form.address" placeholder="如 DB1.DBD0" />
        </el-form-item>
        <el-form-item label="类型">
          <el-select v-model="form.dataType">
            <el-option v-for="t in dataTypes" :key="t" :label="t" :value="t" />
          </el-select>
        </el-form-item>
        <el-form-item label="单位">
          <el-input v-model="form.unit" placeholder="如 MPa" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="save" :loading="saving">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { pointApi } from '@/api'

const route = useRoute()
const deviceId = Number(route.params.id)

const points = ref<any[]>([])
const dialogVisible = ref(false)
const editing = ref(false)
const loading = ref(false)
const saving = ref(false)
const dataTypes = ['Bool', 'Byte', 'Word', 'DWord', 'Int', 'DInt', 'Real']
const form = ref({ id: 0, code: '', name: '', address: 'DB1.DBD0', dataType: 'Real', unit: '' })

onMounted(load)

async function load() {
  loading.value = true
  try {
    const res = await pointApi.list(deviceId)
    points.value = res.data || []
  } catch (e: any) {
    ElMessage.error('加载点位列表失败: ' + (e?.message || '未知错误'))
  } finally {
    loading.value = false
  }
}

function showDialog(point?: any) {
  if (point) {
    editing.value = true
    form.value = { ...point }
  } else {
    editing.value = false
    form.value = { id: 0, code: '', name: '', address: 'DB1.DBD0', dataType: 'Real', unit: '' }
  }
  dialogVisible.value = true
}

async function save() {
  if (!form.value.code || !form.value.address) {
    ElMessage.warning('请填写点位编码和地址')
    return
  }
  saving.value = true
  try {
    if (editing.value) {
      await pointApi.update(form.value.id, form.value)
      ElMessage.success('点位已更新')
    } else {
      await pointApi.create(deviceId, form.value)
      ElMessage.success('点位已创建')
    }
    dialogVisible.value = false
    await load()
  } catch (e: any) {
    ElMessage.error('保存失败: ' + (e?.message || '未知错误'))
  } finally {
    saving.value = false
  }
}

async function remove(id: number) {
  try {
    await pointApi.delete(id)
    points.value = points.value.filter((p: any) => p.id !== id)
    ElMessage.success('点位已删除')
  } catch (e: any) {
    ElMessage.error('删除失败: ' + (e?.message || '未知错误'))
  }
}

function batchImport() {
  ElMessage.info('批量导入功能: 请通过 API POST /api/devices/{id}/points/batch 提交 JSON 数组')
}
</script>
