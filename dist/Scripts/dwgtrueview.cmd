
:: Autodesk DWG TrueView
echo Instalando Autodesk DWG TrueView...
start /wait "" "Programas\DWGTrueView\Setup.exe" /q /w /t /c "setup.exe /q /w"
if %errorlevel% equ 0 (echo [ok] DWG TrueView instalado com sucesso.) else (echo X Erro ao instalar DWG TrueView.)


