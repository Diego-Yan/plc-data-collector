@echo off
chcp 65001 >nul
title PostgreSQL + TimescaleDB 便携版 — 下载工具
echo ============================================
echo   PostgreSQL 16 + TimescaleDB 便携版 下载
echo   运行一次即可，产物缓存在 pgsql-portable\
echo ============================================
echo.

set PG_VERSION=16.4-1
set PG_URL=https://get.enterprisedb.com/postgresql/postgresql-%PG_VERSION%-windows-x64-binaries.zip
set TS_VERSION=2.15.3
set TS_URL=https://github.com/timescale/timescaledb/releases/download/%TS_VERSION%/timescaledb-%TS_VERSION%-windows-amd64.zip

set CACHE_DIR=%~dp0..\pgsql-portable
set TEMP_DIR=%TEMP%\pgsql-setup

:: ---- cleanup temp ----
if exist "%TEMP_DIR%" rmdir /S /Q "%TEMP_DIR%"
mkdir "%TEMP_DIR%"

:: ---- check if already cached ----
if exist "%CACHE_DIR%\bin\pg_ctl.exe" (
    echo [OK] 已有缓存: %CACHE_DIR%
    echo         跳过下载。如需重新下载，请删除 pgsql-portable\ 目录。
    goto :done
)

:: ---- download PostgreSQL ----
echo [1/3] 下载 PostgreSQL %PG_VERSION% (~100MB) ...
curl -L -o "%TEMP_DIR%\pgsql.zip" "%PG_URL%" --progress-bar
if %errorlevel% neq 0 (
    echo [失败] 下载 PostgreSQL 失败。请检查网络或手动下载:
    echo        %PG_URL%
    echo        解压到: %CACHE_DIR%
    goto :cleanup
)
echo [OK] PostgreSQL 下载完成

:: ---- extract PostgreSQL ----
echo [2/3] 解压 PostgreSQL ...
powershell -Command "Expand-Archive -Path '%TEMP_DIR%\pgsql.zip' -DestinationPath '%TEMP_DIR%\pgsql' -Force"
if %errorlevel% neq 0 ( echo [失败] 解压失败; goto :cleanup )

:: PostgreSQL zip 内通常有一个 pgsql\ 顶层目录
if exist "%TEMP_DIR%\pgsql\pgsql" (
    move "%TEMP_DIR%\pgsql\pgsql\*" "%TEMP_DIR%\pgsql\" >nul 2>&1
    rmdir "%TEMP_DIR%\pgsql\pgsql"
)

:: ---- download TimescaleDB ----
echo [3/3] 下载 TimescaleDB %TS_VERSION% ...
curl -L -o "%TEMP_DIR%\timescaledb.zip" "%TS_URL%" --progress-bar
if %errorlevel% neq 0 (
    echo [警告] TimescaleDB 下载失败，将使用无 TimescaleDB 的 PostgreSQL
    echo        手动下载: %TS_URL%
    echo        解压到 %CACHE_DIR% 的 lib\ 和 share\extension\
) else (
    echo [OK] TimescaleDB 下载完成

    :: extract TimescaleDB
    powershell -Command "Expand-Archive -Path '%TEMP_DIR%\timescaledb.zip' -DestinationPath '%TEMP_DIR%\ts' -Force"
)

:: ---- merge into cache ----
echo 合并到缓存目录...
mkdir "%CACHE_DIR%" 2>nul
xcopy /E /Y /Q "%TEMP_DIR%\pgsql\*" "%CACHE_DIR%\" >nul

:: merge TimescaleDB DLLs and extension files
if exist "%TEMP_DIR%\ts\lib\*.dll" (
    copy /Y "%TEMP_DIR%\ts\lib\*.dll" "%CACHE_DIR%\lib\" >nul 2>&1
)
if exist "%TEMP_DIR%\ts\share\extension\*" (
    mkdir "%CACHE_DIR%\share\extension" 2>nul
    copy /Y "%TEMP_DIR%\ts\share\extension\*" "%CACHE_DIR%\share\extension\" >nul 2>&1
)

:: ---- done ----
:cleanup
if exist "%TEMP_DIR%" rmdir /S /Q "%TEMP_DIR%"

:done
if exist "%CACHE_DIR%\bin\pg_ctl.exe" (
    echo ============================================
    echo   [OK] PostgreSQL 便携版准备就绪
    echo       路径: %CACHE_DIR%
    echo ============================================
) else (
    echo ============================================
    echo   [警告] PostgreSQL 便携版未就绪
    echo   请手动下载并解压到: %CACHE_DIR%
    echo ============================================
)
echo.
pause
