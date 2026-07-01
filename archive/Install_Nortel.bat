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
::ZQ05rAF9IAHYFVzEqQIZJghATQiDOGSoRpAkycrS24o=
::eg0/rx1wNQPfEVWB+kM9LVsJDGQ=
::fBEirQZwNQPfEVWB+kM9LVsJDGQ=
::cRolqwZ3JBvQF1fEqQJQ
::dhA7uBVwLU+EWHyc9UA5PHs=
::YQ03rBFzNR3SWATElA==
::dhAmsQZ3MwfNWATEx0ciLxJbDDaNP2O7Zg==
::ZQ0/vhVqMQ3MEVWAtB9wSA==
::Zg8zqx1/OA3MEVWAtB9wSA==
::dhA7pRFwIByZRRnk
::Zh4grVQjdCuDJHaK8WYiIQ1RcDSQM2yoB7gd+9TL/eWAsEwQVfF/KsGJ5rCHM/Qvw0v3YZch2kV0l9kYCRcWewquDg==
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
echo         SCRIPT DE INSTALAÇÃO - NORTEL ENGENHARIA
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
        timeout /t 5 >nul
    )
)
endlocal

:: reaplicar configuração do console para garantir fonte correta
chcp 65001 >nul
color 0B
mode con:cols=100 lines=30
cls

:: ==========================================================
:: PERGUNTA SE DESEJA CRIAR USUÁRIO ADMIN
:: ==========================================================
echo ==========================================================
echo                   USUÁRIO ADMINISTRADOR
echo ==========================================================
echo.
set /P CRIARADMIN=Deseja criar o usuário 'admin'? (S/N): 
if /I "%CRIARADMIN%"=="S" (
    echo.
    echo Criando usuário 'admin' com senha NT@admin#202_...
    net user admin "NT@admin#202_" /add >nul 2>&1
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
    if errorlevel 1 (
        net user admin /passwordexpires:no >nul 2>&1
    )

    echo Senha do usuário 'admin' configurada para nunca expirar.
    echo.
    timeout /t 4 >nul
) else (
    echo.
    echo O usuário 'admin' não será criado. Prosseguindo...
    echo.
    timeout /t 3 >nul
)
echo.

:: ==========================================================
:: CRIA USUÁRIO NORTEL
:: ==========================================================
echo ==========================================================
echo                      USUÁRIO NORTEL
echo ==========================================================
echo.
set /P CRIARNORTEL=Deseja criar o usuário 'Nortel'? (S/N): 
if /I "%CRIARNORTEL%"=="S" (
    echo.
    echo Criando usuário 'Nortel' com senha NT@colaborador...
    net user Nortel "NT@colaborador" /add >nul 2>&1
    if %errorlevel%==0 (
        echo Usuário 'Nortel' criado com sucesso!
    ) else (
        echo Usuário 'Nortel' já existe ou ocorreu um erro.
    )

    wmic useraccount where name="Nortel" set PasswordExpires=False >nul 2>&1
    if errorlevel 1 (
        net user Nortel /passwordexpires:no >nul 2>&1
    )

    echo Senha do usuário 'Nortel' configurada para nunca expirar.
    echo.
    timeout /t 4 >nul
) else (
    echo.
    echo O usuário 'Nortel' não será criado.
    echo.
    timeout /t 3 >nul
)
cls

:: ==========================================================
:: PERGUNTA SE DESEJA ALTERAR PAPEL DE PAREDE
:: ==========================================================
echo ==========================================================
echo                      PAPEL DE PAREDE
echo ==========================================================
echo.
set /P ALTERWALL=Deseja alterar o papel de parede padrão da NORTEL? (S/N): 
if /I "%ALTERWALL%"=="S" (
    echo.
    echo Iniciando configuração do papel de parede da NORTEL...
    if exist "Scripts\Wallpaper.cmd" (
        call "Scripts\Wallpaper.cmd"
        echo Papel de parede configurado com sucesso.
    ) else (
        echo ERRO: O arquivo Scripts\Wallpaper.cmd não foi encontrado.
    )
    echo.
    timeout /t 6 >nul
) else (
    echo.
    echo O papel de parede não será alterado.
    echo.
    timeout /t 2 >nul
)
cls

:: ==========================================================
:: MENU DE INSTALAÇÃO
:: ==========================================================
echo ==========================================================
echo               SELECIONE O PACOTE DE INSTALAÇÃO
echo ==========================================================
echo.

echo (1) - Pacote com todos os programas 
echo (2) - Pacote sem o Microsoft Office
echo.

set /P INSTALL=Informe o número da opção desejada: 
echo.

if "%INSTALL%"=="1" (
    echo Instalando todos os programas...
    call "Scripts\Setup_All_Programs.cmd"
) else if "%INSTALL%"=="2" (
    echo Instalando todos os programas exceto o Office...
    call "Scripts\Setup_All_Programs_No_Office.cmd"
) else (
    echo Opção inválida. Nenhum pacote foi instalado.
)
echo.

:: ==========================================================
:: LIMPEZA FINAL
:: ==========================================================
echo Limpando atalhos da área de trabalho pública...
del /F /Q "%PUBLIC%\Desktop\*.lnk" >nul 2>&1
echo Limpeza concluída.
echo.

shutdown /r /t 5 /c "Estamos reiniciando o computador para concluir as instalações"