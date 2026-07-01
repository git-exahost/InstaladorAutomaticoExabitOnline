::[Bat To Exe Converter]
::
::YAwzoRdxOk+EWAjk
::fBw5plQjdCuDJHaK8WYiIQ1RcDSQM2yoB7gd+9TL/eWAsEwQVfF/KsGJ5qecI9wa5FbwdJohmHNZl6s=
::YAwzuBVtJxjWCl3EqQJgSA==
::ZR4luwNxJguZRRnk
::Yhs/ulQjdF+5
::cxAkpRVqdFKZSDk=
::cBs/ulQjdF+5
::ZR41oxFsdFKZSDk=
::eBoioBt6dFKZSDk=
::cRo6pxp7LAbNWATEpCI=
::egkzugNsPRvcWATEpCI=
::dAsiuh18IRvcCxnZtBJQ
::cRYluBh/LU+EWAnk
::YxY4rhs+aU+IeA==
::cxY6rQJ7JhzQF1fEqQJQ
::ZQ05rAF9IBncCkqN+0xwdVsGAlbi
::ZQ05rAF9IAHYFVzEqQJQ
::eg0/rx1wNQPfEVWB+kM9LVsJDC2ML3+7CrQY5/q7ytKmgGQpNA==
::fBEirQZwNQPfEVWB+kM9LVsJDGQ=
::cRolqwZ3JBvQF1fEqQJQ
::dhA7uBVwLU+EWHy81WAZHHs=
::YQ03rBFzNR3SWATElA==
::dhAmsQZ3MwfNWATEx0ciLxJbDDaNP2O7Zg==
::ZQ0/vhVqMQ3MEVWAtB9wSA==
::Zg8zqx1/OA3MEVWAtB9wSA==
::dhA7pRFwIByZRRnk
::Zh4grVQjdCuDJHaK8WYiIQ1RcDSQM2yoB7gd+9TL/eWAsEwQVfF/KsGJ5rCHM/Qv40v3YZch2jRfgM5s
::YB416Ek+ZW8=
::
::
::978f952a14a936cc963da21a135fa983
::[Bat To Exe Converter]
::
::YAwzoRdxOk+EWAjk
::fBw5plQjdCuDJHaK8WYiIQ1RcDSQM2yoB7gd+9TL/eWAsEwQVfF/KsGJ5qecI9wa5FbwdJohmHNZl6s=
::YAwzuBVtJxjWCl3EqQJgSA==
::ZR4luwNxJguZRRnk
::Yhs/ulQjdF+5
::cxAkpRVqdFKZSDk=
::cBs/ulQjdF+5
::ZR41oxFsdFKZSDk=
::eBoioBt6dFKZSDk=
::cRo6pxp7LAbNWATEpCI=
::egkzugNsPRvcWATEpCI=
::dAsiuh18IRvcCxnZtBJQ
::cRYluBh/LU+EWAnk
::YxY4rhs+aU+IeA==
::cxY6rQJ7JhzQF1fEqQJQ
::ZQ05rAF9IBncCkqN+0xwdVsGAlbi
::ZQ05rAF9IAHYFVzEqQJQ
::eg0/rx1wNQPfEVWB+kM9LVsJDC2ML3+7CrQY5/q7ytKmgGQpNA==
::fBEirQZwNQPfEVWB+kM9LVsJDGQ=
::cRolqwZ3JBvQF1fEqQJQ
::dhA7uBVwLU+EWHy81WAZHHs=
::YQ03rBFzNR3SWATElA==
::dhAmsQZ3MwfNWATEx0ciLxJbDDaNP2O7Zg==
::ZQ0/vhVqMQ3MEVWAtB9wSA==
::Zg8zqx1/OA3MEVWAtB9wSA==
::dhA7pRFwIByZRRnk
::Zh4grVQjdCuDJHaK8WYiIQ1RcDSQM2yoB7gd+9TL/eWAsEwQVfF/KsGJ5rCHM/Qv40v3YZch2jRfgM5s
::YB416Ek+ZW8=
::
::
::978f952a14a936cc963da21a135fa983
@echo off
chcp 65001 >nul
color 0B
mode con:cols=100 lines=30
echo.
echo ==========================================================
echo               SCRIPT DE INSTALAÇÃO - EXABIT
echo ==========================================================
echo.
echo Este script foi desenvolvido por Sérgio Rocha com o objetivo de
echo automatizar o processo de configuração inicial do Windows, incluindo:
echo.
echo - Instalação dos programas essenciais
echo - Criação automática do usuário Administrador e do usuário Padrão
echo - Aplicação do papel de parede padrão da EXABIT
echo - Limpeza dos atalhos públicos ao final da instalação
echo.
echo Ao término do processo, o computador será reiniciado automaticamente
echo para aplicar todas as configurações realizadas.
echo ==========================================================
echo.
:: ==========================================================
:: ALTERAR NOME DO COMPUTADOR
:: ==========================================================
setlocal ENABLEDELAYEDEXPANSION

set /P RENOMEAR=Deseja alterar o nome do computador? (S/N): 
if /I "%RENOMEAR%"=="S" (
    echo.
    set /P PCNAME=Informe o novo nome do computador: 
    echo.

    if "!PCNAME!"=="" (
        echo Nenhum nome informado. O nome do computador nao sera alterado.
        echo.
        timeout /t 3 >nul
    ) else (
        echo Renomeando computador para "!PCNAME!"...
        wmic computersystem where name="%COMPUTERNAME%" call rename name="!PCNAME!" >nul 2>&1

        set "RC=%ERRORLEVEL%"

        echo.
        if "!RC!"=="0" (
            echo O nome do computador sera alterado para "!PCNAME!" na proxima reinicializacao.
        ) else (
            echo Falha ao renomear o computador. Verifique as permissoes e se o nome e valido.
            echo Codigo de erro: !RC!
        )

        echo.
        echo Nome do computador definido para: !PCNAME!
        echo.
        timeout /t 3 >nul
    )
)
endlocal

:: reaplicar configuração do console para garantir fonte correta
chcp 65001 >nul
color 0B
mode con:cols=100 lines=30
cls

:: ===============================
:: PERGUNTA SE DESEJA CRIAR USUÁRIO ADMIN
:: ===============================
echo ==========================================================
echo                   USUÁRIO ADMINISTRADOR
echo ==========================================================
echo.
set /P CRIARADMIN=Deseja criar o usuário 'admin'? (S/N): 
if /I "%CRIARADMIN%"=="S" (
    echo.
    echo Criando usuário 'admin' com senha EXA@admin#202_...
    net user admin "EXA@admin#202_" /add >nul 2>&1
    if %errorlevel%==0 (
        echo Usuário 'admin' criado com sucesso.
    ) else (
        echo Usuário 'admin' já existe ou ocorreu um erro.
    )

    echo Adicionando 'admin' ao grupo de administradores...
    net localgroup "Administradores" admin /add >nul 2>&1
    if not %errorlevel%==0 (
        net localgroup "Administrators" admin /add >nul 2>&1
    )

    wmic useraccount where name="admin" set PasswordExpires=False >nul 2>&1
    if not %errorlevel%==0 (
        net user admin /passwordexpires:no >nul 2>&1
    )
    echo Senha do usuário 'admin' configurada para nunca expirar.
    echo.
    timeout /t 4 >nul
) else (
    echo.
    echo O usuário 'admin' não será criado. Prosseguindo com a instalação...
    echo.
    timeout /t 3 >nul
)
cls

:: ==========================================================
:: CRIA USUÁRIO NORTEL
:: ==========================================================
echo ==========================================================
echo                      USUÁRIO PADRÃO
echo ==========================================================
echo.
setlocal ENABLEDELAYEDEXPANSION

set /P CRIARNORTEL=Deseja criar o usuário padrão? (S/N): 
echo.

if /I "!CRIARNORTEL!"=="S" (

    :: Pergunta o nome do usuário
    set /P USERNAME=Informe o nome do usuário: 

    if "!USERNAME!"=="" (
        echo ERRO: Nome inválido. Operação cancelada.
        timeout /t 3 >nul
        goto :CONTINUAR
    )

    :: Pergunta a senha
    set /P PASSWORD=Informe a senha para !USERNAME!: 

    echo.
    echo Criando usuário "!USERNAME!"...

    net user "!USERNAME!" "!PASSWORD!" /add
    if !ERRORLEVEL! EQU 0 (
        echo Usuário "!USERNAME!" criado com sucesso!
    ) else (
        echo ERRO ao criar o usuário "!USERNAME!".
        echo Possíveis causas:
        echo - Senha não atende os requisitos
        echo - Usuário já existe
        echo - Permissão insuficiente
        echo.
        timeout /t 5 >nul
        goto :CONTINUAR
    )

    :: Senha nunca expira
    wmic useraccount where "name='!USERNAME!'" set PasswordExpires=False >nul 2>&1
    if ERRORLEVEL 1 (
        net user "!USERNAME!" /passwordexpires:no
    )

    echo Senha do usuário "!USERNAME!" configurada para nunca expirar.
    echo.
    timeout /t 5 >nul

) else (
    echo O usuário padrão não será criado.
    echo.
    timeout /t 3 >nul
)

:CONTINUAR
endlocal
cls


:: ===============================
:: PERGUNTA SE DESEJA ALTERAR PAPEL DE PAREDE
:: ===============================
echo ==========================================================
echo                      PAPEL DE PAREDE
echo ==========================================================
echo.
set /P ALTERWALL=Deseja alterar o papel de parede padrão da EXABIT? (S/N): 
if /I "%ALTERWALL%"=="S" (
    echo.
    echo Iniciando configuração do papel de parede da EXABIT...
    call "Scripts\wallpaper.cmd"
    echo Papel de parede configurado com sucesso.
    echo.
    timeout /t 6 >nul
) else (
    echo.
    echo O papel de parede não será alterado.
    echo.
    timeout /t 2 >nul
)
cls

:: ===============================
:: MENU DE INSTALAÇÃO
:: ===============================
echo ==========================================================
echo            INSTALAÇÃO DO PACOTE DE PROGRAMAS 
echo ==========================================================
echo.

set /P INSTALL=Gostaria de instalar os programas básicos? (S/N): 
echo.

if /I "%INSTALL%"=="S" (
    call "Scripts\program_base.cmd"
) else if /I "%INSTALL%"=="N" (
    echo Instalação ignorada pelo usuário.
) else (
    echo Opção inválida. Nenhum pacote foi instalado.
)
echo.

set /P INSTALL=Gostaria de instalar o Radmin VPN? (S/N): 
echo.

if /I "%INSTALL%"=="S" (
    call "Scripts\radmin.cmd"
) else if /I "%INSTALL%"=="N" (
    echo Instalação ignorada pelo usuário.
) else (
    echo Opção inválida. Nenhum pacote foi instalado.
)
echo.

set /P INSTALL=Gostaria de instalar o Microsoft Office? (S/N): 
echo.

if /I "%INSTALL%"=="S" (
    call "Scripts\office.cmd"
) else if /I "%INSTALL%"=="N" (
    echo Instalação ignorada pelo usuário.
) else (
    echo Opção inválida. Nenhum pacote foi instalado.
)
echo.

set /P INSTALL=Gostaria de instalar o Autodesk DWG TrueView? (S/N): 
echo.

if /I "%INSTALL%"=="S" (
    call "Scripts\dwgtrueview.cmd"
) else if /I "%INSTALL%"=="N" (
    echo Instalação ignorada pelo usuário.
) else (
    echo Opção inválida. Nenhum pacote foi instalado.
)
echo.

echo Limpando atalhos do Desktop público...
del /F /Q "%public%\Desktop\*.lnk" >nul 2>&1
echo -> Desktop limpo.
timeout /t 3 /nobreak >nul
cls

echo ==========================================
echo  Todas as instalações foram concluídas.
echo  O sistema será reiniciado em breve.
echo ==========================================
echo.
timeout /t 3 /nobreak >nul

shutdown /r /c "Estamos reiniciando o computador para concluir as instalações"
