# ==============================================================================
# Script: apply_wallpapers.ps1
# Empresa: EXABIT Solucoes em TI
# Finalidade: Configura todos os arquivos da pasta Wallpapers como papel de parede
#             em todos os temas do Windows (Aero, Dark, ThemeA-D, Spotlight),
#             no perfil padrao de novos usuarios e no usuario atual.
# ==============================================================================

[CmdletBinding()]
param(
    [string]$BasePath = "",
    [string]$Client = "",
    [switch]$PreventChangingWallpaper
)

$ErrorActionPreference = "Continue"

if (-not [string]::IsNullOrWhiteSpace($Client)) {
    $Client = $Client.Replace('"', '').Replace("'", "").Trim()
}
$clientTitle = if (-not [string]::IsNullOrWhiteSpace($Client)) { " ($Client)" } else { "" }

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Configuracao de Papeis de Parede Personalizados$clientTitle (EXABIT) " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Determina o diretorio base da aplicacao e sanitiza caracteres invalidos de escape de linha de comando
if (-not [string]::IsNullOrWhiteSpace($BasePath)) {
    # Remove aspas literais, apóstrofos e espaços que possam ter sido passados no escape de linha de comando
    $BasePath = $BasePath.Replace('"', '').Replace("'", "").Trim()
    if ($BasePath -match '^[a-zA-Z]:$') {
        $BasePath = $BasePath + "\"
    }
}

if ([string]::IsNullOrWhiteSpace($BasePath) -or -not (Test-Path -LiteralPath $BasePath -ErrorAction SilentlyContinue)) {
    $BasePath = Split-Path -Parent $PSScriptRoot
    if (-not (Test-Path -LiteralPath (Join-Path $BasePath "Scripts") -ErrorAction SilentlyContinue)) {
        $BasePath = $PSScriptRoot
    }
}

# 2. Localiza a pasta Wallpapers e as imagens disponiveis
$targetSub = if (-not [string]::IsNullOrWhiteSpace($Client)) { "Wallpapers\$Client" } else { "Wallpapers" }
$candidateDirs = @(
    (Join-Path $BasePath $targetSub),
    (Join-Path $BasePath "dist\$targetSub"),
    (Join-Path $BasePath "Scripts\$targetSub"),
    (Join-Path $PSScriptRoot $targetSub),
    (Join-Path $PSScriptRoot "..\$targetSub")
)

$wallpapersDir = $null
$imageExtensions = @(".jpg", ".jpeg", ".png", ".bmp")
$imageFiles = @()

foreach ($dir in $candidateDirs) {
    if ([string]::IsNullOrWhiteSpace($dir)) { continue }
    if (Test-Path -LiteralPath $dir -ErrorAction SilentlyContinue) {
        $found = @(Get-ChildItem -LiteralPath $dir -File -ErrorAction SilentlyContinue | Where-Object {
                $ext = $_.Extension.ToLower()
                $imageExtensions -contains $ext -and -not $_.Name.Contains("-Navi")
            })
        if ($found.Count -gt 0) {
            $wallpapersDir = $dir
            $imageFiles = $found
            break
        }
    }
}

if ($imageFiles.Count -eq 0) {
    Write-Warning "Nenhum arquivo de imagem (.jpg, .jpeg, .png, .bmp) encontrado na pasta Wallpapers."
    exit 1
}

Write-Host "Pasta de origem: $wallpapersDir" -ForegroundColor Gray
Write-Host "Total de papeis de parede encontrados: $($imageFiles.Count)" -ForegroundColor Green
foreach ($img in $imageFiles) {
    Write-Host "  - $($img.Name) ($([math]::Round($img.Length / 1MB, 2)) MB)" -ForegroundColor Gray
}
Write-Host ""

# Funcoes auxiliares para tomar posse e conceder permissoes a Administradores (SID *S-1-5-32-544)
function Grant-AdminAccess {
    param([string]$Path)
    try {
        if (Test-Path $Path) {
            takeown /f "$Path" /r /d y >$null 2>&1
            icacls "$Path" /grant "*S-1-5-32-544:(OI)(CI)F" /t /c /q >$null 2>&1
        }
    }
    catch {}
}

function Grant-AdminFileAccess {
    param([string]$FilePath)
    try {
        if (Test-Path $FilePath) {
            takeown /f "$FilePath" >$null 2>&1
            icacls "$FilePath" /grant "*S-1-5-32-544:F" /c /q >$null 2>&1
        }
    }
    catch {}
}

# 3. Diretorios do sistema operacional
$sysWeb = "$env:SystemRoot\Web"
$wallpaperSysDir = "$sysWeb\Wallpaper"
$folderName = if (-not [string]::IsNullOrWhiteSpace($Client)) { $Client } else { "Personalizado" }
$customSystemDir = "$wallpaperSysDir\$folderName"
$themesSysDir = "$env:SystemRoot\Resources\Themes"

# Cria pasta central C:\Windows\Web\Wallpaper\<folderName>
if (-not (Test-Path $customSystemDir)) {
    New-Item -Path $customSystemDir -ItemType Directory -Force | Out-Null
}
Grant-AdminAccess $wallpaperSysDir
Grant-AdminAccess $customSystemDir

# Copia todas as imagens para a pasta central
Write-Host "Copiando imagens para $customSystemDir..." -ForegroundColor Yellow
$installedImages = @()
foreach ($img in $imageFiles) {
    $dest = Join-Path $customSystemDir $img.Name
    Grant-AdminFileAccess $dest
    Copy-Item -Path $img.FullName -Destination $dest -Force
    $installedImages += $dest
    Write-Host "  [+] $dest" -ForegroundColor Gray
}

# Define imagem principal (prioriza 01-*, img0.jpg ou Porto da Barra se existir, senao a primeira)
$mainImg = $imageFiles | Where-Object { $_.Name -like "01-*" -or $_.Name -like "01_*" } | Select-Object -First 1
if (-not $mainImg) {
    $mainImg = $imageFiles | Where-Object { $_.Name -ieq "img0.jpg" } | Select-Object -First 1
}
if (-not $mainImg) {
    $mainImg = $imageFiles | Where-Object { $_.Name -like "*Porto da Barra*" } | Select-Object -First 1
}
if (-not $mainImg) {
    $mainImg = $imageFiles[0]
}
$mainImgPath = Join-Path $customSystemDir $mainImg.Name
Write-Host ""
Write-Host "Imagem principal definida: $($mainImg.Name)" -ForegroundColor Cyan
Write-Host ""

# 4. Copia os arquivos para todas as pastas de temas do Windows
$themeFolders = @(
    "$wallpaperSysDir\Windows",
    "$wallpaperSysDir\ThemeA",
    "$wallpaperSysDir\ThemeB",
    "$wallpaperSysDir\ThemeC",
    "$wallpaperSysDir\ThemeD",
    "$wallpaperSysDir\Spotlight"
)

Write-Host "Atualizando pastas de temas em C:\Windows\Web\Wallpaper..." -ForegroundColor Yellow
foreach ($tDir in $themeFolders) {
    if (-not (Test-Path $tDir)) {
        New-Item -Path $tDir -ItemType Directory -Force | Out-Null
    }
    Grant-AdminAccess $tDir
    foreach ($img in $imageFiles) {
        $dest = Join-Path $tDir $img.Name
        Grant-AdminFileAccess $dest
        Copy-Item -Path $img.FullName -Destination $dest -Force
    }
}

# Substitui as imagens padrao de cada tema para que fiquem personalizadas imediatamente
# A) Windows (Aero / Light e Dark)
Grant-AdminFileAccess "$wallpaperSysDir\Windows\img0.jpg"
Copy-Item -Path $mainImg.FullName -Destination "$wallpaperSysDir\Windows\img0.jpg" -Force

Grant-AdminFileAccess "$wallpaperSysDir\Windows\img19.jpg"
Copy-Item -Path $mainImg.FullName -Destination "$wallpaperSysDir\Windows\img19.jpg" -Force

# B) Spotlight
Grant-AdminFileAccess "$wallpaperSysDir\Spotlight\img50.jpg"
Copy-Item -Path $mainImg.FullName -Destination "$wallpaperSysDir\Spotlight\img50.jpg" -Force
if (Test-Path "$wallpaperSysDir\Spotlight\img14.jpg") {
    Grant-AdminFileAccess "$wallpaperSysDir\Spotlight\img14.jpg"
    Copy-Item -Path $mainImg.FullName -Destination "$wallpaperSysDir\Spotlight\img14.jpg" -Force
}

# C) Tela de Bloqueio (Lock Screen)
$screenDir = "$sysWeb\Screen"
if (Test-Path $screenDir) {
    Grant-AdminAccess $screenDir
    Grant-AdminFileAccess "$screenDir\img100.jpg"
    Copy-Item -Path $mainImg.FullName -Destination "$screenDir\img100.jpg" -Force
}

# D) Cache 4K (Windows 10/11)
$fourKDir = "$sysWeb\4K\Wallpaper\Windows"
if (Test-Path $fourKDir) {
    Grant-AdminAccess $fourKDir
    Get-ChildItem -Path $fourKDir -Filter "*.jpg" | ForEach-Object {
        Grant-AdminFileAccess $_.FullName
        Copy-Item -Path $mainImg.FullName -Destination $_.FullName -Force
    }
}

# E) Temas ThemeA, ThemeB, ThemeC, ThemeD (distribuicao ciclica das imagens)
$themeGroups = @(
    @{ Dir = "$wallpaperSysDir\ThemeA"; Files = @("img20.jpg", "img21.jpg", "img22.jpg", "img23.jpg") },
    @{ Dir = "$wallpaperSysDir\ThemeB"; Files = @("img24.jpg", "img25.jpg", "img26.jpg", "img27.jpg") },
    @{ Dir = "$wallpaperSysDir\ThemeC"; Files = @("img28.jpg", "img29.jpg", "img30.jpg", "img31.jpg") },
    @{ Dir = "$wallpaperSysDir\ThemeD"; Files = @("img32.jpg", "img33.jpg", "img34.jpg", "img35.jpg") }
)

$cycleIdx = 0
foreach ($grp in $themeGroups) {
    Grant-AdminAccess $grp.Dir
    foreach ($fn in $grp.Files) {
        $sourceImg = $imageFiles[$cycleIdx % $imageFiles.Count]
        $destFile = Join-Path $grp.Dir $fn
        Grant-AdminFileAccess $destFile
        Copy-Item -Path $sourceImg.FullName -Destination $destFile -Force
        $cycleIdx++
    }
}
Write-Host "[ok] Pastas e imagens dos temas atualizadas." -ForegroundColor Green
Write-Host ""

# 5. Atualiza os arquivos .theme em C:\Windows\Resources\Themes\
Write-Host "Configurando arquivos de tema em C:\Windows\Resources\Themes..." -ForegroundColor Yellow
$themeFiles = @("aero.theme", "dark.theme", "spotlight.theme", "themeA.theme", "themeB.theme", "themeC.theme", "themeD.theme")

foreach ($tName in $themeFiles) {
    $tPath = Join-Path $themesSysDir $tName
    if (Test-Path $tPath) {
        try {
            Grant-AdminFileAccess $tPath
            $rawContent = [System.IO.File]::ReadAllText($tPath, [System.Text.Encoding]::Default)

            # Remove linhas orfas corrompidas de versoes anteriores ($10 / $110)
            $rawContent = [regex]::Replace($rawContent, '(?m)^\s*\$1(?:0|10)\s*\r?\n?', '')

            # Atualiza ou insere chaves na sessao [Control Panel\Desktop]
            if ($rawContent -match '(?is)(\[Control Panel\\Desktop\]\s*\r?\n)(.*?)(?=\r?\n\[|\Z)') {
                $desktopMatch = [regex]::Match($rawContent, '(?is)(\[Control Panel\\Desktop\]\s*\r?\n)(.*?)(?=\r?\n\[|\Z)')
                $sectionHeader = $desktopMatch.Groups[1].Value
                $sectionBody = $desktopMatch.Groups[2].Value

                if ($sectionBody -match '(?im)^Wallpaper\s*=') {
                    $sectionBody = [regex]::Replace($sectionBody, '(?im)^(Wallpaper\s*=)[^\r\n]*', "`${1}%SystemRoot%\web\wallpaper\Windows\img0.jpg")
                }
                else {
                    $sectionBody = "Wallpaper=%SystemRoot%\web\wallpaper\Windows\img0.jpg`r`n" + $sectionBody
                }

                if ($sectionBody -match '(?im)^TileWallpaper\s*=') {
                    $sectionBody = [regex]::Replace($sectionBody, '(?im)^(TileWallpaper\s*=)[^\r\n]*', "`${1}0")
                }
                else {
                    $sectionBody = "TileWallpaper=0`r`n" + $sectionBody
                }

                if ($sectionBody -match '(?im)^WallpaperStyle\s*=') {
                    $sectionBody = [regex]::Replace($sectionBody, '(?im)^(WallpaperStyle\s*=)[^\r\n]*', "`${1}10")
                }
                else {
                    $sectionBody = "WallpaperStyle=10`r`n" + $sectionBody
                }

                $rawContent = $rawContent.Substring(0, $desktopMatch.Index) + $sectionHeader + $sectionBody + $rawContent.Substring($desktopMatch.Index + $desktopMatch.Length)
            }

            # Configura sessao [Slideshow] com todas as imagens da pasta Wallpapers
            $sb = New-Object System.Text.StringBuilder
            [void]$sb.AppendLine("[Slideshow]")
            [void]$sb.AppendLine("Interval=1800000")
            [void]$sb.AppendLine("Shuffle=0")
            [void]$sb.AppendLine("ImagesRootPath=%SystemRoot%\Web\Wallpaper\$folderName")
            for ($i = 0; $i -lt $imageFiles.Count; $i++) {
                [void]$sb.AppendLine("Item$($i)Path=%SystemRoot%\Web\Wallpaper\$folderName\$($imageFiles[$i].Name)")
            }

            if ($rawContent -match '(?is)\[Slideshow\].*?(?=(\r?\n\[|\Z))') {
                $rawContent = [regex]::Replace($rawContent, '(?is)\[Slideshow\].*?(?=(\r?\n\[|\Z))', $sb.ToString().TrimEnd())
            }
            else {
                $rawContent = $rawContent.TrimEnd() + "`r`n`r`n" + $sb.ToString().TrimEnd() + "`r`n"
            }

            [System.IO.File]::WriteAllText($tPath, $rawContent, [System.Text.Encoding]::Default)
            Write-Host "  [ok] $tName atualizado (Papel de parede + Slideshow de todas as imagens)" -ForegroundColor Green
        }
        catch {
            Write-Warning "Falha ao gravar $($tName): $($_.Exception.Message)"
        }
    }
}
Write-Host ""

# 6. Atualiza Registro e Cache do Usuario Atual (desbloqueia temporariamente antes da troca ao vivo)
Write-Host "Configurando usuario atual..." -ForegroundColor Yellow
$mainWallpaperTarget = "$wallpaperSysDir\Windows\img0.jpg"
$regDesktop = "HKCU:\Control Panel\Desktop"
$regCuActiveDesktop = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop"
$regCuSysPol = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\System"

if (Test-Path $regCuActiveDesktop) {
    if (Get-ItemProperty -Path $regCuActiveDesktop -Name "NoChangingWallPaper" -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $regCuActiveDesktop -Name "NoChangingWallPaper" -ErrorAction SilentlyContinue
    }
}
if (Test-Path $regCuSysPol) {
    if (Get-ItemProperty -Path $regCuSysPol -Name "Wallpaper" -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $regCuSysPol -Name "Wallpaper" -ErrorAction SilentlyContinue
    }
    if (Get-ItemProperty -Path $regCuSysPol -Name "WallpaperStyle" -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $regCuSysPol -Name "WallpaperStyle" -ErrorAction SilentlyContinue
    }
}

Set-ItemProperty -Path $regDesktop -Name "Wallpaper" -Value $mainWallpaperTarget
Set-ItemProperty -Path $regDesktop -Name "WallpaperStyle" -Value "10"
Set-ItemProperty -Path $regDesktop -Name "TileWallpaper" -Value "0"
if (Get-ItemProperty -Path $regDesktop -Name "TranscodedImageCache" -ErrorAction SilentlyContinue) {
    Remove-ItemProperty -Path $regDesktop -Name "TranscodedImageCache" -ErrorAction SilentlyContinue
}

$regSlideshow = "HKCU:\Control Panel\Personalization\Desktop Slideshow"
if (-not (Test-Path $regSlideshow)) {
    New-Item -Path $regSlideshow -Force | Out-Null
}
Set-ItemProperty -Path $regSlideshow -Name "Interval" -Value 1800000 -Type DWord
Set-ItemProperty -Path $regSlideshow -Name "Shuffle" -Value 0 -Type DWord

$userThemeDir = "$env:APPDATA\Microsoft\Windows\Themes"
if (Test-Path $userThemeDir) {
    try {
        $transcoded = Join-Path $userThemeDir "TranscodedWallpaper"
        if (Test-Path $transcoded) {
            Remove-Item $transcoded -Force -ErrorAction SilentlyContinue
        }
    }
    catch {}

    $cachedFiles = Join-Path $userThemeDir "CachedFiles"
    if (Test-Path $cachedFiles) {
        Remove-Item "$cachedFiles\*" -Force -Recurse -ErrorAction SilentlyContinue
    }
}
Write-Host "[ok] Registro e cache do usuario atual configurados." -ForegroundColor Green
Write-Host ""

# 7. Dispara atualizacao imediata da tela via SystemParametersInfo + IDesktopWallpaper (Fill) ANTES das politicas de bloqueio
Write-Host "Aplicando papel de parede em modo Preencher (Fill) em todas as telas..." -ForegroundColor Yellow
try {
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public enum DesktopWallpaperPosition { Center = 0, Tile = 1, Stretch = 2, Fit = 3, Fill = 4, Span = 5 }

[StructLayout(LayoutKind.Sequential)]
public struct RECT {
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDesktopWallpaper {
    void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
    [return: MarshalAs(UnmanagedType.LPWStr)] string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID);
    [return: MarshalAs(UnmanagedType.LPWStr)] string GetMonitorDevicePathAt(uint monitorIndex);
    uint GetMonitorDevicePathCount();
    void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID, out RECT displayRect);
    void SetBackgroundColor(uint color);
    uint GetBackgroundColor();
    void SetPosition(DesktopWallpaperPosition position);
    DesktopWallpaperPosition GetPosition();
}

[ComImport, Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
public class DesktopWallpaperClass {}

public class NativeWallpaper {
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

    public static string ApplyFill(string path) {
        SystemParametersInfo(20, 0, path, 3);
        System.Threading.Thread.Sleep(150);
        try {
            var dw = (IDesktopWallpaper)new DesktopWallpaperClass();
            dw.SetPosition(DesktopWallpaperPosition.Fill);
            dw.SetWallpaper(null, path);
            uint count = dw.GetMonitorDevicePathCount();
            for (uint i = 0; i < count; i++) {
                try {
                    string mon = dw.GetMonitorDevicePathAt(i);
                    if (!string.IsNullOrEmpty(mon)) {
                        dw.SetWallpaper(mon, path);
                    }
                }
                catch {}
            }
            dw.SetPosition(DesktopWallpaperPosition.Fill);
            return "SPI+IDesktopWallpaper:" + dw.GetPosition().ToString();
        }
        catch {
            return "SPI-Only";
        }
    }
}
"@
    $appliedMode = [NativeWallpaper]::ApplyFill($mainWallpaperTarget)
    Set-ItemProperty -Path $regDesktop -Name "WallpaperStyle" -Value "10" -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $regDesktop -Name "TileWallpaper" -Value "0" -ErrorAction SilentlyContinue
    Write-Host "[ok] Tela atualizada em modo Preencher ($appliedMode)." -ForegroundColor Green
}
catch {
    rundll32.exe user32.dll, UpdatePerUserSystemParameters
}
Write-Host ""

# 8. Configura o perfil Default (novos usuarios que forem criados)
Write-Host "Configurando perfil padrao do Windows para novos usuarios..." -ForegroundColor Yellow
$defaultNtUser = "$env:SystemDrive\Users\Default\NTUSER.DAT"
if (Test-Path $defaultNtUser) {
    reg load "HKU\DefaultUser" "$defaultNtUser" >$null 2>&1
    if ($LASTEXITCODE -eq 0) {
        reg add "HKU\DefaultUser\Control Panel\Desktop" /v Wallpaper /t REG_SZ /d "$mainWallpaperTarget" /f >$null 2>&1
        reg add "HKU\DefaultUser\Control Panel\Desktop" /v WallpaperStyle /t REG_SZ /d 10 /f >$null 2>&1
        reg add "HKU\DefaultUser\Control Panel\Desktop" /v TileWallpaper /t REG_SZ /d 0 /f >$null 2>&1
        reg add "HKU\DefaultUser\Control Panel\Personalization\Desktop Slideshow" /v Interval /t REG_DWORD /d 1800000 /f >$null 2>&1
        reg add "HKU\DefaultUser\Control Panel\Personalization\Desktop Slideshow" /v Shuffle /t REG_DWORD /d 0 /f >$null 2>&1

        reg query "HKU\DefaultUser\Software\Microsoft\Windows\CurrentVersion\Policies\System" /v WallpaperStyle >$null 2>&1
        if ($LASTEXITCODE -eq 0) {
            reg add "HKU\DefaultUser\Software\Microsoft\Windows\CurrentVersion\Policies\System" /v WallpaperStyle /t REG_SZ /d 4 /f >$null 2>&1
        }
        reg query "HKU\DefaultUser\Software\Microsoft\Windows\CurrentVersion\Policies\System" /v Wallpaper >$null 2>&1
        if ($LASTEXITCODE -eq 0) {
            reg add "HKU\DefaultUser\Software\Microsoft\Windows\CurrentVersion\Policies\System" /v Wallpaper /t REG_SZ /d "$mainWallpaperTarget" /f >$null 2>&1
        }

        if ($PreventChangingWallpaper) {
            reg add "HKU\DefaultUser\Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop" /v NoChangingWallPaper /t REG_DWORD /d 1 /f >$null 2>&1
            reg add "HKU\DefaultUser\Software\Microsoft\Windows\CurrentVersion\Policies\System" /v Wallpaper /t REG_SZ /d "$mainWallpaperTarget" /f >$null 2>&1
            reg add "HKU\DefaultUser\Software\Microsoft\Windows\CurrentVersion\Policies\System" /v WallpaperStyle /t REG_SZ /d 4 /f >$null 2>&1
            Write-Host "[ok] Politica corporativa ativada no Default: colaboradores impedidos de alterar o papel de parede." -ForegroundColor Green
        }

        reg unload "HKU\DefaultUser" >$null 2>&1
        Write-Host "[ok] Perfil Default configurado para novos usuarios." -ForegroundColor Green
    }
}
Write-Host ""

# 9A. Atualiza hives ja carregados em HKEY_USERS (usuarios atualmente logados)
Get-ChildItem -Path "Registry::HKEY_USERS" -ErrorAction SilentlyContinue | ForEach-Object {
    $sidName = $_.PSChildName
    if ($sidName -match '^S-1-5-21-\d+-\d+-\d+-\d+$') {
        $hkuBase = "Registry::HKEY_USERS\$sidName"
        $hkuDesktop = "$hkuBase\Control Panel\Desktop"
        try {
            if (-not (Test-Path $hkuDesktop)) {
                New-Item -Path $hkuDesktop -Force | Out-Null
            }
            Set-ItemProperty -Path $hkuDesktop -Name "Wallpaper" -Value $mainWallpaperTarget -ErrorAction SilentlyContinue
            Set-ItemProperty -Path $hkuDesktop -Name "WallpaperStyle" -Value "10" -ErrorAction SilentlyContinue
            Set-ItemProperty -Path $hkuDesktop -Name "TileWallpaper" -Value "0" -ErrorAction SilentlyContinue
            if (Get-ItemProperty -Path $hkuDesktop -Name "TranscodedImageCache" -ErrorAction SilentlyContinue) {
                Remove-ItemProperty -Path $hkuDesktop -Name "TranscodedImageCache" -ErrorAction SilentlyContinue
            }

            $hkuPolSys = "$hkuBase\Software\Microsoft\Windows\CurrentVersion\Policies\System"
            if (Test-Path $hkuPolSys) {
                if (Get-ItemProperty -Path $hkuPolSys -Name "WallpaperStyle" -ErrorAction SilentlyContinue) {
                    Set-ItemProperty -Path $hkuPolSys -Name "WallpaperStyle" -Value "4" -ErrorAction SilentlyContinue
                }
                if (Get-ItemProperty -Path $hkuPolSys -Name "Wallpaper" -ErrorAction SilentlyContinue) {
                    Set-ItemProperty -Path $hkuPolSys -Name "Wallpaper" -Value $mainWallpaperTarget -ErrorAction SilentlyContinue
                }
            }

            if ($PreventChangingWallpaper) {
                $accountShortName = ""
                try {
                    $ntAccount = (New-Object System.Security.Principal.SecurityIdentifier($sidName)).Translate([System.Security.Principal.NTAccount]).Value
                    $accountShortName = $ntAccount.Split('\')[-1]
                }
                catch {}

                if ($accountShortName -notmatch '^(admin|Administrator|Administrador)$') {
                    $hkuPolAd = "$hkuBase\Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop"
                    if (-not (Test-Path $hkuPolAd)) { New-Item -Path $hkuPolAd -Force | Out-Null }
                    if (-not (Test-Path $hkuPolSys)) { New-Item -Path $hkuPolSys -Force | Out-Null }
                    Set-ItemProperty -Path $hkuPolAd -Name "NoChangingWallPaper" -Value 1 -Type DWord -ErrorAction SilentlyContinue
                    Set-ItemProperty -Path $hkuPolSys -Name "Wallpaper" -Value $mainWallpaperTarget -ErrorAction SilentlyContinue
                    Set-ItemProperty -Path $hkuPolSys -Name "WallpaperStyle" -Value "4" -ErrorAction SilentlyContinue
                    $displayUser = if (-not [string]::IsNullOrWhiteSpace($accountShortName)) { $accountShortName } else { $sidName }
                    Write-Host ('[ok] Bloqueio de papel de parede aplicado no hive carregado: ' + $displayUser) -ForegroundColor Green
                }
            }
        }
        catch {}
    }
}

# 9B. Atualiza usuarios existentes offline em C:\Users
Get-ChildItem -Path "$env:SystemDrive\Users" -Directory | ForEach-Object {
    $uName = $_.Name
    $uThemes = Join-Path $_.FullName "AppData\Roaming\Microsoft\Windows\Themes"
    if (Test-Path $uThemes) {
        try {
            $uTranscoded = Join-Path $uThemes "TranscodedWallpaper"
            if (Test-Path $uTranscoded) {
                Remove-Item $uTranscoded -Force -ErrorAction SilentlyContinue
            }
            $uCached = Join-Path $uThemes "CachedFiles"
            if (Test-Path $uCached) {
                Remove-Item "$uCached\*" -Force -Recurse -ErrorAction SilentlyContinue
            }
        }
        catch {}
    }

    $uNt = Join-Path $_.FullName "NTUSER.DAT"
    if (($uName -notmatch '^(Default|Public|All Users)$') -and (Test-Path $uNt)) {
        reg load "HKU\TempUser" "$uNt" >$null 2>&1
        if ($LASTEXITCODE -eq 0) {
            reg add "HKU\TempUser\Control Panel\Desktop" /v Wallpaper /t REG_SZ /d "$mainWallpaperTarget" /f >$null 2>&1
            reg add "HKU\TempUser\Control Panel\Desktop" /v WallpaperStyle /t REG_SZ /d 10 /f >$null 2>&1
            reg add "HKU\TempUser\Control Panel\Desktop" /v TileWallpaper /t REG_SZ /d 0 /f >$null 2>&1
            reg delete "HKU\TempUser\Control Panel\Desktop" /v TranscodedImageCache /f >$null 2>&1

            reg query "HKU\TempUser\Software\Microsoft\Windows\CurrentVersion\Policies\System" /v WallpaperStyle >$null 2>&1
            if ($LASTEXITCODE -eq 0) {
                reg add "HKU\TempUser\Software\Microsoft\Windows\CurrentVersion\Policies\System" /v WallpaperStyle /t REG_SZ /d 4 /f >$null 2>&1
            }
            reg query "HKU\TempUser\Software\Microsoft\Windows\CurrentVersion\Policies\System" /v Wallpaper >$null 2>&1
            if ($LASTEXITCODE -eq 0) {
                reg add "HKU\TempUser\Software\Microsoft\Windows\CurrentVersion\Policies\System" /v Wallpaper /t REG_SZ /d "$mainWallpaperTarget" /f >$null 2>&1
            }

            if ($PreventChangingWallpaper -and ($uName -notmatch '^(admin|Administrator|Administrador)$')) {
                reg add "HKU\TempUser\Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop" /v NoChangingWallPaper /t REG_DWORD /d 1 /f >$null 2>&1
                reg add "HKU\TempUser\Software\Microsoft\Windows\CurrentVersion\Policies\System" /v Wallpaper /t REG_SZ /d "$mainWallpaperTarget" /f >$null 2>&1
                reg add "HKU\TempUser\Software\Microsoft\Windows\CurrentVersion\Policies\System" /v WallpaperStyle /t REG_SZ /d 4 /f >$null 2>&1
                Write-Host ('[ok] Bloqueio de papel de parede aplicado para usuario existente: ' + $uName) -ForegroundColor Green
            }

            reg unload "HKU\TempUser" >$null 2>&1
        }
    }
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "  Papeis de parede aplicados com sucesso em todos os temas! " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
exit 0
