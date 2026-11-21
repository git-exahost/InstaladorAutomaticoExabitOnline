@echo off
chcp 65001 >nul
title Compilando Auto Instalador de Programas...

echo ============================================
echo  Compilando Auto Instalador de Programas (C# WinForms)
echo ============================================
echo.

REM Localiza o compilador C# do .NET Framework
set CSC="%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist %CSC% (
    set CSC="%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

echo Compilador: %CSC%
echo.

REM Cria diretÃ³rio de saÃ­da se nÃ£o existir
if not exist "bin\Release" mkdir "bin\Release"

REM Limpa executavel antigo de nome legado se existir
if exist "..\dist\install.exe" del /f /q "..\dist\install.exe" >nul 2>nul
if exist "..\dist\install.exe.config" del /f /q "..\dist\install.exe.config" >nul 2>nul

REM Compila todos os arquivos .cs
%CSC% /target:winexe ^
      /out:"..\dist\Instalador Automatico Exabit.exe" ^
      /win32manifest:app.manifest ^
      /platform:anycpu ^
      /highentropyva+ ^
      /reference:System.dll ^
      /reference:System.Core.dll ^
      /reference:System.Drawing.dll ^
      /reference:System.Windows.Forms.dll ^
      /reference:Microsoft.CSharp.dll ^
      /reference:System.ServiceProcess.dll ^
      /reference:System.Management.dll ^
      /reference:System.IO.Compression.dll ^
      /reference:System.IO.Compression.FileSystem.dll ^
      /resource:Image\logo_exabit.png,InstallerGUI.logo_exabit.png ^
      /resource:Sound\R2D2.mp3,InstallerGUI.R2D2.mp3 ^
      /resource:Scripts\apply_wallpapers.ps1,InstallerGUI.apply_wallpapers.ps1 ^
      /win32icon:install.ico ^
      /optimize+ ^
      /nologo ^
      Program.cs ^
      Forms\MainForm.cs ^
      Models\ProgramInfo.cs ^
      Services\InstallerService.cs ^
      Properties\AssemblyInfo.cs

REM Verifica resultado
if %errorlevel% equ 0 (
    echo.
    echo ============================================
    echo  Compilacao concluida com sucesso!
    echo ============================================
    echo.

    REM Copia tambem para bin\Release
    copy /Y "..\dist\Instalador Automatico Exabit.exe" "bin\Release\Instalador Automatico Exabit.exe" >nul 2>nul

    REM Copia o arquivo de configuracao DPI (.config)
    if exist "app.config" (
        copy /Y "app.config" "bin\Release\Instalador Automatico Exabit.exe.config" >nul
        copy /Y "app.config" "..\dist\Instalador Automatico Exabit.exe.config" >nul
    )


    echo Destino: ..\dist\Instalador Automatico Exabit.exe
    for %%I in ("..\dist\Instalador Automatico Exabit.exe") do echo Tamanho: %%~zI bytes
    echo.
    echo %DATE% %TIME% > "..\dist\build_info.txt"

    REM Assinatura digital Authenticode opcional - requer certificado corporativo real
    echo Aplicando assinatura Authenticode - certificado real opcional...
    powershell -NoProfile -ExecutionPolicy Bypass -File sign.ps1
    echo.
) else (
    echo.
    echo  X  Erro na compilacao. Verifique os erros acima.
    echo.
    pause
    exit /b 1
)

echo.
echo Para executar: "dist\Instalador Automatico Exabit.exe"
echo.
pause
