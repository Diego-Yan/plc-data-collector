# PLC 数据采集系统 — 开发说明文档

> 版本：1.1.0 | 日期：2026-05-20 | 作者：海绵宝宝 & 派大星
> TAG: single-process-merge — 2026-05-20 — 架构变更

## 一、项目概述

### 1.1 项目定位
纯 Windows 单机部署的西门子 S7 系列 PLC 数据采集系统。提供设备管理、点位配置、实时监控、历史数据查询、数据转发和 Web 管理界面。

### 1.2 核心功能
- PLC 数据采集：S7.NET 驱动，多设备并行，断线自动重连
- 实时缓存：进程内 MemoryCache（零外部依赖）
- 时序存储：PostgreSQL + TimescaleDB，按天分区
- 关系库转发：定时聚合写入 JSONB 宽表，供外部系统查询
- Web 管理：Vue3 + Element Plus 前端，10 个页面
- 实时广播：WebSocket 推送，前端即时刷新

### 1.3 运行环境
- 操作系统：Windows 10/11
- 数据库：PostgreSQL 14+ + TimescaleDB 扩展
- 浏览器：Chrome / Edge / Firefox
- 无其他依赖（无需 Redis、无需 Node.js 运行时）

---

## 二、系统架构

### 2.1 架构图

```
浏览器(Vue3) → REST/WebSocket → PLCDataCollector.Web.exe (单进程)
                                  ├─ Kestrel Web Server (端口 5000)
                                  ├─ CollectorService (PLC采集, 1s轮询)
                                  ├─ ForwardService (关系库转发)
                                  ├─ WebSocketHandler (实时推送)
                                  ├─ MemoryCacheService (实时缓存)
                                  └─ DeviceManager (设备/点位管理, JSON持久化)
                                        ↓
                                  PostgreSQL + TimescaleDB
                                        ↓
                                  关系库 (转发目标)
```

### 2.2 架构变更 (v1.1.0)

**单进程合并：** 原 v1.0.0 为双进程架构（Windows Service + ASP.NET Web），两者内存隔离导致 Web API 修改对采集进程不可见。v1.1.0 合并为单进程：PLCDataCollector.Web.exe 同时承载 Kestrel + Windows Service 宿主 + 所有 BackgroundService。

**移除 Redis：** 原蓝图设计 Redis 缓存，实际实现使用 Microsoft.Extensions.Caching.Memory（进程内），已清理所有 Redis 残留代码（AppConfig.RedisConfig、ConfigController /api/config/redis 端点、前端 Redis 配置页签）。

**持久化：** DeviceManager 增加 JSON 文件持久化（`config/devices.json` + `config/points.json`），重启后配置不丢失。

### 2.3 数据流程

PLC设备 → CollectorService(1秒轮询) → MemoryCache(5min过期) → TimescaleDB(日分区) → 关系库JSONB宽表 → WebSocket推送

### 2.4 项目结构

```
PLCDataCollector/
├── PLCDataCollector.sln
├── build.bat / install.bat / uninstall.bat
├── README.md / DEVELOPMENT.md
├── src/
│   ├── PLCDataCollector.Core/              # 核心库
│   │   ├── Configuration/AppConfig.cs
│   │   ├── Models/Device.cs
│   │   ├── Plc/PlcConnection.cs, CollectTask.cs
│   │   ├── Cache/MemoryCacheService.cs
│   │   └── Storage/TimeSeriesService.cs
│   └── PLCDataCollector.Web/               # ★ 单进程入口
│       ├── Program.cs
│       ├── appsettings.json
│       ├── Controllers/Devices, Points, Data, Config
│       ├── WebSocket/WebSocketHandler.cs
│       └── Services/CollectorService, ForwardService, DeviceManager
└── frontend/                               # Vue 3 前端
    └── src/ (10 views + router + api + stores)
```

---

## 三、开发环境搭建

### 3.1 必备工具

| 工具 | 版本 | 安装 |
|------|------|------|
| .NET SDK | 8.0+ | https://dotnet.microsoft.com |
| Node.js | 20+ | https://nodejs.org |
| PostgreSQL + TimescaleDB | 14+ | Docker 或本地安装 |

### 3.2 本地调试

```bash
# 还原依赖
dotnet restore
cd frontend && npm install && cd ..

# 启动前端 (http://localhost:5173)
cd frontend && npm run dev

# 启动后端 (http://localhost:5000)
dotnet run --project src/PLCDataCollector.Web
```

### 3.3 数据库

```bash
docker run -d --name pg-ts \
  -e POSTGRES_PASSWORD=postgres \
  -p 5432:5432 \
  timescale/timescaledb:latest-pg16
```

---

## 四、构建与打包

### 4.1 一键构建

```bash
build.bat
```

执行流程：检查 SDK → NuGet 还原 → 后端 self-contained 编译（单项目 PLCDataCollector.Web）→ npm build → 复制 dist 到 wwwroot → 输出到 publish/

### 4.2 打包为 Zip

```bash
powershell Compress-Archive -Path publish\* -DestinationPath PLCDataCollector.zip
```

---

## 五、部署指南

### 5.1 目标机零依赖

- 不需要 .NET 运行时（self-contained 内嵌）
- 不需要 Node.js（前端已编译为 wwwroot/ 静态文件）
- 不需要 Redis（进程内 MemoryCache）
- **不需要单独安装 PostgreSQL**（部署包自带 PostgreSQL 16 + TimescaleDB 便携版）

### 5.2 安装步骤（一键）

1. 复制 `publish/` 到目标 Windows 机器
2. 右键 `install.bat` → 管理员运行
3. 等待 2-3 分钟（首次需 initdb）
4. 浏览器自动打开 `http://localhost:5000`

`install.bat` 自动完成：
1. 管理员权限检测
2. 初始化 PostgreSQL 数据目录 (`initdb`)
3. 配置 postgresql.conf（端口/地址/TimescaleDB preload）
4. 注册并启动 PostgreSQL Windows Service (`PLCDataCollectorDB`)
5. 创建数据库 `plc_data` + TimescaleDB 扩展 + 配置表
6. 写入 appsettings.json 连接串
7. 注册并启动 PLCDataCollector Windows Service
8. 配置 Windows 防火墙 (端口 5000)
9. 打开浏览器

### 5.3 构建时的数据库打包

首次构建前，运行一次 `scripts\setup-pgsql.bat` 下载 PostgreSQL 16 + TimescaleDB 便携版（~100MB），缓存到 `pgsql-portable\`（已 gitignore）。后续 `build.bat` 自动将此目录复制进 `publish\pgsql\`。

跳过此步骤时，build.bat 会提示选择是否继续。部署包将不含数据库——目标机器需自行安装 PostgreSQL 14+ + TimescaleDB。

### 5.4 服务管理

```bash
net start PLCDataCollector    # 启动
net stop PLCDataCollector     # 停止
uninstall.bat                  # 卸载
```

---

## 六、API 接口

### 设备管理
GET/POST /api/devices, PUT/DELETE /api/devices/{id}, POST reconnect, PUT status

### 点位管理
GET/POST /api/devices/{id}/points, PUT/DELETE /api/points/{id}, POST /api/devices/{id}/points/batch, POST /api/points/batch-delete

### 数据接口
GET /api/devices/{id}/realtime, GET /api/points/{id}/realtime, GET /api/devices/{id}/history

### 配置接口
GET/PUT /api/config/{timescaledb|relational|system}

### WebSocket
ws://host:5000/ws
→ subscribe: {"type":"subscribe","deviceId":"1","pointIds":["1"]}
← data: {"type":"data","deviceId":"1","pointId":"1","value":123.45,"ts":"...","quality":0}

---

## 七、数据库设计

### 时序表 (TimescaleDB, 按设备分表, 日分区)
```sql
t_data_{deviceId}(time TIMESTAMPTZ, point_id VARCHAR(64), value DOUBLE, quality SMALLINT, PK(time,point_id))
```

### 宽表 (关系库, JSONB 列)
```sql
r_data_{deviceId}(time TIMESTAMPTZ PRIMARY KEY, data JSONB)
```
data 列存储 `{"point_code": value, ...}` 的 JSON 快照。

### 配置表 (计划中)
devices, points, forward_cfg, sys_config, logs

---

## 八、版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| 1.3.1 | 2026-05-20 | **审计修复**：CollectorService StopAsync 清理连接泄漏 + 设备变更检测重连 + 删设备自动清理。Device.Copy() 浅拷贝消除跨线程竞争。前端 TS 错误修复 (form 加 id)。SystemConfig save 数字→字符串转换。PlcConnection 暴露 Ip/Port。 |
| 1.3.0 | 2026-05-20 | **真正一键部署**：部署包自带 PostgreSQL 16 + TimescaleDB 便携版。install.bat 自动 initdb→建库→建表→注册服务→启动→开浏览器。目标机零依赖。新增 scripts/setup-pgsql.bat、sql/init.sql、.gitignore。 |
| 1.2.0 | 2026-05-20 | **Code Review 修复**：PlcConnection 改用异步 API (OpenAsync/ReadAsync)。DeviceManager 加 ReaderWriterLockSlim 线程安全。WebSocketHandler HashSet→ConcurrentDictionary 线程安全。ConfigController 加文件锁+系统配置 GET 返回 int。CollectorService 从配置读取采集间隔。Core 移除未用 NLog。Web 移除 Newtonsoft.Json→System.Text.Json。前端 vue-tsc 1.8→2.0，移除未用 echarts/vue-echarts，删除死 logApi。 |
| 1.1.0 | 2026-05-20 | **单进程合并**：Web+Service 合并为单进程，消除内存隔离。移除 Redis 残留。DeviceManager JSON 持久化。修复 API 路由不匹配、ElMessage 导入、ForwardService 骨架实现。 |
| 1.0.0 | 2026-05-20 | 初始版本：架构/设备CRUD/点位管理/实时监控/历史查询/配置管理/WebSocket/一键部署 |

---

## 九、待办事项

- [x] ~~Web+Service 合并为单进程~~
- [x] ~~移除 Redis 残留代码~~
- [x] ~~DeviceManager 持久化~~
- [x] ~~ForwardService 基础实现~~
- [x] ~~API 路由修复~~
- [x] ~~ElMessage 导入修复~~
- [x] ~~PlcConnection sync-over-async → OpenAsync/ReadAsync~~
- [x] ~~DeviceManager 线程安全 (ReaderWriterLockSlim)~~
- [x] ~~WebSocketHandler 线程安全 (ConcurrentDictionary)~~
- [x] ~~ConfigController 文件锁 + 系统配置 GET 返回 int~~
- [x] ~~CollectorService 从配置读取采集间隔~~
- [x] ~~Core 移除未用 NLog 依赖~~
- [x] ~~Web 移除 Newtonsoft.Json → System.Text.Json~~
- [x] ~~前端 vue-tsc 1.8→2.0, 移除未用 echarts, 删除死 logApi~~
- [x] ~~一键部署：PostgreSQL 便携版打包 + install.bat 全自动~~
- [x] ~~CollectorService PlcConnection 泄漏修复 (StopAsync + PruneConnections)~~
- [x] ~~Device 跨线程竞争修复 (Copy+ 深拷贝 GetActiveDevices)~~
- [x] ~~前端 TS 错误修复 (Devices.vue/Points.vue form.id)~~
- [x] ~~SystemConfig save 数字→字符串转换修复~~
- [ ] DeviceManager 改为 PostgreSQL 持久化（替代 JSON 文件）
- [ ] ForwardService 完善动态列宽表
- [ ] 日志 API 实现
- [ ] 前端 ECharts 趋势图
- [ ] 批量导入导出 Excel
- [ ] 单元测试
- [ ] PLC 模拟器集成测试
