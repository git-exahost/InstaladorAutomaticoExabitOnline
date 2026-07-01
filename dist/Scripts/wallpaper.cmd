@echo off
chcp 65001 >nul
echo ============================================
echo  Atualizando papel de parede do Windows
echo ============================================
echo.

setlocal enabledelayedexpansion

REM Caminhos principais
set "SRC_WALL=%~dp0Wallpapers\img0.jpg"
set "DST_WALL=%SystemRoot%\Web\Wallpaper\Windows\img0.jpg"
set "THEME_PATH=%AppData%\Microsoft\Windows\Themes"
set "DEFAULT_NTUSER=%SystemDrive%\Users\Default\NTUSER.DAT"

if not exist "%SRC_WALL%" (
    echo ✖ ERRO: Imagem "%SRC_WALL%" não encontrada.
    pause
    exit /b
)

echo Encerrando Explorer para liberar arquivos...
taskkill /f /im explorer.exe >nul 2>&1
echo.

echo Substituindo imagem padrão do Windows...
takeown /f "%DST_WALL%" >nul 2>&1
icacls "%DST_WALL%" /grant Administradores:F /t >nul 2>&1
copy /Y "%SRC_WALL%" "%DST_WALL%" >nul
echo [ok]] Imagem copiada para: %DST_WALL%
echo.

echo Atualizando registro do usuário atual...
reg add "HKCU\Control Panel\Desktop" /v Wallpaper /t REG_SZ /d "%DST_WALL%" /f >nul
reg add "HKCU\Control Panel\Desktop" /v WallpaperStyle /t REG_SZ /d 10 /f >nul
reg add "HKCU\Control Panel\Desktop" /v TileWallpaper /t REG_SZ /d 0 /f >nul
echo [ok] Registro atualizado.
echo.

echo Atualizando cache do usuário (TranscodedWallpaper)...
if exist "%THEME_PATH%\TranscodedWallpaper" (
    del /f /q "%THEME_PATH%\TranscodedWallpaper" >nul 2>&1
)
copy /Y "%SRC_WALL%" "%THEME_PATH%\TranscodedWallpaper" >nul 2>&1
echo [ok] Cache de papel de parede sincronizado.
echo.

echo Aplicando o papel de parede imediatamente...
RUNDLL32.EXE user32.dll,SystemParametersInfoW 20,0,"%DST_WALL%",3
echo [ok] Papel de parede aplicado.
echo.

echo Definindo imagem padrão para novos usuários...
if exist "%DEFAULT_NTUSER%" (
    reg load "HKU\DefaultUser" "%DEFAULT_NTUSER%" >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        reg add "HKU\DefaultUser\Control Panel\Desktop" /v Wallpaper /t REG_SZ /d "%DST_WALL%" /f >nul
        reg add "HKU\DefaultUser\Control Panel\Desktop" /v WallpaperStyle /t REG_SZ /d 10 /f >nul
        reg add "HKU\DefaultUser\Control Panel\Desktop" /v TileWallpaper /t REG_SZ /d 0 /f >nul
        reg unload "HKU\DefaultUser" >nul 2>&1
        echo [ok] Definido também para novos perfis.
    ) else (
        echo X Falha ao carregar NTUSER.DAT do perfil Default.
    )
) else (
    echo [!] NTUSER.DAT do perfil Default não encontrado.
)
echo.

echo Reiniciando Explorer...
start explorer.exe
echo.
echo ============================================
echo [ok] Papel de parede atualizado com sucesso!
echo ============================================

