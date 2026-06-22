@echo off
chcp 65001 >nul
title PLCDataCollector Build
echo ============================================
echo   PLC 数据采集系统 — 一键构建
echo ============================================
echo.
:: TAG: one-click-build — v1.3.0
:: TAG: review-fix-3 — 2026-06-22 — fix error-handling syntax (; → multiple lines),
::      show build output on failure for diagnosis.

cd /d "%~dp0"

:: ====================================================================
:: 1. 检查环境
:: ====================================================================
echo [1/8] 检查编译环境...

where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo [错误] 未找到 .NET SDK
    echo        请安装 .NET 8 SDK: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)
echo [OK] .NET SDK

where node >nul 2>&1
if %errorlevel% neq 0 (
    echo [错误] 未找到 Node.js
    echo        请安装 Node.js 20+: https://nodejs.org
    pause
    exit /b 1
)
echo [OK] Node.js

:: ====================================================================
:: 2. 清理
:: ====================================================================
echo [2/8] 清理旧构建产物...
if exist "publish" rmdir /s /q "publish" 2>nul
if exist "frontend\dist" rmdir /s /q "frontend\dist" 2>nul
echo [OK] 清理完成

:: ====================================================================
:: 3. 检查 PostgreSQL 便携版
:: ====================================================================
echo [3/8] 检查 PostgreSQL 便携版缓存...
set "PG_CACHE=pgsql-portable"
if exist "%PG_CACHE%\bin\pg_ctl.exe" (
    echo [OK] 已缓存
) else (
    echo [信息] 未缓存，正在下载...
    call scripts\setup-pgsql.bat
    if %errorlevel% neq 0 (
        echo [错误] PostgreSQL 便携版下载失败
        pause
        exit /b 1
    )
)

:: ====================================================================
:: 4. 还原 NuGet 包
:: ====================================================================
echo [4/8] 还原 NuGet 包...
dotnet restore PLCDataCollector.sln >nul 2>&1
if %errorlevel% neq 0 (
    echo [错误] NuGet 还原失败
    dotnet restore PLCDataCollector.sln
    pause
    exit /b 1
)
echo [OK] NuGet 还原完成

:: ====================================================================
:: 5. 编译后端 (self-contained single-file)
:: ====================================================================
echo [5/8] 编译后端 (win-x64, self-contained)...
dotnet publish src/PLCDataCollector.Web/PLCDataCollector.Web.csproj ^
    -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o publish/service >build-backend.log 2>&1
if %errorlevel% neq 0 (
    echo [错误] 后端编译失败
    echo --- 错误日志 ---
    type build-backend.log
    pause
    exit /b 1
)
echo [OK] 后端编译完成
del build-backend.log 2>nul

:: ====================================================================
:: 6. 构建前端 (vue-tsc + vite)
:: ====================================================================
echo [6/8] 构建前端 (TypeScript + Vite)...
cd frontend
if not exist "node_modules" (
    echo     安装前端依赖...
    call npm install >..\build-frontend.log 2>&1
    if %errorlevel% neq 0 (
        cd ..
        echo [错误] npm install 失败
        type build-frontend.log
        pause
        exit /b 1
    )
)

call npm run build >..\build-frontend.log 2>&1
if %errorlevel% neq 0 (
    cd ..
    echo [错误] 前端构建失败
    echo --- 错误日志 ---
    type build-frontend.log
    pause
    exit /b 1
)
cd ..
echo [OK] 前端构建完成
del build-frontend.log 2>nul

:: ====================================================================
:: 7. 打包出产物
:: ====================================================================
echo [7/8] 打包出产物...

:: 前端 → wwwroot
if not exist "publish\service\wwwroot" mkdir "publish\service\wwwroot"
xcopy /E /Y "frontend\dist\*" "publish\service\wwwroot\" >nul

:: SQL 脚本
if not exist "publish\sql" mkdir "publish\sql"
copy /Y "sql\init.sql" "publish\sql\" >nul

:: 安装/卸载脚本
copy /Y "install.bat" "publish\" >nul
copy /Y "uninstall.bat" "publish\" >nul

:: PostgreSQL 便携版
if not exist "publish\pgsql" mkdir "publish\pgsql"
xcopy /E /Y "%PG_CACHE%\*" "publish\pgsql\" >nul

echo [OK] 打包完成

:: ====================================================================
:: 8. 完成
:: ====================================================================
echo [8/8] 构建完成！
echo ============================================
echo   输出目录: %CD%\publish\
echo ============================================
echo.
echo   部署说明:
echo     1. 将 publish\ 目录复制到目标 Windows 机器
echo     2. 右键 install.bat → "以管理员身份运行"
echo     3. 浏览器自动打开 http://localhost:5000
echo.
pause
