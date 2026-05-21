# PLC 数据采集系统

> TAG: one-click-deploy — 2026-05-20 — v1.3.1

西门子 S7 系列 PLC 数据采集系统，Windows 单机一键部署。

## 快速开始

### 环境要求（构建用）
- Windows 10/11
- .NET 8 SDK
- Node.js 20+

### 构建

```bash
# 首次：下载 PostgreSQL 便携版（只需一次，~100MB，缓存到 pgsql-portable\）
scripts\setup-pgsql.bat

# 构建部署包
build.bat
```

自动完成：检查 SDK → 清理 → 打包 PostgreSQL → NuGet 还原 → 后端编译 → 前端编译 → 输出到 `publish\`

### 一键部署

1. 将 `publish\` 目录复制到目标 Windows 机器
2. 右键 `install.bat` → **以管理员身份运行**

`install.bat` 自动完成全部操作：
- 初始化并启动 PostgreSQL 16 + TimescaleDB
- 创建数据库 `plc_data` + 配置表
- 写入连接配置
- 注册并启动 PLCDataCollector Windows Service
- 配置防火墙
- 打开浏览器 `http://localhost:5000`

**目标机零依赖** — 无需安装 .NET、Node.js、PostgreSQL、Redis。部署包已包含一切。

### 服务管理

```bash
net start PLCDataCollector      # 启动应用
net stop PLCDataCollector       # 停止应用
net start PLCDataCollectorDB    # 启动数据库
net stop PLCDataCollectorDB     # 停止数据库
uninstall.bat                   # 卸载全部
```

### 技术栈

| 层 | 技术 |
|---|------|
| 前端 | Vue 3 + Element Plus + ECharts |
| 后端 | .NET 8 + ASP.NET Core (Kestrel) |
| 采集 | S7.NET（西门子 S7 协议） |
| 缓存 | MemoryCache（进程内，零外部依赖） |
| 时序存储 | PostgreSQL + TimescaleDB |

### 架构

```
PLCDataCollector.Web.exe (单进程)
├─ Kestrel Web Server (端口 5000)
├─ CollectorService (PLC采集, 1s轮询)
├─ ForwardService (关系库转发)
├─ WebSocketHandler (实时推送)
└─ DeviceManager (JSON持久化)
```

### 数据流程

```
PLC → S7.NET 采集 → MemoryCache (进程内缓存)
                         ↓
                TimescaleDB 时序存储
                         ↓
                关系库 JSONB 宽表 → 外部系统
                         ↓
                WebSocket 广播 → Vue 前端
```

### 默认端口
- Web 管理页面: `http://localhost:5000`
- PostgreSQL: `127.0.0.1:5432`

### 版本

**v1.1.0** (2026-05-20) — 单进程合并，Redis 残留清理，持久化，多项 Bug 修复。详见 [DEVELOPMENT.md](DEVELOPMENT.md)。
