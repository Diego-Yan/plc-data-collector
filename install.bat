@echo off
chcp 65001 >nul
title PLC 数据采集系统 — 一键安装
echo ============================================
echo   PLC 数据采集系统 — 一键安装
echo ============================================
echo.
:: TAG: one-click-deploy — 2026-05-20
:: Fully automated: embedded PostgreSQL + TimescaleDB + Windows Service + Web

set "SERVICE_NAME=PLCDataCollector"
set "PG_SERVICE_NAME=PLCDataCollectorDB"
set "PG_PORT=5432"
set "PG_USER=postgres"
set "PG_PASSWORD=plcdata2024"
set "PG_DATABASE=plc_data"
set "WEB_PORT=5000"
set "INSTALL_DIR=%~dp0"
set "PG_DIR=%INSTALL_DIR%pgsql"
set "PG_DATA=%INSTALL_DIR%data\pgdata"
set "SQL_DIR=%INSTALL_DIR%sql"

:: ====================================================================
:: 1. 管理员权限检查
:: ====================================================================
echo [1/6] 检查管理员权限...
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [错误] 请以管理员身份运行！
    echo        右键 install.bat → "以管理员身份运行"
    pause
    exit /b 1
)
echo [OK] 管理员权限

:: ====================================================================
:: 2. 检查/设置 PostgreSQL
:: ====================================================================
echo [2/6] 配置数据库...

:: 检查是否已有 PostgreSQL 运行中
set PG_READY=0
"%PG_DIR%\bin\pg_isready.exe" -h 127.0.0.1 -p %PG_PORT% -q >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] 检测到 PostgreSQL 已在运行 (端口 %PG_PORT%)
    set PG_READY=1
    goto :db_setup
)

:: 检查系统是否已有 PostgreSQL 服务
sc query postgresql-x64-16 >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] 检测到系统 PostgreSQL 服务，尝试启动...
    net start postgresql-x64-16 >nul 2>&1
    "%PG_DIR%\bin\pg_isready.exe" -h 127.0.0.1 -p %PG_PORT% -q >nul 2>&1
    if %errorlevel% equ 0 (
        set PG_READY=1
        goto :db_setup
    )
)

:: 使用自带的 PostgreSQL 便携版
if not exist "%PG_DIR%\bin\pg_ctl.exe" (
    echo [错误] 未找到 PostgreSQL 便携版，且系统无可用 PostgreSQL。
    echo.
    echo   请确保以下之一：
    echo   A) 构建时运行了 scripts\setup-pgsql.bat 以下载 PostgreSQL 便携版
    echo   B) 目标机器已安装 PostgreSQL 14+ + TimescaleDB
    echo.
    pause
    exit /b 1
)

echo [OK] 使用部署包自带的 PostgreSQL 便携版

:: 初始化数据目录
if not exist "%PG_DATA%" (
    echo   首次运行 — 初始化 PostgreSQL 数据目录...
    mkdir "%PG_DATA%" 2>nul
    "%PG_DIR%\bin\initdb.exe" -D "%PG_DATA%" -U %PG_USER% --auth=trust --no-locale >nul 2>&1
    if %errorlevel% neq 0 (
        echo [错误] initdb 失败
        pause
        exit /b 1
    )
    echo   [OK] 数据目录已初始化: %PG_DATA%
)

:: 配置 postgresql.conf
echo   应用配置...
set "PGCONF=%PG_DATA%\postgresql.conf"
:: 端口
powershell -Command "(Get-Content '%PGCONF%') -replace '#?port\s*=\s*\d+', 'port = %PG_PORT%'" | powershell -Command "$input | Set-Content '%PGCONF%'"
:: 监听地址
powershell -Command "(Get-Content '%PGCONF%') -replace '#?listen_addresses\s*=\s*''.*''', 'listen_addresses = ''127.0.0.1'''" | powershell -Command "$input | Set-Content '%PGCONF%'"
:: shared_preload_libraries (TimescaleDB)
findstr /C:"shared_preload_libraries" "%PGCONF%" >nul 2>&1
if %errorlevel% neq 0 (
    echo shared_preload_libraries = 'timescaledb' >> "%PGCONF%"
)

:: 注册为 Windows Service（如果尚未注册）
sc query %PG_SERVICE_NAME% >nul 2>&1
if %errorlevel% neq 0 (
    echo   注册 PostgreSQL Windows Service...
    "%PG_DIR%\bin\pg_ctl.exe" register -N %PG_SERVICE_NAME% -D "%PG_DATA%" -S auto >nul 2>&1
    echo   [OK] 服务已注册: %PG_SERVICE_NAME%
)

:: 启动 PostgreSQL
echo   启动 PostgreSQL...
sc query %PG_SERVICE_NAME% | findstr /C:"RUNNING" >nul
if %errorlevel% neq 0 (
    net start %PG_SERVICE_NAME% >nul 2>&1
)

:: 等待 PostgreSQL 就绪
echo   等待 PostgreSQL 就绪...
set /a RETRY=0
:wait_pg
timeout /t 2 /nobreak >nul
"%PG_DIR%\bin\pg_isready.exe" -h 127.0.0.1 -p %PG_PORT% -q >nul 2>&1
if %errorlevel% equ 0 (
    set PG_READY=1
    echo   [OK] PostgreSQL 已就绪
    goto :db_setup
)
set /a RETRY+=1
if %RETRY% LSS 15 goto :wait_pg
echo [错误] PostgreSQL 启动超时
pause
exit /b 1

:: ====================================================================
:: 3. 创建数据库 + 初始化
:: ====================================================================
:db_setup
echo [3/6] 初始化数据库...
set "PSQL=%PG_DIR%\bin\psql.exe -h 127.0.0.1 -p %PG_PORT% -U %PG_USER%"

:: 创建数据库（如果不存在）
echo   创建数据库...
echo SELECT 'CREATE DATABASE %PG_DATABASE%' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '%PG_DATABASE%')\g | "%PSQL%" -d postgres >nul 2>&1
%PSQL% -d postgres -c "CREATE DATABASE %PG_DATABASE%;" >nul 2>&1
echo   [OK] 数据库: %PG_DATABASE%

:: 设置密码
%PSQL% -d postgres -c "ALTER USER %PG_USER% PASSWORD '%PG_PASSWORD%';" >nul 2>&1

:: 运行初始化 SQL（TimescaleDB 扩展 + 配置表）
if exist "%SQL_DIR%\init.sql" (
    echo   运行初始化脚本...
    %PSQL% -d %PG_DATABASE% -f "%SQL_DIR%\init.sql" >nul 2>&1
    if %errorlevel% neq 0 (
        echo   [警告] init.sql 部分语句失败，可能 TimescaleDB 扩展未安装
        echo          时序功能将不可用，但系统可继续运行
    ) else (
        echo   [OK] 初始化完成
    )
)

:: ====================================================================
:: 4. 写入连接配置到 appsettings
:: ====================================================================
echo [4/6] 写入连接配置...
set "CONNSTR=Host=127.0.0.1;Port=%PG_PORT%;Database=%PG_DATABASE%;Username=%PG_USER%;Password=%PG_PASSWORD%"
set "APPSETTINGS=%INSTALL_DIR%service\appsettings.json"

:: 用 PowerShell 更新 JSON（比手动字符串替换更可靠）
powershell -Command ^
  "$json = Get-Content '%APPSETTINGS%' -Raw | ConvertFrom-Json; ^
   $json.ConnectionStrings.TimeSeriesDb = '%CONNSTR%'; ^
   $json.ConnectionStrings.RelationalDb = '%CONNSTR%'; ^
   $json | ConvertTo-Json | Set-Content '%APPSETTINGS%'"

if %errorlevel% neq 0 (
    echo [警告] 无法更新 appsettings.json，将使用默认连接串
)
echo [OK] 连接配置已写入

:: ====================================================================
:: 5. 注册并启动 PLCDataCollector 服务
:: ====================================================================
echo [5/6] 注册应用服务...

if not exist "%INSTALL_DIR%service\PLCDataCollector.Web.exe" (
    echo [错误] 未找到 PLCDataCollector.Web.exe
    pause
    exit /b 1
)

:: 配置防火墙规则
netsh advfirewall firewall add rule name="PLCDataCollector Web" dir=in action=allow protocol=TCP localport=%WEB_PORT% >nul 2>&1

:: 注册服务
sc query %SERVICE_NAME% >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] 服务已存在，跳过注册
) else (
    sc create %SERVICE_NAME% binPath="%INSTALL_DIR%service\PLCDataCollector.Web.exe" start=auto >nul
    sc description %SERVICE_NAME% "西门子 S7 系列 PLC 数据采集系统" >nul
    sc config %SERVICE_NAME% start=auto >nul
    echo [OK] 服务已注册
)

:: 启动服务
echo   启动服务...
net start %SERVICE_NAME% >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] 服务已启动
) else (
    echo [警告] 服务启动失败，请检查事件查看器
)

:: ====================================================================
:: 6. 完成
:: ====================================================================
echo [6/6] 安装完成
echo ============================================
echo   安装完成！
echo ============================================
echo.
echo   访问地址: http://localhost:%WEB_PORT%
echo   默认账号: admin / admin
echo.
echo   数据库信息:
echo     端口: %PG_PORT%
echo     用户: %PG_USER%
echo     密码: %PG_PASSWORD%
echo     数据库: %PG_DATABASE%
echo.
echo   管理命令:
echo     net start %SERVICE_NAME%      启动应用
echo     net stop %SERVICE_NAME%       停止应用
echo     net start %PG_SERVICE_NAME%   启动数据库
echo     net stop %PG_SERVICE_NAME%    停止数据库
echo     uninstall.bat                 卸载全部
echo.
start http://localhost:%WEB_PORT%
pause
