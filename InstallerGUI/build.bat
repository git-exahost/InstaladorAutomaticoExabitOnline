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

REM Compila todos os arquivos .cs
%CSC% /target:winexe ^
      /out:..\dist\install.exe ^
      /win32manifest:app.manifest ^
      /platform:anycpu ^
      /highentropyva+ ^
      /reference:System.dll ^
      /reference:System.Core.dll ^
      /reference:System.Drawing.dll ^
      /reference:System.Windows.Forms.dll ^
      /reference:Microsoft.CSharp.dll ^
      /reference:System.ServiceProcess.dll ^
      /reference:System.IO.Compression.dll ^
      /reference:System.IO.Compression.FileSystem.dll ^
      /resource:Image\logo_exabit.png,InstallerGUI.logo_exabit.png ^
      /resource:Sound\R2D2.mp3,InstallerGUI.R2D2.mp3 ^
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
    copy /Y "..\dist\install.exe" "bin\Release\install.exe" >nul 2>nul

    REM Copia o arquivo de configuracao DPI (.config)
    if exist "app.config" (
        copy /Y "app.config" "bin\Release\install.exe.config" >nul
        copy /Y "app.config" "..\dist\install.exe.config" >nul
    )

    REM Copia a pasta de som para a pasta dist
    if exist "Sound" (
        if not exist "..\dist\Sound" mkdir "..\dist\Sound"
        copy /Y "Sound\R2D2.mp3" "..\dist\Sound\R2D2.mp3" >nul
    )

    echo Destino: ..\dist\install.exe
    for %%I in ("..\dist\install.exe") do echo Tamanho: %%~zI bytes
    echo.
    echo %DATE% %TIME% > "..\dist\build_info.txt"

    REM Assinatura digital Authenticode corporativa
    echo Aplicando assinatura digital Authenticode...
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
echo Para executar: dist\install.exe
echo.
pause
