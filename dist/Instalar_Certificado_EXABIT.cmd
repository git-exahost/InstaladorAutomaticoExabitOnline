@echo off
chcp 65001 >nul
title Instala‡Æo do Certificado Digital EXABIT

echo ======================================================================
echo  INSTALA€ÇO DE CERTIFICADO DE CONFIAN€A - EXABIT SOLU€åES EM TI
echo ======================================================================
echo.

REM Verifica se est  executando como Administrador
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [!] Solicitando privil‚gios de Administrador...
    powershell -NoProfile -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

set "CER_FILE=%~dp0exabit_cert.cer"

if not exist "%CER_FILE%" (
    echo [X] Erro: Arquivo de certificado '%CER_FILE%' nÆo encontrado!
    echo.
    pause
    exit /b 1
)

echo Instalando certificado digital nos reposit¢rios de confian‡a do Windows...
echo.

certutil -addstore -f "Root" "%CER_FILE%" >nul
if %errorlevel% equ 0 (
    echo [ok] Adicionado a: Autoridades de Certifica‡Æo Raiz Confi veis
) else (
    echo [X] Falha ao adicionar … Raiz Confi vel
)

certutil -addstore -f "TrustedPublisher" "%CER_FILE%" >nul
if %errorlevel% equ 0 (
    echo [ok] Adicionado a: Editores Confi veis (Trusted Publishers)
) else (
    echo [X] Falha ao adicionar a Editores Confi veis
)

echo.
echo ======================================================================
echo  CONCLUÖDO COM SUCESSO!
echo  O instalador da EXABIT agora ‚ reconhecido como Editor Confi vel
echo  pelo Windows Defender, Kaspersky e outros sistemas de seguran‡a.
echo ======================================================================
echo.
pause
