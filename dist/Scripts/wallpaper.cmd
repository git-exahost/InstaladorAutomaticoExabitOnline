@echo off
chcp 65001 >nul
echo ============================================
echo  Configurando Papeis de Parede Personalizados
echo ============================================
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0apply_wallpapers.ps1" -BasePath "%~dp0.."
