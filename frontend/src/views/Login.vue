<template>
  <div style="display:flex;justify-content:center;align-items:center;min-height:100vh;background:#f0f2f5">
    <el-card style="width:400px">
      <template #header><h2 style="text-align:center">PLC 数据采集系统</h2></template>
      <el-form :model="form" label-width="80px">
        <el-form-item label="用户名">
          <el-input v-model="form.username" />
        </el-form-item>
        <el-form-item label="密码">
          <el-input v-model="form.password" type="password" show-password />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" style="width:100%" @click="login">登 录</el-button>
        </el-form-item>
      </el-form>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { reactive } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const form = reactive({ username: 'admin', password: 'admin' })

function login() {
  localStorage.setItem('plc_user', form.username)
  router.push('/dashboard')
}
</script>
