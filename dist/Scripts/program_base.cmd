:: WinRAR
echo Instalando WinRAR...
start /wait "" "Programas\winrar-x64-713br.exe" /S
if %errorlevel% equ 0 (echo [ok] WinRAR instalado com sucesso.) else (echo X Erro ao instalar WinRAR.)
echo.

:: 7-Zip
echo Instalando 7-Zip...
start /wait msiexec /i "Programas\7z2501-x64.msi" /qn /norestart
if %errorlevel% equ 0 (echo [ok] 7-Zip instalado com sucesso.) else (echo X Erro ao instalar 7-Zip.)
echo.

:: Mozilla Firefox
echo Instalando Mozilla Firefox...
start /wait msiexec /i "Programas\Firefox_Setup_145.0.msi" /qn /norestart
if %errorlevel% equ 0 (echo [ok] Firefox instalado com sucesso.) else (echo X Erro ao instalar Firefox.)
echo.

:: Google Chrome
echo Instalando Google Chrome...
start /wait "" "Programas\ChromeStandaloneSetup64.exe" /silent /install
if %errorlevel% equ 0 (echo [ok] Google Chrome instalado com sucesso.) else (echo X Erro ao instalar Google Chrome.)
echo.

:: RustDesk
echo Instalando RustDesk...
start /wait "" "Programas\rustdesk-1.4.6-x86_64.exe" /S
if %errorlevel% equ 0 (echo [ok] RustDesk instalado com sucesso.) else (echo X Erro ao instalar RustDesk.)
echo.

:: Java Runtime Environment
echo Instalando Java Runtime Environment (JRE)...
start /wait "" "Programas\jre-8u471-windows-x64.exe" /s /L "C:\PATHSETUP.LOG"
if %errorlevel% equ 0 (echo [ok] JRE instalado com sucesso.) else (echo X Erro ao instalar JRE.)
echo.

:: Google Earth
echo Instalando Google Earth Pro...
start /wait "" "Programas\googleearthprowin-7.3.1-x64.exe" OMAHA=1
if %errorlevel% equ 0 (echo [ok] Google Earth instalado com sucesso.) else (echo X Erro ao instalar Google Earth.)
echo.

:: Notepad++
echo Instalando Notepad++...
start /wait "" "Programas\npp.7.7.1.Installer.x64.exe" /S
if %errorlevel% equ 0 (echo [ok] Notepad++ instalado com sucesso.) else (echo X Erro ao instalar Notepad++.)
echo.

:: Adobe Acrobat Reader
echo Instalando Adobe Acrobat Reader...
start /wait "" "Programas\AcroRdrDC2500120693_pt_BR.exe" /sAll /msi EULA_ACCEPT=YES /qn /norestart
if %errorlevel% equ 0 (echo [ok] Adobe Acrobat instalado com sucesso.) else (echo X Erro ao instalar Adobe Acrobat.)
echo.