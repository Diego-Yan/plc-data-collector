# PLC 数据采集系统 — 开发说明文档

> 版本：1.4.0 | 日期：2026-06-22

## 一、项目概述

### 1.1 项目定位
纯 Windows 单机部署的西门子 S7 系列 PLC 数据采集系统。提供设备管理、点位配置、实时监控、历史数据查询、数据转发和 Web 管理界面。

### 1.2 核心功能
- PLC 数据采集：S7.NET 驱动，多设备并发采集，1 秒轮询，超时控制 + 断线自动重连
- 实时缓存：进程内 MemoryCache（零外部依赖，5 分钟过期，50000 条容量限制）
- 时序存储：PostgreSQL + TimescaleDB，按设备分表、按天分区（hypertable）
- 关系库转发：定时聚合写入 JSONB 宽表，供外部系统查询
- 实时广播：WebSocket 推送，前端即时刷新
- Web 管理：Vue3 + Element Plus 前端，10 个页面
- 一键部署：部署包自带 PostgreSQL 16 + TimescaleDB 便携版，install.bat 全自动

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
                                  ├─ CollectorService (PLC采集, 1s轮询, 并发设备)
                                  ├─ ForwardService (关系库转发, 定时聚合 JSONB)
                                  ├─ WebSocketHandler (实时推送)
                                  ├─ MemoryCacheService (实时缓存)
                                  └─ DeviceManager (设备/点位管理 + JSON持久化)
                                        ↓
                                  PostgreSQL + TimescaleDB (时序库 plc_data)
                                        ↓
                                  PostgreSQL 关系库 (plc_data_forward)
```

### 2.2 关键架构决策

**单进程合并 (v1.1.0)：** 原 v1.0.0 为双进程架构（Windows Service + ASP.NET Web），两者内存隔离导致 Web API 修改对采集进程不可见。v1.1.0 合并为单进程，所有服务注册为 Singleton 共享同一实例。

**移除 Redis：** 使用 `Microsoft.Extensions.Caching.Memory`（进程内），已清理所有 Redis 残留代码。

**Self-Contained 单文件发布：** 后端编译为单 EXE，内嵌 .NET 运行时。

**SQL 注入防护 (v1.4.0)：** 所有动态表名使用 `NpgsqlCommandBuilder.QuoteIdentifier()` 保护，deviceId 用正则 `^\d+$` 校验。

**类型统一 (v1.4.0)：** DeviceId / PointId 统一为 `int`，消除代码库中所有 `.ToString()` / `int.TryParse` 转换。

**API 契约映射 (v1.4.0)：** ConfigController 前端 camelCase ↔ 后端文件 snake_case 通过 `SystemKeyMap` 字典映射。

### 2.3 数据流程

```
PLC设备 → CollectorService(1秒轮询,并发设备) → MemoryCache(5min过期) → TimescaleDB(日分区)
       → WebSocket实时推送
  ForwardService(10s定时) → 关系库 JSONB 宽表
```

### 2.4 项目结构

```
PLCDataCollector/
├── PLCDataCollector.sln
├── build.bat / install.bat / uninstall.bat
├── README.md / DEVELOPMENT.md / CODE_WIKI.md
├── sql/init.sql
├── scripts/setup-pgsql.bat
├── src/
│   ├── PLCDataCollector.Core/              # 核心库
│   │   ├── Configuration/AppConfig.cs
│   │   ├── Models/Device.cs
│   │   ├── Plc/PlcConnection.cs
│   │   ├── Cache/MemoryCacheService.cs
│   │   └── Storage/TimeSeriesService.cs
│   └── PLCDataCollector.Web/               # 单进程入口
│       ├── Program.cs
│       ├── appsettings.json
│       ├── Controllers/
│       │   ├── DevicesController.cs
│       │   ├── PointsController.cs
│       │   ├── DataController.cs
│       │   └── ConfigController.cs
│       ├── WebSocket/WebSocketHandler.cs
│       └── Services/
│           ├── CollectorService.cs
│           ├── ForwardService.cs
│           └── DeviceManager.cs
└── frontend/                               # Vue 3 前端
    ├── vite.config.ts / package.json
    └── src/
        ├── main.ts / App.vue
        ├── router/index.ts
        ├── api/index.ts
        ├── stores/websocket.ts
        └── views/ (Login, Home, Devices, Points, Monitor,
             History, Logs, config/Storage, config/Forward, config/System)
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

Vite 配置编辑 `frontend/vite.config.ts` 代理 `/api` → `http://localhost:5000` 和 `/ws` → `ws://localhost:5000`。

### 3.3 数据库

```bash
docker run -d --name pg-ts \
  -e POSTGRES_PASSWORD=postgres \
  -p 5432:5432 \
  timescale/timescaledb:latest-pg16

psql -h 127.0.0.1 -U postgres -d postgres -c "CREATE DATABASE plc_data;"
psql -h 127.0.0.1 -U postgres -d postgres -c "CREATE DATABASE plc_data_forward;"
psql -h 127.0.0.1 -U postgres -d plc_data -f sql/init.sql
```

---

## 四、构建与打包

### 4.1 一键构建

```bash
build.bat
```

执行流程 (8 步)：检查 SDK → 清理 → 检查 PG 缓存 → NuGet 还原 → 后端 self-contained 编译 → 前端 vue-tsc + vite 构建 → 打包出产物 → 完成

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
2. 初始化 PostgreSQL 数据目录 (`initdb`) + 配置 postgresql.conf
3. 注册并启动 PostgreSQL Windows Service (`PLCDataCollectorDB`)
4. 创建时序库 `plc_data` + 关系库 `plc_data_forward` + TimescaleDB 扩展 + 配置表
5. 写入两条独立连接串到 `appsettings.json`
6. 注册并启动 PLCDataCollector Windows Service
7. 配置 Windows 防火墙 (端口 5000)
8. 打开浏览器

### 5.3 服务管理

```bash
net start PLCDataCollector    # 启动
net stop PLCDataCollector     # 停止
net start PLCDataCollectorDB  # 启动数据库
net stop PLCDataCollectorDB   # 停止数据库
uninstall.bat                  # 卸载
```

---

## 六、API 接口

### 设备管理
| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/devices?page=1&size=20` | 分页列表 |
| GET | `/api/devices/{id}` | 详情 |
| POST | `/api/devices` | 新增 |
| PUT | `/api/devices/{id}` | 更新 |
| DELETE | `/api/devices/{id}` | 删除 |
| POST | `/api/devices/{id}/reconnect` | 触发重连 |
| PUT | `/api/devices/{id}/status` | 启停 |

### 点位管理
| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/devices/{deviceId}/points` | 列表 |
| POST | `/api/devices/{deviceId}/points` | 新增 |
| PUT | `/api/points/{id}` | 更新 |
| DELETE | `/api/points/{id}` | 删除 |
| POST | `/api/devices/{deviceId}/points/batch` | 批量导入 |
| POST | `/api/points/batch-delete` | 批量删除 |

### 数据接口
| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/devices/{deviceId}/realtime` | 设备实时数据 |
| GET | `/api/points/{id}/realtime` | 单点位实时值 |
| GET | `/api/devices/{deviceId}/history?pointIds=1,2&from=...&to=...` | 历史查询 |

### 配置接口
| 方法 | 路径 | 说明 |
|------|------|------|
| GET/PUT | `/api/config/timescaledb` | 时序库配置 |
| GET/PUT | `/api/config/relational` | 关系库配置 |
| GET/PUT | `/api/config/system` | 系统设置 (camelCase) |

### WebSocket
`ws://host:5000/ws`

订阅: `{"type":"subscribe","deviceId":"1","pointIds":["1"]}`

数据推送: `{"type":"data","deviceId":"1","pointId":"1","value":123.45,"ts":"2026-06-22T...","quality":0}`

状态推送: `{"type":"status","deviceId":"1","online":true}`

---

## 七、数据库设计

### 时序表 (TimescaleDB, 按设备分表, 日分区)
```sql
t_data_{deviceId}(
    time     TIMESTAMPTZ NOT NULL,
    point_id INTEGER NOT NULL,
    value    DOUBLE PRECISION,
    quality  SMALLINT DEFAULT 0,
    PRIMARY KEY (time, point_id)
)
```

### 宽表 (关系库, JSONB 列)
```sql
r_data_{deviceId}(
    time TIMESTAMPTZ NOT NULL PRIMARY KEY,
    data JSONB NOT NULL
)
```
`data` 列存储 `{"point_code": value, ...}` 的 JSON 快照。

---

## 八、安全审计摘要

本项目经过 4 轮深度代码审计（v1.4.0），共修复 **37 个安全问题**：

| 严重程度 | 数量 | 示例 |
|---------|------|------|
| 严重 | 7 | SQL 注入、设备在线状态丢失、API 契约不匹配、竞态条件 |
| 高危 | 12 | PlcConnection 资源泄漏、NpgsqlDataSource 未释放、前端空壳组件 |
| 中等 | 14 | ID 类型不一致、异常传播、空 catch 静默失败 |
| 低 | 4 | 假分页、文档过期 |

关键安全措施：
- SQL 注入：`QuoteIdentifier` + `^\d+$` 正则校验
- 线程安全：`ConcurrentDictionary`、`ReaderWriterLockSlim`、`TryAdd`
- 类型统一：`DeviceId`/`PointId` 全栈 `int`
- 资源管理：`IAsyncDisposable`、`NpgsqlDataSource` 连接池
- API 安全：WhiteList 键映射、输入验证、存在性检查
- 前端：导航守卫、错误处理、loading 状态

---

## 九、版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| 1.4.0 | 2026-06-22 | **第 4 轮审计**：WebSocket disconnect 清理 reconnectTimer + Router beforeEach 导航守卫 + SystemConfig save 错误处理 + StorageConfig save 错误处理 + AppConfig 移除死 DatabaseConfig + DEVELOPMENT.md 更新至 v1.4.0 |
| 1.3.2 | 2026-06-22 | **第 3 轮审计**：ConfigController SystemKeyMap 前端 camelCase↔内部 snake_case + install.bat 创建 plc_data_forward + build.bat 错误处理重写 + ForwardConfig/Logs/Points 组件实现 + WebSocket deviceId→string 协议一致性 |
| 1.3.1 | 2026-06-22 | **第 1+2 轮审计**：SQL 注入防护(QuoteIdentifier) + 设备在线状态回写 + PlcConnection IAsyncDisposable + ConcurrentDictionary + PointValue int 统一 + NpgsqlDataSource 连接池 + EnsureWideTable 缓存 + UpdateOnlineStatus 去抖 + ReadAsync WaitAsync + GetOrCreateConnection TryAdd |
| 1.3.0 | 2026-05-20 | 一键部署：PostgreSQL 便携版打包 + install.bat 全自动 |
| 1.2.0 | 2026-05-20 | Code Review 修复：PlcConnection 异步 API + DeviceManager ReaderWriterLockSlim + WebSocketHandler ConcurrentDictionary |
| 1.1.0 | 2026-05-20 | 单进程合并 + 移除 Redis + DeviceManager JSON 持久化 |
| 1.0.0 | 2026-05-20 | 初始版本 |

---

## 十、待办事项

- [x] Web+Service 合并为单进程
- [x] 移除 Redis
- [x] SQL 注入防护
- [x] 设备在线状态回写
- [x] PlcConnection IAsyncDisposable + 超时控制
- [x] 设备并发采集
- [x] BatchImport/BatchDelete 批处理
- [x] Type 统一 (int DeviceId/PointId)
- [x] NpgsqlDataSource 连接池
- [x] EnsureWideTable 缓存
- [x] UpdateOnlineStatus 去抖保存
- [x] ConfigController SystemKeyMap 映射
- [x] install.bat plc_data_forward 独立库
- [x] build.bat 错误处理
- [x] ForwardConfig/Logs 组件实现
- [x] Router 导航守卫
- [x] WebSocket disconnect 清理 reconnectTimer
- [ ] DeviceManager 迁移到 PostgreSQL 持久化
- [ ] ForwardService 完善动态列宽表
- [ ] 日志 API 实现
- [ ] 前端 ECharts 趋势图
- [ ] Excel 导入导出
- [ ] 单元测试与集成测试
