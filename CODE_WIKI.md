# PLC 数据采集系统 — Code Wiki

> 版本：v1.4.0 | 日期：2026-06-22
> 本文档为代码级技术参考，覆盖项目整体架构、模块职责、关键类与函数、依赖关系及运行方式。

---

## 目录

1. [项目概述](#1-项目概述)
2. [项目整体架构](#2-项目整体架构)
3. [项目目录结构](#3-项目目录结构)
4. [技术栈与依赖关系](#4-技术栈与依赖关系)
5. [核心模块职责](#5-核心模块职责)
6. [关键类与函数说明](#6-关键类与函数说明)
   - 6.1 [Core 层（PLCDataCollector.Core）](#61-core-层plcdatacollectorcore)
   - 6.2 [Web 层（PLCDataCollector.Web）](#62-web-层plcdatacollectorweb)
   - 6.3 [前端（frontend）](#63-前端frontend)
7. [数据流程](#7-数据流程)
8. [数据库设计](#8-数据库设计)
9. [API 接口](#9-api-接口)
10. [WebSocket 协议](#10-websocket-协议)
11. [项目运行方式](#11-项目运行方式)
12. [配置说明](#12-配置说明)
13. [架构演进与版本历史](#13-架构演进与版本历史)

---

## 1. 项目概述

### 1.1 项目定位

纯 Windows 单机部署的**西门子 S7 系列 PLC 数据采集系统**。面向工业现场，提供设备管理、点位配置、实时监控、历史查询、数据转发与 Web 管理界面。

### 1.2 核心功能

| 功能 | 说明 |
|------|------|
| PLC 数据采集 | 基于 S7.Net 驱动，多设备并行，1 秒轮询，断线自动重连 |
| 实时缓存 | 进程内 `MemoryCache`（零外部依赖，5 分钟过期） |
| 时序存储 | PostgreSQL + TimescaleDB，按设备分表、按天分区（hypertable） |
| 关系库转发 | 定时聚合缓存快照，写入 JSONB 宽表，供外部系统查询 |
| Web 管理 | Vue3 + Element Plus，10 个页面 |
| 实时广播 | WebSocket 推送，前端即时刷新 |

### 1.3 运行环境

- 操作系统：Windows 10/11
- 数据库：PostgreSQL 14+ + TimescaleDB 扩展
- 浏览器：Chrome / Edge / Firefox
- **目标机零依赖**：部署包自带 .NET 运行时（self-contained）、PostgreSQL 16 便携版、前端静态文件，无需单独安装 .NET / Node.js / Redis / PostgreSQL

---

## 2. 项目整体架构

### 2.1 架构图

```
浏览器(Vue3) ──REST/WebSocket──> PLCDataCollector.Web.exe (单进程)
                                   ├─ Kestrel Web Server (端口 5000)
                                   │   ├─ Controllers (Devices/Points/Data/Config)
                                   │   └─ WebSocket 端点 (/ws)
                                   ├─ CollectorService (PLC 采集, BackgroundService)
                                   ├─ ForwardService  (关系库转发, BackgroundService)
                                   ├─ WebSocketHandler (实时推送)
                                   ├─ MemoryCacheService (进程内缓存)
                                   └─ DeviceManager (设备/点位管理, JSON 持久化)
                                          │
                                          ↓
                                   PostgreSQL + TimescaleDB (时序库 plc_data)
                                          │
                                          ↓
                                   关系库 (plc_data_forward, JSONB 宽表)
```

### 2.2 架构关键决策

1. **单进程合并（v1.1.0）**：原 v1.0.0 为双进程（Windows Service + ASP.NET Web），内存隔离导致 Web API 修改对采集进程不可见。v1.1.0 合并为单进程 `PLCDataCollector.Web.exe`，同时承载 Kestrel + Windows Service 宿主 + 所有 BackgroundService。
2. **移除 Redis**：使用 `Microsoft.Extensions.Caching.Memory`（进程内），已清理所有 Redis 残留代码。
3. **JSON 持久化**：`DeviceManager` 使用 `config/devices.json` + `config/points.json`，重启后配置不丢失（计划后续迁移至 PostgreSQL）。
4. **Self-Contained 单文件发布**：后端编译为单 EXE，内嵌 .NET 运行时。

### 2.3 分层职责

```
┌─────────────────────────────────────────────────┐
│  frontend (Vue3)  — 表现层：UI、路由、状态、API  │
├─────────────────────────────────────────────────┤
│  PLCDataCollector.Web — 应用层：                 │
│    Controllers (HTTP API)                        │
│    Services (BackgroundService 业务后台)         │
│    WebSocket (实时推送)                          │
├─────────────────────────────────────────────────┤
│  PLCDataCollector.Core — 领域层：                │
│    Models (实体)  Plc (协议)  Cache  Storage     │
│    Configuration (配置模型)                      │
└─────────────────────────────────────────────────┘
```

---

## 3. 项目目录结构

```
PLCDataCollector/
├── PLCDataCollector.sln                  # VS 解决方案（Web + Core 两项目）
├── README.md                             # 用户文档
├── DEVELOPMENT.md                        # 开发文档
├── CODE_WIKI.md                          # 本文档
├── build.bat                             # 一键构建脚本
├── install.bat                           # 一键安装脚本（目标机）
├── uninstall.bat                         # 卸载脚本
│
├── scripts/
│   └── setup-pgsql.bat                   # 下载 PostgreSQL 16 + TimescaleDB 便携版
│
├── sql/
│   └── init.sql                          # 数据库初始化（扩展 + 配置表）
│
├── src/
│   ├── PLCDataCollector.Core/            # 核心库（领域层）
│   │   ├── PLCDataCollector.Core.csproj
│   │   ├── Configuration/AppConfig.cs    # 配置模型
│   │   ├── Models/Device.cs              # 实体：Device/Point/PointValue
│   │   ├── Plc/
│   │   │   ├── PlcConnection.cs          # S7 连接封装
│   │   │   └── CollectTask.cs            # 采集任务模型
│   │   ├── Cache/MemoryCacheService.cs   # 进程内缓存
│   │   └── Storage/TimeSeriesService.cs  # TimescaleDB 读写
│   │
│   └── PLCDataCollector.Web/             # Web 应用（应用层 + 入口）
│       ├── PLCDataCollector.Web.csproj
│       ├── Program.cs                    # 应用入口（DI 注册 + 中间件）
│       ├── appsettings.json              # 连接串 + 采集/转发参数
│       ├── Controllers/
│       │   ├── DevicesController.cs      # 设备 CRUD
│       │   ├── PointsController.cs       # 点位 CRUD
│       │   ├── DataController.cs         # 实时/历史数据
│       │   └── ConfigController.cs       # 系统配置
│       ├── Services/
│       │   ├── CollectorService.cs       # PLC 采集后台服务
│       │   ├── ForwardService.cs         # 关系库转发后台服务
│       │   └── DeviceManager.cs          # 设备/点位管理（持久化）
│       └── WebSocket/
│           └── WebSocketHandler.cs       # WebSocket 订阅与广播
│
└── frontend/                             # Vue3 前端
    ├── package.json
    ├── vite.config.ts                    # Vite 配置（含 /api /ws 代理）
    ├── index.html
    └── src/
        ├── main.ts                       # 应用入口
        ├── App.vue                       # 根组件（侧边栏布局）
        ├── env.d.ts
        ├── router/index.ts               # 路由（10 个页面）
        ├── api/index.ts                  # Axios API 封装
        ├── stores/websocket.ts           # Pinia WebSocket Store
        └── views/
            ├── Login.vue                 # 登录
            ├── Home.vue                  # 首页看板
            ├── Devices.vue               # 设备管理
            ├── Points.vue                # 点位管理
            ├── Monitor.vue               # 实时监控
            ├── History.vue               # 历史查询
            ├── Logs.vue                  # 日志中心
            └── config/
                ├── StorageConfig.vue     # 存储配置
                ├── ForwardConfig.vue     # 转发配置
                └── SystemConfig.vue      # 系统设置
```

---

## 4. 技术栈与依赖关系

### 4.1 技术栈

| 层 | 技术 | 版本 |
|----|------|------|
| 前端框架 | Vue 3 | ^3.4.0 |
| 前端路由 | Vue Router | ^4.3.0 |
| 前端状态 | Pinia | ^2.1.0 |
| UI 组件库 | Element Plus | ^2.6.0 |
| HTTP 客户端 | Axios | ^1.6.0 |
| 构建工具 | Vite | ^5.1.0 |
| 类型检查 | vue-tsc + TypeScript | ^2.0.0 / ^5.3.0 |
| 后端框架 | .NET 8 + ASP.NET Core (Kestrel) | net8.0 |
| PLC 协议 | S7.Net | 2.0.0 |
| 缓存 | Microsoft.Extensions.Caching.Memory | 8.0.0 |
| 数据库驱动 | Npgsql | 8.0.0 |
| 日志 | NLog + NLog.Web.AspNetCore | 5.3.0 |
| Windows 服务 | Microsoft.Extensions.Hosting.WindowsServices | 8.0.0 |
| 时序存储 | PostgreSQL + TimescaleDB | 16 / 2.15.3 |

### 4.2 后端包依赖

**PLCDataCollector.Core.csproj**（核心库，无 Web 依赖）：
- `S7.Net` 2.0.0 — 西门子 S7 协议
- `Microsoft.Extensions.Caching.Memory` 8.0.0 — 进程内缓存
- `Npgsql` 8.0.0 — PostgreSQL 驱动

**PLCDataCollector.Web.csproj**（Web 应用，引用 Core）：
- `Microsoft.Extensions.Hosting.WindowsServices` 8.0.0 — Windows Service 宿主
- `S7.Net` 2.0.0
- `NLog` / `NLog.Web.AspNetCore` / `NLog.Extensions.Logging` 5.3.0 — 日志
- `Npgsql` 8.0.0
- `ProjectReference` → `PLCDataCollector.Core`

### 4.3 项目间依赖

```
frontend (Vue) ──HTTP/WS──> PLCDataCollector.Web
                                  │
                                  └──> PLCDataCollector.Core
                                          │
                                          ├──> S7.Net (PLC 通信)
                                          ├──> Npgsql (PostgreSQL)
                                          └──> MemoryCache (进程内缓存)
```

---

## 5. 核心模块职责

### 5.1 PLCDataCollector.Core（领域层）

| 模块 | 文件 | 职责 |
|------|------|------|
| Configuration | `AppConfig.cs` | 定义配置数据模型（采集器、数据库、转发） |
| Models | `Device.cs` | 实体定义：`Device`、`Point`、`PointValue`、枚举 |
| Plc | `PlcConnection.cs` | 封装 S7.Net 连接，异步连接/读取/断开 |
| Plc | `CollectTask.cs` | 采集任务数据结构（设备 + 点位列表） |
| Cache | `MemoryCacheService.cs` | 进程内缓存，存储实时点位值与设备状态 |
| Storage | `TimeSeriesService.cs` | TimescaleDB 时序表创建、写入、历史查询 |

### 5.2 PLCDataCollector.Web（应用层 + 入口）

| 模块 | 文件 | 职责 |
|------|------|------|
| 入口 | `Program.cs` | DI 容器注册、中间件配置、WebSocket 端点映射 |
| Controllers | `DevicesController.cs` | 设备 CRUD、重连、启停 |
| Controllers | `PointsController.cs` | 点位 CRUD、批量导入/删除 |
| Controllers | `DataController.cs` | 实时数据、历史数据查询 |
| Controllers | `ConfigController.cs` | 系统配置（时序库/关系库/系统设置）JSON 文件持久化 |
| Services | `CollectorService.cs` | PLC 采集后台服务（`BackgroundService`，1 秒轮询） |
| Services | `ForwardService.cs` | 关系库转发后台服务（定时聚合写入 JSONB 宽表） |
| Services | `DeviceManager.cs` | 设备/点位内存管理 + JSON 文件持久化（线程安全） |
| WebSocket | `WebSocketHandler.cs` | WebSocket 连接管理、订阅、广播 |

### 5.3 frontend（表现层）

| 模块 | 文件 | 职责 |
|------|------|------|
| 入口 | `main.ts` | 创建 Vue 应用，注册 Pinia/Router/ElementPlus |
| 根组件 | `App.vue` | 侧边栏布局，登录态判断 |
| 路由 | `router/index.ts` | 10 个页面路由（Hash 模式） |
| API | `api/index.ts` | Axios 实例 + 四组 API 封装（device/point/data/config） |
| Store | `stores/websocket.ts` | Pinia WebSocket Store（连接/订阅/重连） |
| 视图 | `views/*.vue` | 10 个业务页面 |

---

## 6. 关键类与函数说明

### 6.1 Core 层（PLCDataCollector.Core）

#### 6.1.1 `Configuration/AppConfig.cs`

配置数据模型（POCO），用于映射 `appsettings.json`。

```csharp
public class AppConfig
public class CollectorConfig   // 采集参数：间隔/超时/重连/延迟启动
public class DatabaseConfig    // 数据库连接：Host/Port/Database/Username/Password
public class ForwardConfig     // 转发参数：IntervalSec
```

> 注：实际运行时 `CollectorService` / `ForwardService` 直接通过 `IConfiguration` 读取键值，`AppConfig` 类作为强类型模型备用。

#### 6.1.2 `Models/Device.cs`

核心实体定义。

| 类型 | 说明 |
|------|------|
| `Device` | PLC 设备：Id、Name、IpAddress、Port(默认102)、Rack、Slot、Protocol(S7)、Enabled、IsOnline、LastCollectedAt、CreatedAt。提供 `Copy()` 浅拷贝用于线程安全迭代。 |
| `Point` | 采集点位：Id、DeviceId、Code、Name、Address（如 `DB1.DBD0`）、DataType、Unit、Enabled。 |
| `DataType` (enum) | `Bool, Byte, Word, DWord, Int, DInt, Real` |
| `PointValue` | 点位实时值：DeviceId、PointId、Value(double?)、Timestamp、Quality |
| `QualityStatus` (enum) | `Good=0, Timeout=1, BadValue=2, DeviceOffline=3, NotCollected=4` |

#### 6.1.3 `Plc/PlcConnection.cs`

封装 S7.Net 的 `Plc` 对象，实现 `IDisposable`。

| 成员 | 说明 |
|------|------|
| `DeviceId` / `Ip` / `Port` | 连接标识与暴露的地址信息（用于变更检测） |
| `IsConnected` | 是否已连接（委托 `_plc?.IsConnected`） |
| `ConnectionStateChanged` 事件 | 连接状态变化通知 |
| `ConnectAsync()` | 创建 `Plc(CpuType.S71200, ip, rack, slot)` 并 `OpenAsync()`，失败触发事件返回 false |
| `DisconnectAsync()` | `CloseAsync()` + 触发状态事件 |
| `ReadAsync(address)` | 读取指定地址数据，异常返回 null |
| `Dispose()` | 关闭并释放底层 `Plc` |

> 构造函数硬编码 `CpuType.S71200`，适用于 S7-1200/1500 系列。

#### 6.1.4 `Plc/CollectTask.cs`

采集任务数据结构（`CollectTask` 包含 `DeviceId` + `List<CollectPoint>`），当前实现中 `CollectorService` 直接使用 `DeviceManager` 的数据，此类为预留模型。

#### 6.1.5 `Cache/MemoryCacheService.cs`

进程内缓存服务，`IDisposable`，默认 5 分钟过期。

| 方法 | 说明 |
|------|------|
| `ConnectAsync()` | 空实现（兼容接口，原 Redis 占位） |
| `SetPointValue(PointValue)` | 缓存键 `point:{deviceId}:{pointId}` |
| `GetPointValue(deviceId, pointId)` | 读取点位实时值 |
| `SetDeviceStatus(deviceId, online)` | 缓存键 `device:{deviceId}:status`，存 `DeviceStatus` |
| `GetDeviceStatus(deviceId)` | 读取设备在线状态 |

辅助类 `DeviceStatus`：`Online` + `LastSeen`（UTC ISO 8601 字符串）。

#### 6.1.6 `Storage/TimeSeriesService.cs`

TimescaleDB 时序数据读写，每设备一张 hypertable。

| 方法 | 说明 |
|------|------|
| `EnsureTableAsync(deviceId)` | 建表 `t_data_{deviceId}`（time, point_id, value, quality），并 `create_hypertable` 按天分区 |
| `WritePoint(PointValue)` | INSERT 一条时序记录 |
| `QueryHistoryAsync(deviceId, pointIds[], from, to)` | 按点位 + 时间范围查询历史，`point_id = ANY(@pointIds)` |

> 表名直接拼接 `deviceId`，依赖 `DeviceManager` 的 int 自增 Id 保证安全。

---

### 6.2 Web 层（PLCDataCollector.Web）

#### 6.2.1 `Program.cs`（应用入口）

```csharp
builder.Host.UseWindowsService(...)   // Windows Service 宿主
builder.Services.AddControllers()
// 单例服务（Web 请求 + 后台服务共享）
builder.Services.AddSingleton<MemoryCacheService>()
builder.Services.AddSingleton<TimeSeriesService>(...)  // 从 ConnectionStrings 构造
builder.Services.AddSingleton<DeviceManager>()
builder.Services.AddSingleton<WebSocketHandler>()
// 后台服务
builder.Services.AddHostedService<CollectorService>()
builder.Services.AddHostedService<ForwardService>()

app.UseWebSockets()
app.UseStaticFiles()                  // 服务前端 wwwroot
app.MapControllers()
app.Map("/ws", ...)                   // WebSocket 端点
app.MapFallbackToFile("index.html")   // SPA 回退
```

**DI 生命周期要点**：所有核心服务注册为**单例**，确保 HTTP 请求与 BackgroundService 共享同一实例（单进程合并的核心保证）。

#### 6.2.2 `Services/DeviceManager.cs`

设备/点位内存管理 + JSON 持久化，`IDisposable`，**线程安全**（`ReaderWriterLockSlim`）。

| 方法 | 说明 |
|------|------|
| `GetAllAsync()` / `GetByIdAsync(id)` | 设备查询（读锁） |
| `CreateAsync(Device)` / `UpdateAsync(Device)` / `DeleteAsync(id)` | 设备 CRUD（写锁 + Save） |
| `ReconnectAsync(id)` | 标记 `IsOnline=false`（实际重连由 CollectorService 处理） |
| `SetStatusAsync(id, enabled)` | 启停设备 |
| `GetActiveDevices()` | 返回 `Enabled=true` 设备的**浅拷贝**列表（供 CollectorService 迭代） |
| `GetDevicePoints(deviceIdStr)` / `GetAllPoints()` | 点位查询 |
| `AddPoint` / `UpdatePoint` / `DeletePoint` | 点位 CRUD |
| `Load()` / `Save()` | JSON 文件持久化（`config/devices.json` + `config/points.json`） |

**持久化机制**：构造时 `Load()`，每次写操作后 `Save()`。自增 Id 通过 `_nextDeviceId` / `_nextPointId` 维护，`Load()` 时根据现有最大 Id 重算。

#### 6.2.3 `Services/CollectorService.cs`

PLC 采集后台服务，继承 `BackgroundService`。

| 成员 | 说明 |
|------|------|
| `_collectIntervalMs` | 从 `IConfiguration` 读取 `Collector:CollectIntervalMs`（默认 1000） |
| `_connections` | `Dictionary<string, PlcConnection>` 设备 Id → 连接 |
| `ExecuteAsync(ct)` | 启动延迟 5 秒 → 循环 `CollectCycleAsync` + `Task.Delay(interval)` |
| `CollectCycleAsync(ct)` | 遍历活跃设备：获取/创建连接 → 连接 → 读取所有点位 → 写缓存 + 写时序库 + WebSocket 广播 |
| `GetOrCreateConnection(device)` | 复用连接；若 IP/Port 变更则销毁旧连接重建 |
| `PruneConnections(activeDevices)` | 清理已删除设备的连接（防止泄漏） |
| `StopAsync(ct)` | 停止时 Dispose 所有连接 |

**采集周期数据流**：
```
DeviceManager.GetActiveDevices() → PlcConnection.ReadAsync(address)
  → Convert.ToDouble → PointValue
  → MemoryCacheService.SetPointValue
  → TimeSeriesService.WritePoint
  → WebSocketHandler.BroadcastPoint
```

#### 6.2.4 `Services/ForwardService.cs`

关系库转发后台服务，继承 `BackgroundService`。

| 成员 | 说明 |
|------|------|
| `_connString` | 从 `ConnectionStrings:RelationalDb` 读取 |
| `_intervalSec` | 从 `Forward:IntervalSec` 读取（默认 10） |
| `ExecuteAsync(ct)` | 延迟 10 秒 → 循环 `ForwardCycleAsync` + `Delay(interval)` |
| `ForwardCycleAsync(ct)` | 遍历活跃设备：读取所有点位缓存值 → 组装 `Dictionary<pointCode, value>` → JSON 序列化 → INSERT 到 `r_data_{deviceId}(time, data JSONB)` |
| `EnsureWideTable(conn, tableName)` | 建表 `r_data_{deviceId}(time TIMESTAMPTZ PK, data JSONB)` |

**转发模型**：JSONB 宽表，`data` 列存 `{"point_code": value, ...}` 快照，适配动态点位 schema。

#### 6.2.5 `WebSocket/WebSocketHandler.cs`

WebSocket 连接管理、订阅与广播，**线程安全**（`ConcurrentDictionary`）。

| 成员 | 说明 |
|------|------|
| `_subscriptions` | `ConcurrentDictionary<deviceId, ConcurrentDictionary<WebSocket, byte>>` |
| `HandleAsync(ws, ct)` | 循环接收消息，解析 `SubscribeMessage`，维护订阅关系；连接关闭时清理 |
| `BroadcastPoint(PointValue)` | 向订阅该设备的所有 socket 推送 `{type:"data", deviceId, pointId, value, ts, quality}` |
| `BroadcastStatus(deviceId, online)` | 推送 `{type:"status", deviceId, online}` |
| `BroadcastToDevice(deviceId, bytes)` | 实际广播逻辑，发送失败则移除 socket |

`SubscribeMessage`：`{ Type: "subscribe"|"unsubscribe", DeviceId, PointIds? }`。

#### 6.2.6 `Controllers/DevicesController.cs`

路由 `api/[controller]`（即 `/api/devices`）。

| 方法 | 路由 | 说明 |
|------|------|------|
| GET | `/api/devices?page&size` | 分页查询，返回 `{total, items}` |
| GET | `/api/devices/{id}` | 单设备 |
| POST | `/api/devices` | 创建 |
| PUT | `/api/devices/{id}` | 更新 |
| DELETE | `/api/devices/{id}` | 删除 |
| POST | `/api/devices/{id}/reconnect` | 触发重连 |
| PUT | `/api/devices/{id}/status` | 启停，body `{enabled: bool}` |

#### 6.2.7 `Controllers/PointsController.cs`

路由 `api`。

| 方法 | 路由 | 说明 |
|------|------|------|
| GET | `/api/devices/{deviceId}/points` | 设备下点位列表 |
| POST | `/api/devices/{deviceId}/points` | 新增点位 |
| PUT | `/api/points/{id}` | 更新点位 |
| DELETE | `/api/points/{id}` | 删除点位 |
| POST | `/api/devices/{deviceId}/points/batch` | 批量导入 |
| POST | `/api/points/batch-delete` | 批量删除（body: `[id...]`） |

#### 6.2.8 `Controllers/DataController.cs`

路由 `api`。

| 方法 | 路由 | 说明 |
|------|------|------|
| GET | `/api/devices/{deviceId}/realtime` | 设备所有点位实时值（从缓存） |
| GET | `/api/points/{id}/realtime` | 单点位实时值 |
| GET | `/api/devices/{deviceId}/history?pointIds&from&to` | 历史查询（TimescaleDB），`pointIds` 逗号分隔 |

#### 6.2.9 `Controllers/ConfigController.cs`

路由 `api/config`，JSON 文件持久化（`config/system_config.json`），`static` 锁保护文件 I/O。

| 方法 | 路由 | 说明 |
|------|------|------|
| GET/PUT | `/api/config/timescaledb` | 时序库配置 |
| GET/PUT | `/api/config/relational` | 关系库配置 |
| GET/PUT | `/api/config/system` | 系统设置（采集间隔/超时/重连/日志保留），GET 返回 int |

默认配置项：`timescaledb_*`、`relational_*`、`collect_interval`、`timeout`、`reconnect_interval`、`log_retention_days`。

---

### 6.3 前端（frontend）

#### 6.3.1 `main.ts`

创建 Vue 应用，注册 Pinia、Router、ElementPlus，挂载到 `#app`。

#### 6.3.2 `App.vue`

根组件，根据 `route.path === '/'` 判断登录态：
- 登录页：直接渲染 `<router-view />`
- 其他页：左侧菜单（220px）+ 顶栏 + 主内容区，菜单项对应 10 个路由

#### 6.3.3 `router/index.ts`

Hash 模式路由，10 个页面：

| 路径 | 组件 | 说明 |
|------|------|------|
| `/` | Login.vue | 登录 |
| `/dashboard` | Home.vue | 首页看板 |
| `/devices` | Devices.vue | 设备管理 |
| `/devices/:id/points` | Points.vue | 点位管理 |
| `/monitor` | Monitor.vue | 实时监控 |
| `/history` | History.vue | 历史查询 |
| `/config/storage` | StorageConfig.vue | 存储配置 |
| `/config/forward` | ForwardConfig.vue | 转发配置 |
| `/config/system` | SystemConfig.vue | 系统设置 |
| `/logs` | Logs.vue | 日志中心 |

#### 6.3.4 `api/index.ts`

Axios 实例（`baseURL: '/api'`, `timeout: 10000`），导出四组 API：

| API 对象 | 方法 |
|----------|------|
| `deviceApi` | list / get / create / update / delete / reconnect / setStatus |
| `pointApi` | list / create / update / delete / batchImport / batchDelete |
| `dataApi` | getRealtime / getPointRealtime / getHistory |
| `configApi` | getTimeScaleDb / setTimeScaleDb / getRelational / setRelational / getSystem / setSystem |

#### 6.3.5 `stores/websocket.ts`

Pinia Store，管理 WebSocket 连接：

| 成员 | 说明 |
|------|------|
| `ws` / `connected` | 响应式状态 |
| `connect(url)` | 建立连接，`onclose` 时 3 秒自动重连 |
| `disconnect()` | 主动关闭 |
| `subscribe(deviceId, pointIds)` | 发送订阅消息 |
| `unsubscribe(deviceId, pointIds)` | 发送取消订阅 |
| `onMessage(handler)` | 注册消息处理器，返回取消注册函数 |

#### 6.3.6 视图组件说明

| 视图 | 功能 |
|------|------|
| `Login.vue` | 简单登录（默认 admin/admin，写入 `localStorage`） |
| `Home.vue` | 统计卡片（总设备/在线/离线/点位）+ 设备列表 |
| `Devices.vue` | 设备 CRUD 弹窗 + 重连 + 点位跳转 |
| `Points.vue` | 点位 CRUD 弹窗 + 批量导入（占位） |
| `Monitor.vue` | 选择设备 → 拉取实时数据表格 |
| `History.vue` | 设备 + 时间范围查询历史，导出 Excel（占位） |
| `StorageConfig.vue` | 时序库/关系库配置表单 |
| `ForwardConfig.vue` | 转发配置（前端占位，未对接后端） |
| `SystemConfig.vue` | 采集间隔/超时/重连/日志保留设置 |
| `Logs.vue` | 日志列表（前端占位，后端 API 未实现） |

---

## 7. 数据流程

### 7.1 采集 → 存储 → 推送（实时链路）

```
PLC 设备
  │  S7 协议 (TCP 102)
  ↓
CollectorService.CollectCycleAsync (每 1s)
  │  PlcConnection.ReadAsync(address)
  ↓
PointValue { DeviceId, PointId, Value, Timestamp, Quality }
  │
  ├─> MemoryCacheService.SetPointValue   (进程内缓存, 5min 过期)
  ├─> TimeSeriesService.WritePoint        (TimescaleDB 时序表 t_data_{deviceId})
  └─> WebSocketHandler.BroadcastPoint     (推送订阅客户端)
```

### 7.2 转发链路（关系库）

```
ForwardService.ForwardCycleAsync (每 10s)
  │  遍历活跃设备
  ↓
MemoryCacheService.GetPointValue (读取各点位最新缓存值)
  │
  ↓
组装 {point_code: value, ...} → JSON
  │
  ↓
INSERT r_data_{deviceId}(time, data JSONB)  (关系库 plc_data_forward)
  │
  ↓
外部系统查询 JSONB 宽表
```

### 7.3 前端查询链路

```
浏览器
  ├─ REST /api/devices/{id}/realtime  → MemoryCache (实时)
  ├─ REST /api/devices/{id}/history   → TimescaleDB (历史)
  └─ WebSocket /ws                    → 实时推送
```

---

## 8. 数据库设计

### 8.1 时序库（`plc_data`，TimescaleDB）

**配置表**（`sql/init.sql` 创建）：

```sql
CREATE EXTENSION IF NOT EXISTS timescaledb;

CREATE TABLE sys_config (
    key   VARCHAR(128) PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE logs (
    id      BIGSERIAL PRIMARY KEY,
    time    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    level   VARCHAR(16) NOT NULL,
    message TEXT NOT NULL
);
CREATE INDEX idx_logs_time  ON logs (time DESC);
CREATE INDEX idx_logs_level ON logs (level);
```

**时序数据表**（运行时由 `TimeSeriesService.EnsureTableAsync` 自动创建，每设备一张）：

```sql
CREATE TABLE t_data_{deviceId} (
    time     TIMESTAMPTZ NOT NULL,
    point_id VARCHAR(64) NOT NULL,
    value    DOUBLE PRECISION,
    quality  SMALLINT DEFAULT 0,
    PRIMARY KEY (time, point_id)
);
SELECT create_hypertable('t_data_{deviceId}', 'time', if_not_exists => TRUE);
```

### 8.2 关系库（`plc_data_forward`）

**宽表**（运行时由 `ForwardService.EnsureWideTable` 自动创建，每设备一张）：

```sql
CREATE TABLE r_data_{deviceId} (
    time TIMESTAMPTZ NOT NULL PRIMARY KEY,
    data JSONB NOT NULL
);
-- data 列存储 {"point_code": value, ...} 快照
```

### 8.3 配置持久化（文件）

| 文件 | 内容 | 管理者 |
|------|------|--------|
| `config/devices.json` | 设备列表 | `DeviceManager` |
| `config/points.json` | 点位列表 | `DeviceManager` |
| `config/system_config.json` | 系统配置 KV | `ConfigController` |

---

## 9. API 接口

### 9.1 设备管理

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/devices?page=1&size=20` | 分页列表 |
| GET | `/api/devices/{id}` | 详情 |
| POST | `/api/devices` | 新增 |
| PUT | `/api/devices/{id}` | 更新 |
| DELETE | `/api/devices/{id}` | 删除 |
| POST | `/api/devices/{id}/reconnect` | 触发重连 |
| PUT | `/api/devices/{id}/status` | 启停（body: `{"enabled": true}`） |

### 9.2 点位管理

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/devices/{deviceId}/points` | 列表 |
| POST | `/api/devices/{deviceId}/points` | 新增 |
| PUT | `/api/points/{id}` | 更新 |
| DELETE | `/api/points/{id}` | 删除 |
| POST | `/api/devices/{deviceId}/points/batch` | 批量导入 |
| POST | `/api/points/batch-delete` | 批量删除 |

### 9.3 数据接口

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/devices/{deviceId}/realtime` | 设备实时数据 |
| GET | `/api/points/{id}/realtime` | 单点位实时 |
| GET | `/api/devices/{deviceId}/history?pointIds=1,2&from=...&to=...` | 历史 |

### 9.4 配置接口

| 方法 | 路径 | 说明 |
|------|------|------|
| GET/PUT | `/api/config/timescaledb` | 时序库配置 |
| GET/PUT | `/api/config/relational` | 关系库配置 |
| GET/PUT | `/api/config/system` | 系统设置 |

---

## 10. WebSocket 协议

**端点**：`ws://host:5000/ws`

**客户端 → 服务端（订阅消息）**：
```json
{ "type": "subscribe", "deviceId": "1", "pointIds": ["1", "2"] }
{ "type": "unsubscribe", "deviceId": "1", "pointIds": ["1"] }
```

**服务端 → 客户端（数据推送）**：
```json
// 点位数据
{ "type": "data", "deviceId": "1", "pointId": "1", "value": 123.45, "ts": "2026-06-22T...", "quality": 0 }

// 设备状态
{ "type": "status", "deviceId": "1", "online": true }
```

`quality` 字段为 `QualityStatus` 枚举的 int 值（0=Good）。

---

## 11. 项目运行方式

### 11.1 开发环境

**必备工具**：

| 工具 | 版本 |
|------|------|
| .NET SDK | 8.0+ |
| Node.js | 20+ |
| PostgreSQL + TimescaleDB | 14+ / 2.x |

**本地调试**：

```bash
# 1. 还原后端依赖
dotnet restore

# 2. 还原前端依赖
cd frontend && npm install && cd ..

# 3. 启动数据库（Docker 方式）
docker run -d --name pg-ts \
  -e POSTGRES_PASSWORD=postgres \
  -p 5432:5432 \
  timescale/timescaledb:latest-pg16

# 4. 启动前端 (http://localhost:5173)
cd frontend && npm run dev

# 5. 启动后端 (http://localhost:5000)
dotnet run --project src/PLCDataCollector.Web
```

**开发期代理**：`vite.config.ts` 配置 `/api` → `http://localhost:5000`、`/ws` → `ws://localhost:5000`，前端通过 Vite 代理访问后端。

### 11.2 构建打包

```bash
# 首次：下载 PostgreSQL 便携版（~100MB，缓存到 pgsql-portable/）
scripts\setup-pgsql.bat

# 一键构建
build.bat
```

**`build.bat` 执行流程（8 步）**：
1. 检查 .NET SDK
2. 检查 Node.js
3. 清理旧构建产物
4. 检查 PostgreSQL 便携版缓存
5. NuGet 还原
6. 后端 self-contained 编译（`dotnet publish -r win-x64 --self-contained -p:PublishSingleFile`）
7. 前端 `npm run build`（vue-tsc 类型检查 + vite 打包）
8. 打包：前端 dist → `publish/service/wwwroot/`，SQL 脚本、安装脚本、PostgreSQL 便携版 → `publish/`

**输出目录**：`publish/`

### 11.3 一键部署（目标机）

1. 将 `publish/` 复制到目标 Windows 机器
2. 右键 `install.bat` → **以管理员身份运行**
3. 等待 2-3 分钟（首次需 initdb）
4. 浏览器自动打开 `http://localhost:5000`

**`install.bat` 自动完成（6 步）**：
1. 管理员权限检查
2. 配置 PostgreSQL（initdb → 改 postgresql.conf → 注册服务 `PLCDataCollectorDB` → 启动 → 等待就绪）
3. 初始化数据库（创建 `plc_data` → 设密码 → 运行 `init.sql`）
4. 写入连接串到 `service/appsettings.json`
5. 注册并启动 `PLCDataCollector` Windows Service + 配置防火墙（端口 5000）
6. 打开浏览器

**默认账号**：`admin / admin`（前端 localStorage 简单校验，无后端鉴权）

### 11.4 服务管理

```bash
net start PLCDataCollector      # 启动应用
net stop PLCDataCollector       # 停止应用
net start PLCDataCollectorDB    # 启动数据库
net stop PLCDataCollectorDB     # 停止数据库
uninstall.bat                   # 卸载全部（保留数据目录）
```

### 11.5 默认端口

| 服务 | 地址 |
|------|------|
| Web 管理页面 | `http://localhost:5000` |
| PostgreSQL | `127.0.0.1:5432` |
| 开发前端 | `http://localhost:5173` |

---

## 12. 配置说明

### 12.1 `appsettings.json`

```json
{
  "ConnectionStrings": {
    "TimeSeriesDb": "Host=127.0.0.1;Database=plc_data;Username=postgres;Password=",
    "RelationalDb": "Host=127.0.0.1;Database=plc_data_forward;Username=postgres;Password="
  },
  "Collector": {
    "CollectIntervalMs": 1000,      // 采集轮询间隔
    "TimeoutMs": 3000,              // 采集超时（预留）
    "ReconnectIntervalSec": 10,     // 重连间隔（预留）
    "ServiceDelayStartSec": 5       // 服务启动延迟
  },
  "Forward": {
    "IntervalSec": 10               // 转发周期
  }
}
```

### 12.2 运行时配置（`config/system_config.json`）

由 `ConfigController` 管理，前端"系统配置"页面读写。键值包括：
- `timescaledb_host/port/database/username`
- `relational_host/port/database/username`
- `collect_interval` / `timeout` / `reconnect_interval` / `log_retention_days`

> 注：当前运行时配置仅持久化到文件，**未热加载到采集/转发服务**（服务启动时从 `IConfiguration` 读取一次）。

---

## 13. 架构演进与版本历史

| 版本 | 日期 | 关键变更 |
|------|------|----------|
| 1.0.0 | 2026-05-20 | 初始版本：双进程架构（Service + Web），设备/点位 CRUD、实时监控、历史查询、配置管理、WebSocket、一键部署 |
| 1.1.0 | 2026-05-20 | **单进程合并**：Web + Service 合并消除内存隔离；移除 Redis 残留；DeviceManager JSON 持久化；修复 API 路由、ElMessage 导入、ForwardService 骨架 |
| 1.2.0 | 2026-05-20 | **Code Review 修复**：PlcConnection 改异步 API；DeviceManager 加 `ReaderWriterLockSlim`；WebSocketHandler 改 `ConcurrentDictionary`；ConfigController 加文件锁 + 系统配置返回 int；CollectorService 从配置读采集间隔；Core 移除 NLog；Web 移除 Newtonsoft → System.Text.Json；前端 vue-tsc 1.8→2.0，移除未用 echarts |
| 1.3.0 | 2026-05-20 | **真正一键部署**：部署包自带 PostgreSQL 16 + TimescaleDB 便携版；install.bat 全自动 initdb→建库→建表→注册服务→启动→开浏览器；目标机零依赖 |
| 1.3.1 | 2026-06-22 | **第 1+2 轮深度审计**：SQL 注入防护 (`QuoteIdentifier` + `^\d+$`)；设备在线状态回写 `UpdateOnlineStatus`；PlcConnection `IAsyncDisposable` + `WaitAsync` 超时；`ConcurrentDictionary` 采集中替换 `Dictionary`；`PointValue.DeviceId/PointId` int 统一；`NpgsqlDataSource` 连接池替换裸连接；`EnsureWideTable` 缓存；`UpdateOnlineStatus` 去抖保存；`GetOrCreateConnection TryAdd` 防泄漏 |
| 1.3.2 | 2026-06-22 | **第 3 轮审计**：`ConfigController` 新增 `SystemKeyMap` 前端 camelCase↔后端 snake_case 映射；`install.bat` 创建 `plc_data_forward` 独立关系库 + 双连接串；`build.bat` 错误处理语法重写 + 自动下载 PostgreSQL；`ForwardConfig`/`Logs`/`Points` 前端组件真实实现 |
| 1.4.0 | 2026-06-22 | **第 4+5 轮审计**：WebSocket disconnect 清理 `reconnectTimer`；Router 导航守卫 `beforeEach`；`SystemConfig`/`StorageConfig` save 错误处理；`AppConfig` 移除死 `DatabaseConfig` 类；`DEVELOPMENT.md`/`README.md` 全面更新；新增 `CODE_WIKI.md` 代码级架构文档。累 计 52 项修复，7 严重 / 12 高危 / 14 中等 / 4 低。 |

### 关键架构决策回顾

1. **双进程 → 单进程 (v1.1.0)**：解决 Web API 修改对采集进程不可见的问题，所有服务注册为单例共享内存。
2. **Redis → MemoryCache (v1.1.0)**：简化部署，消除外部依赖，进程内缓存满足实时性需求，v1.4.0 增加 50000 条容量上限。
3. **SQL 注入防护 (v1.3.1)**：动态表名使用 `NpgsqlCommandBuilder.QuoteIdentifier()` 保护，deviceId 用正则 `^\d+$` 校验。
4. **Type 统一 (v1.3.1)**：`DeviceId` / `PointId` 全栈从 `string` 改为 `int`，消除所有 `.ToString()` / `int.TryParse` 转换。
5. **NpgsqlDataSource 连接池 (v1.3.1)**：`TimeSeriesService` 从每次 `new NpgsqlConnection` 改为 `NpgsqlDataSource` 连接池，实现 `IDisposable`。
6. **API 契约映射 (v1.3.2)**：`ConfigController` 通过 `SystemKeyMap` 字典实现前端 camelCase 键到后端 snake_case 键的安全映射。
7. **同步 → 异步 (v1.2.0)**：PlcConnection 全面异步化，避免 sync-over-async 线程池问题。

---

> 本文档基于源码分析生成，覆盖项目架构、模块、类、API、数据流与运行方式。如代码变更请同步更新。
