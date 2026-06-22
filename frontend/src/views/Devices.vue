<template>
  <div>
    <el-row style="margin-bottom:15px">
      <el-button type="primary" @click="showDialog()">新增设备</el-button>
    </el-row>
    <el-table :data="devices" stripe v-loading="loading">
      <el-table-column prop="id" label="ID" width="60" />
      <el-table-column prop="name" label="名称" />
      <el-table-column prop="ipAddress" label="IP地址" width="130" />
      <el-table-column prop="port" label="端口" width="70" />
      <el-table-column label="状态" width="90">
        <template #default="{ row }">
          <el-tag :type="row.isOnline ? 'success' : 'danger'" size="small">{{ row.isOnline ? '在线' : '离线' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="280">
        <template #default="{ row }">
          <el-button size="small" @click="showDialog(row)">编辑</el-button>
          <el-button size="small" @click="$router.push(`/devices/${row.id}/points`)">点位</el-button>
          <el-button size="small" type="primary" @click="reconnect(row.id)">重连</el-button>
          <el-button size="small" type="danger" @click="remove(row.id)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" :title="editing ? '编辑设备' : '新增设备'" width="500px">
      <el-form :model="form" label-width="100px">
        <el-form-item label="名称" required>
          <el-input v-model="form.name" placeholder="请输入设备名称" />
        </el-form-item>
        <el-form-item label="IP地址" required>
          <el-input v-model="form.ipAddress" placeholder="192.168.0.1" />
        </el-form-item>
        <el-form-item label="端口">
          <el-input-number v-model="form.port" :min="1" :max="65535" />
        </el-form-item>
        <el-form-item label="机架/槽位">
          <el-input-number v-model="form.rack" :min="0" /> / <el-input-number v-model="form.slot" :min="0" />
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
import { ElMessage } from 'element-plus'
import { deviceApi } from '@/api'

const devices = ref<any[]>([])
const dialogVisible = ref(false)
const editing = ref(false)
const loading = ref(false)
const saving = ref(false)
const form = ref({ id: 0, name: '', ipAddress: '192.168.0.1', port: 102, rack: 0, slot: 1 })

async function loadDevices() {
  loading.value = true
  try {
    const res = await deviceApi.list()
    devices.value = res.data.items || []
  } catch (e: any) {
    ElMessage.error('加载设备列表失败: ' + (e?.message || '未知错误'))
  } finally {
    loading.value = false
  }
}

onMounted(loadDevices)

function showDialog(device?: any) {
  if (device) {
    editing.value = true
    form.value = { ...device }
  } else {
    editing.value = false
    form.value = { id: 0, name: '', ipAddress: '192.168.0.1', port: 102, rack: 0, slot: 1 }
  }
  dialogVisible.value = true
}

async function save() {
  if (!form.value.name || !form.value.ipAddress) {
    ElMessage.warning('请填写设备名称和IP地址')
    return
  }
  saving.value = true
  try {
    if (editing.value) {
      await deviceApi.update(form.value.id, form.value)
      ElMessage.success('设备已更新')
    } else {
      await deviceApi.create(form.value)
      ElMessage.success('设备已创建')
    }
    dialogVisible.value = false
    await loadDevices()
  } catch (e: any) {
    ElMessage.error('保存失败: ' + (e?.message || '未知错误'))
  } finally {
    saving.value = false
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

async function remove(id: number) {
  try {
    await deviceApi.delete(id)
    devices.value = devices.value.filter((d: any) => d.id !== id)
    ElMessage.success('设备已删除')
  } catch (e: any) {
    ElMessage.error('删除失败: ' + (e?.message || '未知错误'))
  }
}
</script>
