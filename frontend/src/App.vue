<template>
  <div v-if="isLogin">
    <router-view />
  </div>
  <el-container v-else style="min-height:100vh">
    <el-aside width="220px" style="background:#304156">
      <el-menu :default-active="route.path" router background-color="#304156" text-color="#fff" active-text-color="#409EFF">
        <el-menu-item index="/dashboard">🏠 首页看板</el-menu-item>
        <el-menu-item index="/devices">📟 设备管理</el-menu-item>
        <el-menu-item index="/monitor">📊 实时监控</el-menu-item>
        <el-menu-item index="/history">📈 历史查询</el-menu-item>
        <el-sub-menu index="config">
          <template #title>⚙️ 系统配置</template>
          <el-menu-item index="/config/storage">存储配置</el-menu-item>
          <el-menu-item index="/config/forward">转发配置</el-menu-item>
          <el-menu-item index="/config/system">系统设置</el-menu-item>
        </el-sub-menu>
        <el-menu-item index="/logs">📋 日志中心</el-menu-item>
      </el-menu>
    </el-aside>
    <el-container>
      <el-header style="background:#fff;border-bottom:1px solid #e6e6e6;display:flex;align-items:center;justify-content:space-between;padding:0 20px">
        <h3>PLC 数据采集系统</h3>
        <el-button type="danger" size="small" @click="logout">退出</el-button>
      </el-header>
      <el-main style="background:#f0f2f5">
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
const route = useRoute()
const router = useRouter()
const isLogin = computed(() => route.path === '/')
function logout() { localStorage.removeItem('plc_user'); router.push('/') }
</script>
