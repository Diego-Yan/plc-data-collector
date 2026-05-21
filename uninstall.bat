@echo off
chcp 65001 >nul
title PLC 数据采集系统 — 卸载
echo ============================================
echo   PLC 数据采集系统 — 卸载
echo ============================================
echo.
:: TAG: one-click-deploy — 2026-05-20 — also cleans up PostgreSQL service

:: 检查管理员权限
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [错误] 请以管理员身份运行
    pause
    exit /b 1
)

:: 停止并删除应用服务
echo [1/3] 卸载应用服务...
net stop PLCDataCollector >nul 2>&1
sc delete PLCDataCollector >nul 2>&1
echo [OK] 应用服务已卸载

:: 停止并删除数据库服务
echo [2/3] 卸载数据库服务...
net stop PLCDataCollectorDB >nul 2>&1
sc delete PLCDataCollectorDB >nul 2>&1
echo [OK] 数据库服务已卸载

:: 防火墙规则
echo [3/3] 清理防火墙规则...
netsh advfirewall firewall delete rule name="PLCDataCollector Web" >nul 2>&1
echo [OK] 防火墙规则已清理

:: 提示数据目录
if exist "%~dp0data\pgdata" (
    echo.
    echo   数据目录保留在: %~dp0data\pgdata\
    echo   如需彻底清除，请手动删除 data\ 目录。
)

echo ============================================
echo   卸载完成
echo ============================================
pause
