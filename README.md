# PLC 数据采集系统

> v1.4.0 | 2026-06-22

西门子 S7 系列 PLC 数据采集系统，Windows 单机一键部署，经过 4 轮深度代码审计。

## 快速开始

### 环境要求（构建用）

- Windows 10/11
- .NET 8 SDK
- Node.js 20+

### 构建部署包

```bash
# 首次：下载 PostgreSQL 16 便携版（~100MB，缓存到 pgsql-portable\）
scripts\setup-pgsql.bat

# 构建
build.bat
```

`build.bat` 自动完成：检查 SDK → 清理 → 检查 PostgreSQL 缓存 → NuGet 还原 → 后端 self-contained 编译 → 前端 vue-tsc + vite 构建 → 输出到 `publish\`

### 一键部署

1. 将 `publish\` 目录复制到目标 Windows 机器
2. 右键 `install.bat` → **以管理员身份运行**

`install.bat` 自动完成全部操作：
- 初始化并启动 PostgreSQL 16 + TimescaleDB (自带的便携版或系统已有)
- 创建时序库 `plc_data` + 关系库 `plc_data_forward` + 配置表
- 写入两条独立连接串到 `appsettings.json`
- 注册并启动 PLCDataCollector Windows Service
- 配置防火墙 (端口 5000)
- 打开浏览器 `http://localhost:5000`

**目标机零依赖** — 无需安装 .NET / Node.js / PostgreSQL / Redis。部署包已包含一切。

### 服务管理

```bash
net start PLCDataCollector      # 启动应用
net stop PLCDataCollector       # 停止应用
net start PLCDataCollectorDB    # 启动数据库
net stop PLCDataCollectorDB     # 停止数据库
uninstall.bat                   # 卸载全部（保留数据目录）
```

### 默认账号

`admin / admin`（前端 localStorage 简单校验）

### 技术栈

| 层 | 技术 |
|---|------|
| 前端 | Vue 3 + Element Plus |
| 后端 | .NET 8 + ASP.NET Core (Kestrel) |
| 采集 | S7.NET（西门子 S7 协议） |
| 缓存 | MemoryCache（进程内，零外部依赖，50000 条容量上限） |
| 时序存储 | PostgreSQL + TimescaleDB |
| 关系转发 | PostgreSQL JSONB 宽表 |
| WebSocket | 实时数据推送 |

### 架构

```
PLCDataCollector.Web.exe (单进程)
├─ Kestrel Web Server (端口 5000)
├─ CollectorService (PLC采集, 1s轮询, 并发设备)
├─ ForwardService  (关系库转发, 10s定时聚合JSONB)
├─ WebSocketHandler (实时推送)
├─ MemoryCacheService (实时缓存, 5min过期)
└─ DeviceManager (设备/点位管理, JSON持久化 + ReaderWriterLockSlim)
```

### 数据流程

```
PLC → S7.NET 采集 (并发设备, WaitAsync超时) → MemoryCache (进程内缓存)
       ↓
       TimescaleDB 时序存储 (hyperTable, 日分区)
       ↓
       关系库 JSONB 宽表 → 外部系统查询
       ↓
       WebSocket 广播 → Vue 前端即时刷新
```

### 数据库

| 库 | 用途 | 表结构 |
|----|------|--------|
| `plc_data` | 时序库 | `t_data_{id}(time, point_id INTEGER, value, quality)` per device |
| `plc_data_forward` | 关系库 | `r_data_{id}(time, data JSONB)` per device |

### 默认端口

- Web 管理页面: `http://localhost:5000`
- PostgreSQL: `127.0.0.1:5432`
- 开发前端: `http://localhost:5173`

### 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| 1.4.0 | 2026-06-22 | 第 3+4+5 轮审计：API 契约映射 + install.bat 创建 plc_data_forward + build.bat 重写 + 前端空壳组件实现 + Router 导航守卫 + WebSocket 重连清理 + Type 统一 + NpgsqlDataSource 连接池 + Config 白名单 + 共计 45 项修复 |
| 1.3.1 | 2026-06-22 | 第 1+2 轮审计：SQL 注入防护 + 设备在线状态回写 + PlcConnection IAsyncDisposable + 线程安全 + 批处理 |
| 1.3.0 | 2026-05-20 | 一键部署：PostgreSQL 便携版打包 |
| 1.2.0 | 2026-05-20 | Code Review 修复：异步、锁、ConcurrentDictionary |
| 1.1.0 | 2026-05-20 | 单进程合并 + 移除 Redis + DeviceManager JSON 持久化 |

### 文档

- [CODE_WIKI.md](CODE_WIKI.md) — 代码级架构文档（类/API/DB/数据流程）
- [DEVELOPMENT.md](DEVELOPMENT.md) — 开发部署指南（环境/构建/审计摘要）
