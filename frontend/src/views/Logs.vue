<template>
  <div>
    <el-card>
      <template #header>
        <span>日志中心</span>
        <span style="float:right;color:#909399;font-size:13px">
          日志文件位置: logs\ 目录
        </span>
      </template>
      <p style="color:#909399;text-align:center;padding:40px 0">
        日志功能已启用，所有采集、转发和错误日志将写入 logs\ 目录下的日志文件。
        <br />
        请通过文件管理器或文本编辑器在服务器上查看日志文件。
      </p>
    </el-card>
    <el-card style="margin-top:15px">
      <template #header>故障排查提示</template>
      <ul style="line-height:2">
        <li><strong>服务无法启动：</strong>检查 Windows 事件查看器 → 应用程序</li>
        <li><strong>设备离线：</strong>检查 PLC 网络连通性、端口 102 是否开放</li>
        <li><strong>采集无数据：</strong>确认设备点位地址正确、数据类型匹配</li>
        <li><strong>数据库错误：</strong>确认 PostgreSQL 服务正在运行、连接串密码正确</li>
      </ul>
    </el-card>
  </div>
</template>
