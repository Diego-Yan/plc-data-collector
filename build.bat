@echo off
chcp 65001 >nul
title PLC 数据采集系统 - 构建打包工具
echo ============================================
echo   PLC 数据采集系统 - 一键构建与打包
echo ============================================
echo.
:: TAG: one-click-deploy — 2026-05-20 — 8 steps, bundles PostgreSQL portable

:: [1/8] 检查 .NET SDK
echo [1/8] 检查 .NET SDK...
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [失败] 未检测到 .NET SDK
    echo 请从 https://dotnet.microsoft.com/download/dotnet/8.0 安装 .NET 8 SDK
    pause
    exit /b 1
)
for /f %%i in ('dotnet --version') do set DOTNET_VER=%%i
echo [OK] .NET SDK %DOTNET_VER%

:: [2/8] 检查 Node.js
echo [2/8] 检查 Node.js...
node --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [失败] 未检测到 Node.js
    echo 请从 https://nodejs.org 安装 Node.js 20+
    pause
    exit /b 1
)
for /f %%i in ('node --version') do set NODE_VER=%%i
echo [OK] Node.js %NODE_VER%

:: [3/8] 清理旧构建产物
echo [3/8] 清理旧构建产物...
if exist "publish\"          rmdir /S /Q publish
if exist "src\PLCDataCollector.Web\bin\"  rmdir /S /Q src\PLCDataCollector.Web\bin
if exist "src\PLCDataCollector.Web\obj\"  rmdir /S /Q src\PLCDataCollector.Web\obj
if exist "src\PLCDataCollector.Core\bin\" rmdir /S /Q src\PLCDataCollector.Core\bin
if exist "src\PLCDataCollector.Core\obj\" rmdir /S /Q src\PLCDataCollector.Core\obj
echo [OK] 清理完成

:: [4/8] 检查 PostgreSQL 便携版缓存
echo [4/8] 检查 PostgreSQL 便携版...
if exist "pgsql-portable\bin\pg_ctl.exe" (
    echo [OK] 已找到 PostgreSQL 便携版缓存，将打包进部署包
) else (
    echo [提示] 未找到 pgsql-portable\ 缓存目录
    echo.
    echo         如需一键部署（自带数据库），请先运行:
    echo             scripts\setup-pgsql.bat
    echo.
    echo         该脚本会下载 PostgreSQL 16 + TimescaleDB ~100MB
    echo         下载一次后缓存到 pgsql-portable\，后续构建直接复用。
    echo.
    echo         如果跳过此步骤，部署包将不含数据库——
    echo         目标机器需自行安装 PostgreSQL 14+ + TimescaleDB。
    echo.
    choice /C YN /M "跳过数据库打包，继续构建？"
    if errorlevel 2 exit /b 1
)

:: [5/8] 还原 NuGet
echo [5/8] 还原 NuGet 依赖...
dotnet restore PLCDataCollector.sln >nul 2>&1
if %errorlevel% neq 0 ( echo [失败]; pause; exit /b 1 )
echo [OK] NuGet 依赖已还原

:: [6/8] 编译后端 (Self-Contained, single project)
echo [6/8] 编译后端...
dotnet publish src\PLCDataCollector.Web\PLCDataCollector.Web.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\service >nul 2>&1
if %errorlevel% neq 0 ( echo [失败]; pause; exit /b 1 )
echo [OK] 后端编译完成（单进程 Web+Service）

:: [7/8] 编译前端
echo [7/8] 编译前端...
cd frontend
if not exist "node_modules" call npm install >nul 2>&1
call npm run build >nul 2>&1
if %errorlevel% neq 0 ( cd ..; echo [失败]; pause; exit /b 1 )
cd ..
echo [OK] 前端编译完成

:: [8/8] 打包
echo [8/8] 打包部署...
:: 前端
if not exist "publish\service\wwwroot" mkdir publish\service\wwwroot
xcopy /E /Y /Q frontend\dist\* publish\service\wwwroot\ >nul
:: SQL 初始化脚本
if not exist "publish\sql" mkdir publish\sql
copy sql\init.sql publish\sql\ >nul
:: 安装/卸载脚本
copy install.bat publish\ >nul
copy uninstall.bat publish\ >nul
:: PostgreSQL 便携版（如果存在）
if exist "pgsql-portable\bin\pg_ctl.exe" (
    echo   正在打包 PostgreSQL 便携版...
    mkdir publish\pgsql 2>nul
    xcopy /E /Y /Q pgsql-portable\* publish\pgsql\ >nul
    echo   [OK] PostgreSQL 已打包
) else (
    echo   [跳过] PostgreSQL 未打包（部署时需要手动配置数据库）
)
:: 清理调试符号
if exist "publish\service\*.pdb" del /Q publish\service\*.pdb >nul 2>&1
echo [OK] 打包完成

echo ============================================
echo   构建完成！输出目录: publish\
echo ============================================
if exist "publish\pgsql\bin\pg_ctl.exe" (
    echo   部署包含 PostgreSQL 便携版 — 支持真正一键部署
)
echo.
echo 部署：
echo   1. 将 publish\ 复制到目标 Windows 机器
echo   2. 右键 install.bat → 以管理员身份运行
echo   3. 浏览器打开 http://localhost:5000
echo.
echo   打包为 Zip:
echo     powershell Compress-Archive -Path publish\* -Dest PLCDataCollector.zip
pause
