<template>
  <el-card>
    <template #header>日志中心</template>
    <el-form inline>
      <el-form-item label="等级">
        <el-select v-model="level" style="width:120px">
          <el-option label="全部" value="" />
          <el-option label="Info" value="Info" />
          <el-option label="Warn" value="Warn" />
          <el-option label="Error" value="Error" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button type="primary" @click="search">查询</el-button>
      </el-form-item>
    </el-form>
    <el-table :data="logs" stripe>
      <el-table-column prop="time" label="时间" width="180" />
      <el-table-column prop="level" label="等级" width="80">
        <template #default="{ row }">
          <el-tag :type="row.level === 'Error' ? 'danger' : row.level === 'Warn' ? 'warning' : 'info'" size="small">
            {{ row.level }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="message" label="消息" />
    </el-table>
  </el-card>
</template>

<script setup lang="ts">
// TAG: fixed — added ElMessage import
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
const level = ref('')
const logs = ref<any[]>([
  { time: new Date().toISOString(), level: 'Info', message: '系统已启动' },
  { time: new Date().toISOString(), level: 'Info', message: 'PLC采集服务已启动' },
])
function search() { ElMessage.info('日志查询功能需要后端实现') }
</script>
