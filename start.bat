@echo off
chcp 65001 >nul
title ServerScanner Pro — Touhou Edition 🔮
color 0D

:MENU
cls
echo ================================================================
echo        🔮✨ TOUHOU SERVER SCANNER PRO - ALICE EDITION ✨🔮
echo ================================================================
echo.
echo   [1] ⚡ Запуск Windows GUI Сканера (ServerScanner.exe)
echo   [2] 🌐 Открыть Веб-дашборд (index.html)
echo   [3] 💎 Запустить Ruby Сканер в консоли (src\Ruby.rb)
echo   [4] 📁 Открыть папку с данными и результатами (data)
echo   [5] 🖼️  Открыть папку с ассетами (assets)
echo   [6] 🛠️  Пересобрать ServerScanner.exe (src\Program.cs)
echo   [0] ❌ Выход
echo.
echo ================================================================
set /p choice="Выберите действие [1-6, 0]: "

if "%choice%"=="1" goto LAUNCH_GUI
if "%choice%"=="2" goto LAUNCH_WEB
if "%choice%"=="3" goto LAUNCH_RUBY
if "%choice%"=="4" goto OPEN_DATA
if "%choice%"=="5" goto OPEN_ASSETS
if "%choice%"=="6" goto REBUILD
if "%choice%"=="0" exit /b
goto MENU

:LAUNCH_GUI
start "" "ServerScanner.exe"
goto MENU

:LAUNCH_WEB
start "" "index.html"
goto MENU

:LAUNCH_RUBY
cls
echo Запуск Ruby сканера...
ruby src\Ruby.rb
echo.
pause
goto MENU

:OPEN_DATA
start "" "data"
goto MENU

:OPEN_ASSETS
start "" "assets"
goto MENU

:REBUILD
cls
echo Компиляция ServerScanner.exe из src\Program.cs...
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:ServerScanner.exe src\Program.cs
if %ERRORLEVEL% EQU 0 (
    echo [УСПЕХ] Файл ServerScanner.exe успешно пересобран!
) else (
    echo [ОШИБКА] Не удалось скомпилировать приложение.
)
echo.
pause
goto MENU
