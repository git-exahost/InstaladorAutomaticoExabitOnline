
echo Instalando Microsoft Office...
start /wait "" "Programas\Office\office\Setup64.exe" 
if %errorlevel% equ 0 (echo [ok] Microsoft Office instalado com sucesso.) else (echo X Erro ao instalar Office.)

