@echo off
chcp 65001 >nul
title Instalação do Certificado Digital EXABIT

echo ======================================================================
echo  INSTALAÇÃO DE CERTIFICADO DE CONFIANÇA - EXABIT SOLUÇÕES EM TI
echo ======================================================================
echo.

REM Verifica se está executando como Administrador
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [!] Solicitando privilégios de Administrador...
    powershell -NoProfile -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

set "CER_FILE=%~dp0exabit_cert.cer"

if not exist "%CER_FILE%" (
    echo [X] Erro: Arquivo de certificado '%CER_FILE%' não encontrado!
    echo.
    pause
    exit /b 1
)

echo Instalando certificado digital nos repositórios de confiança do Windows...
echo.

certutil -addstore -f "Root" "%CER_FILE%" >nul
if %errorlevel% equ 0 (
    echo [ok] Adicionado a: Autoridades de Certificação Raiz Confiáveis
) else (
    echo [X] Falha ao adicionar à Raiz Confiável
)

certutil -addstore -f "TrustedPublisher" "%CER_FILE%" >nul
if %errorlevel% equ 0 (
    echo [ok] Adicionado a: Editores Confiáveis (Trusted Publishers)
) else (
    echo [X] Falha ao adicionar a Editores Confiáveis
)

echo.
echo ======================================================================
echo  CONCLUÍDO COM SUCESSO!
echo  O instalador da EXABIT agora é reconhecido como Editor Confiável
echo  pelo Windows Defender, Kaspersky e outros sistemas de segurança.
echo ======================================================================
echo.
pause
