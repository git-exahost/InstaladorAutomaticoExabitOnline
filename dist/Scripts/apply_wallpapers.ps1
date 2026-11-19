# ==============================================================================
# Script: apply_wallpapers.ps1
# Empresa: EXABIT Solucoes em TI
# Finalidade: Configura todos os arquivos da pasta Wallpapers como papel de parede
#             em todos os temas do Windows (Aero, Dark, ThemeA-D, Spotlight),
#             no perfil padrao de novos usuarios e no usuario atual.
# ==============================================================================

[CmdletBinding()]
param(
    [string]$BasePath = ""
)

$ErrorActionPreference = "Continue"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Configuracao de Papeis de Parede Personalizados (EXABIT) " -ForegroundColor Cyan
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
$candidateDirs = @(
    (Join-Path $BasePath "Wallpapers"),
    (Join-Path $BasePath "dist\Wallpapers"),
    (Join-Path $BasePath "Scripts\Wallpapers"),
    (Join-Path $PSScriptRoot "Wallpapers"),
    (Join-Path $PSScriptRoot "..\Wallpapers")
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
    } catch {}
}

function Grant-AdminFileAccess {
    param([string]$FilePath)
    try {
        if (Test-Path $FilePath) {
            takeown /f "$FilePath" >$null 2>&1
            icacls "$FilePath" /grant "*S-1-5-32-544:F" /c /q >$null 2>&1
        }
    } catch {}
}

# 3. Diretorios do sistema operacional
$sysWeb = "$env:SystemRoot\Web"
$wallpaperSysDir = "$sysWeb\Wallpaper"
$customSystemDir = "$wallpaperSysDir\Personalizado"
$themesSysDir = "$env:SystemRoot\Resources\Themes"

# Cria pasta central C:\Windows\Web\Wallpaper\Personalizado
if (-not (Test-Path $customSystemDir)) {
    New-Item -Path $customSystemDir -ItemType Directory -Force | Out-Null
}
Grant-AdminAccess $wallpaperSysDir
Grant-AdminAccess $customSystemDir

# Copia todas as imagens para a pasta central
Write-Host "Copiando imagens para C:\Windows\Web\Wallpaper\Personalizado..." -ForegroundColor Yellow
$installedImages = @()
foreach ($img in $imageFiles) {
    $dest = Join-Path $customSystemDir $img.Name
    Grant-AdminFileAccess $dest
    Copy-Item -Path $img.FullName -Destination $dest -Force
    $installedImages += $dest
    Write-Host "  [+] $dest" -ForegroundColor Gray
}

# Define imagem principal (prioriza Porto da Barra / img0.jpg se existir, senao a primeira)
$mainImg = $imageFiles | Where-Object { $_.Name -ieq "img0.jpg" } | Select-Object -First 1
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

            # Atualiza sessao [Control Panel\Desktop]
            if ($rawContent -match '(?i)\[Control Panel\\Desktop\]') {
                $rawContent = [regex]::Replace($rawContent, '(?i)(Wallpaper\s*=)[^\r\n]*', "`$1%SystemRoot%\web\wallpaper\Windows\img0.jpg")
                $rawContent = [regex]::Replace($rawContent, '(?i)(TileWallpaper\s*=)[^\r\n]*', "`$10")
                $rawContent = [regex]::Replace($rawContent, '(?i)(WallpaperStyle\s*=)[^\r\n]*', "`$110")
            }

            # Configura sessao [Slideshow] com todas as imagens da pasta Wallpapers
            $sb = New-Object System.Text.StringBuilder
            [void]$sb.AppendLine("[Slideshow]")
            [void]$sb.AppendLine("Interval=1800000")
            [void]$sb.AppendLine("Shuffle=0")
            [void]$sb.AppendLine("ImagesRootPath=%SystemRoot%\Web\Wallpaper\Personalizado")
            for ($i = 0; $i -lt $imageFiles.Count; $i++) {
                [void]$sb.AppendLine("Item$($i)Path=%SystemRoot%\Web\Wallpaper\Personalizado\$($imageFiles[$i].Name)")
            }

            if ($rawContent -match '(?is)\[Slideshow\].*?(?=(\r?\n\[|\Z))') {
                $rawContent = [regex]::Replace($rawContent, '(?is)\[Slideshow\].*?(?=(\r?\n\[|\Z))', $sb.ToString().TrimEnd())
            } else {
                $rawContent = $rawContent.TrimEnd() + "`r`n`r`n" + $sb.ToString().TrimEnd() + "`r`n"
            }

            [System.IO.File]::WriteAllText($tPath, $rawContent, [System.Text.Encoding]::Default)
            Write-Host "  [ok] $tName atualizado (Papel de parede + Slideshow de todas as imagens)" -ForegroundColor Green
        } catch {
            Write-Warning "Falha ao gravar $($tName): $($_.Exception.Message)"
        }
    }
}
Write-Host ""

# 6. Atualiza Registro e Cache do Usuario Atual
Write-Host "Configurando usuario atual..." -ForegroundColor Yellow
$regDesktop = "HKCU:\Control Panel\Desktop"
Set-ItemProperty -Path $regDesktop -Name "Wallpaper" -Value "$wallpaperSysDir\Windows\img0.jpg"
Set-ItemProperty -Path $regDesktop -Name "WallpaperStyle" -Value "10"
Set-ItemProperty -Path $regDesktop -Name "TileWallpaper" -Value "0"

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
        Copy-Item -Path $mainImg.FullName -Destination $transcoded -Force
    } catch {}

    $cachedFiles = Join-Path $userThemeDir "CachedFiles"
    if (Test-Path $cachedFiles) {
        Remove-Item "$cachedFiles\*" -Force -Recurse -ErrorAction SilentlyContinue
    }
}
Write-Host "[ok] Registro e cache do usuario atual configurados." -ForegroundColor Green
Write-Host ""

# 7. Configura o perfil Default (novos usuarios que forem criados)
Write-Host "Configurando perfil padrao do Windows para novos usuarios..." -ForegroundColor Yellow
$defaultNtUser = "$env:SystemDrive\Users\Default\NTUSER.DAT"
if (Test-Path $defaultNtUser) {
    reg load "HKU\DefaultUser" "$defaultNtUser" >$null 2>&1
    if ($LASTEXITCODE -eq 0) {
        reg add "HKU\DefaultUser\Control Panel\Desktop" /v Wallpaper /t REG_SZ /d "%SystemRoot%\web\wallpaper\Windows\img0.jpg" /f >$null 2>&1
        reg add "HKU\DefaultUser\Control Panel\Desktop" /v WallpaperStyle /t REG_SZ /d 10 /f >$null 2>&1
        reg add "HKU\DefaultUser\Control Panel\Desktop" /v TileWallpaper /t REG_SZ /d 0 /f >$null 2>&1
        reg add "HKU\DefaultUser\Control Panel\Personalization\Desktop Slideshow" /v Interval /t REG_DWORD /d 1800000 /f >$null 2>&1
        reg add "HKU\DefaultUser\Control Panel\Personalization\Desktop Slideshow" /v Shuffle /t REG_DWORD /d 0 /f >$null 2>&1
        reg unload "HKU\DefaultUser" >$null 2>&1
        Write-Host "[ok] Perfil Default configurado para novos usuarios." -ForegroundColor Green
    }
}
Write-Host ""

# 8. Atualiza usuarios existentes em C:\Users
Get-ChildItem -Path "$env:SystemDrive\Users" -Directory | ForEach-Object {
    $uThemes = Join-Path $_.FullName "AppData\Roaming\Microsoft\Windows\Themes"
    if (Test-Path $uThemes) {
        try {
            Copy-Item -Path $mainImg.FullName -Destination (Join-Path $uThemes "TranscodedWallpaper") -Force -ErrorAction SilentlyContinue
            $uCached = Join-Path $uThemes "CachedFiles"
            if (Test-Path $uCached) {
                Remove-Item "$uCached\*" -Force -Recurse -ErrorAction SilentlyContinue
            }
        } catch {}
    }
}

# 9. Dispara atualizacao imediata da tela via SystemParametersInfoW
Write-Host "Aplicando papel de parede na tela imediatamente..." -ForegroundColor Yellow
try {
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class NativeWallpaper {
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
}
"@
    [NativeWallpaper]::SystemParametersInfo(20, 0, "$wallpaperSysDir\Windows\img0.jpg", 3) | Out-Null
    Write-Host "[ok] Tela atualizada com sucesso." -ForegroundColor Green
} catch {
    rundll32.exe user32.dll,UpdatePerUserSystemParameters
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "  Papeis de parede aplicados com sucesso em todos os temas! " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
exit
# SIG # Begin signature block
# MIIcjwYJKoZIhvcNAQcCoIIcgDCCHHwCAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCASV8/jyIYaCBzh
# 89Bn8WfB4AbAvuSc66r+UC1GUfEZ8aCCFqgwggNqMIICUqADAgECAhAa3VkuPbH1
# t0A2pZ7si0QoMA0GCSqGSIb3DQEBCwUAME0xCzAJBgNVBAYTAkJSMR4wHAYDVQQK
# DBVFWEFCSVQgU29sdWNvZXMgZW0gVEkxHjAcBgNVBAMMFUVYQUJJVCBTb2x1Y29l
# cyBlbSBUSTAeFw0yNjExMTYyMjMyMDVaFw0zMTExMTYyMjQyMDVaME0xCzAJBgNV
# BAYTAkJSMR4wHAYDVQQKDBVFWEFCSVQgU29sdWNvZXMgZW0gVEkxHjAcBgNVBAMM
# FUVYQUJJVCBTb2x1Y29lcyBlbSBUSTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCC
# AQoCggEBAL7ZJHFO0KebjR4R0C1ygnR/lRsutE3a3s0fMznd3Ql4tEcT4BnAx8rK
# 03170AHGcke+4FUKk0rAsjnFH/19EnfCqlNWjT1vaGzhnSDOgnS4q8iv3pnGN+hZ
# zWnMvk+e+1YbX9mS4McVk5SXjFm8nCjiBBs3AsqE0+u4H1KnMApQgaomFq4yCuBM
# K+kNj7QVzy9UKHR+umvZfIe7v0nNYIfumUM19w7FqAU/WHYD53BFO61axAMnG4tZ
# ogDmwVFQLVZlmPGZh9OF3ajxjv6sY8l2YWe2OYRLOWDqu1UNY2LxRw0ZK7EzdZ3t
# kuedQkY/urAfk3idTEK+cOGOkDmc2JkCAwEAAaNGMEQwDgYDVR0PAQH/BAQDAgeA
# MBMGA1UdJQQMMAoGCCsGAQUFBwMDMB0GA1UdDgQWBBTcPE2vEUE1xQAioLVN7CuJ
# MGfHMDANBgkqhkiG9w0BAQsFAAOCAQEAmHYRVnIhZUWsm8SphKQU5pl4wuVq2sVF
# NJnDpJON6GdDEbunl5ognI9eCKkDx7rW6iqX+DPPXtnt/HiUbAKtV0+B1pauE3NX
# YlmdooSC8qHF0Dx3PWv/KeCypY5ulsqQT4kzZsYOsPut7YR+qbU7cj5dHi/XGAdY
# ye5/OgAteb3fGwdN9C+c+NAI7OHTPIlQ5YDBhD7rJcDNwzbPaGiYdCxXOY1xWCt1
# KL0FYisqyUOSTTiOtpo/8neVdzhfVfGMi6uWlZy8jBvK4wJ/swOfzf7PjucAgszl
# VaQ1e5sexoXxcWXvJrSa/5DXF+jJp2AeANm/i3yEsCVeNUjOpYzDXjCCBY0wggR1
# oAMCAQICEA6bGI750C3n79tQ4ghAGFowDQYJKoZIhvcNAQEMBQAwZTELMAkGA1UE
# BhMCVVMxFTATBgNVBAoTDERpZ2lDZXJ0IEluYzEZMBcGA1UECxMQd3d3LmRpZ2lj
# ZXJ0LmNvbTEkMCIGA1UEAxMbRGlnaUNlcnQgQXNzdXJlZCBJRCBSb290IENBMB4X
# DTIyMDgwMTAwMDAwMFoXDTMxMTEwOTIzNTk1OVowYjELMAkGA1UEBhMCVVMxFTAT
# BgNVBAoTDERpZ2lDZXJ0IEluYzEZMBcGA1UECxMQd3d3LmRpZ2ljZXJ0LmNvbTEh
# MB8GA1UEAxMYRGlnaUNlcnQgVHJ1c3RlZCBSb290IEc0MIICIjANBgkqhkiG9w0B
# AQEFAAOCAg8AMIICCgKCAgEAv+aQc2jeu+RdSjwwIjBpM+zCpyUuySE98orYWcLh
# Kac9WKt2ms2uexuEDcQwH/MbpDgW61bGl20dq7J58soR0uRf1gU8Ug9SH8aeFaV+
# vp+pVxZZVXKvaJNwwrK6dZlqczKU0RBEEC7fgvMHhOZ0O21x4i0MG+4g1ckgHWMp
# Lc7sXk7Ik/ghYZs06wXGXuxbGrzryc/NrDRAX7F6Zu53yEioZldXn1RYjgwrt0+n
# MNlW7sp7XeOtyU9e5TXnMcvak17cjo+A2raRmECQecN4x7axxLVqGDgDEI3Y1Dek
# LgV9iPWCPhCRcKtVgkEy19sEcypukQF8IUzUvK4bA3VdeGbZOjFEmjNAvwjXWkmk
# wuapoGfdpCe8oU85tRFYF/ckXEaPZPfBaYh2mHY9WV1CdoeJl2l6SPDgohIbZpp0
# yt5LHucOY67m1O+SkjqePdwA5EUlibaaRBkrfsCUtNJhbesz2cXfSwQAzH0clcOP
# 9yGyshG3u3/y1YxwLEFgqrFjGESVGnZifvaAsPvoZKYz0YkH4b235kOkGLimdwHh
# D5QMIR2yVCkliWzlDlJRR3S+Jqy2QXXeeqxfjT/JvNNBERJb5RBQ6zHFynIWIgnf
# fEx1P2PsIV/EIFFrb7GrhotPwtZFX50g/KEexcCPorF+CiaZ9eRpL5gdLfXZqbId
# 5RsCAwEAAaOCATowggE2MA8GA1UdEwEB/wQFMAMBAf8wHQYDVR0OBBYEFOzX44LS
# cV1kTN8uZz/nupiuHA9PMB8GA1UdIwQYMBaAFEXroq/0ksuCMS1Ri6enIZ3zbcgP
# MA4GA1UdDwEB/wQEAwIBhjB5BggrBgEFBQcBAQRtMGswJAYIKwYBBQUHMAGGGGh0
# dHA6Ly9vY3NwLmRpZ2ljZXJ0LmNvbTBDBggrBgEFBQcwAoY3aHR0cDovL2NhY2Vy
# dHMuZGlnaWNlcnQuY29tL0RpZ2lDZXJ0QXNzdXJlZElEUm9vdENBLmNydDBFBgNV
# HR8EPjA8MDqgOKA2hjRodHRwOi8vY3JsMy5kaWdpY2VydC5jb20vRGlnaUNlcnRB
# c3N1cmVkSURSb290Q0EuY3JsMBEGA1UdIAQKMAgwBgYEVR0gADANBgkqhkiG9w0B
# AQwFAAOCAQEAcKC/Q1xV5zhfoKN0Gz22Ftf3v1cHvZqsoYcs7IVeqRq7IviHGmlU
# Iu2kiHdtvRoU9BNKei8ttzjv9P+Aufih9/Jy3iS8UgPITtAq3votVs/59PesMHqa
# i7Je1M/RQ0SbQyHrlnKhSLSZy51PpwYDE3cnRNTnf+hZqPC/Lwum6fI0POz3A8eH
# qNJMQBk1RmppVLC4oVaO7KTVPeix3P0c2PR3WlxUjG/voVA9/HYJaISfb8rbII01
# YBwCA8sgsKxYoA5AY8WYIsGyWfVVa88nq2x2zm8jLfR+cWojayL/ErhULSd+2DrZ
# 8LaHlv1b0VysGMNNn3O3AamfV6peKOK5lDCCBrQwggScoAMCAQICEA3HrFcF/yGZ
# LkBDIgw6SYYwDQYJKoZIhvcNAQELBQAwYjELMAkGA1UEBhMCVVMxFTATBgNVBAoT
# DERpZ2lDZXJ0IEluYzEZMBcGA1UECxMQd3d3LmRpZ2ljZXJ0LmNvbTEhMB8GA1UE
# AxMYRGlnaUNlcnQgVHJ1c3RlZCBSb290IEc0MB4XDTI1MDUwNzAwMDAwMFoXDTM4
# MDExNDIzNTk1OVowaTELMAkGA1UEBhMCVVMxFzAVBgNVBAoTDkRpZ2lDZXJ0LCBJ
# bmMuMUEwPwYDVQQDEzhEaWdpQ2VydCBUcnVzdGVkIEc0IFRpbWVTdGFtcGluZyBS
# U0E0MDk2IFNIQTI1NiAyMDI1IENBMTCCAiIwDQYJKoZIhvcNAQEBBQADggIPADCC
# AgoCggIBALR4MdMKmEFyvjxGwBysddujRmh0tFEXnU2tjQ2UtZmWgyxU7UNqEY81
# FzJsQqr5G7A6c+Gh/qm8Xi4aPCOo2N8S9SLrC6Kbltqn7SWCWgzbNfiR+2fkHUil
# jNOqnIVD/gG3SYDEAd4dg2dDGpeZGKe+42DFUF0mR/vtLa4+gKPsYfwEu7EEbkC9
# +0F2w4QJLVSTEG8yAR2CQWIM1iI5PHg62IVwxKSpO0XaF9DPfNBKS7Zazch8NF5v
# p7eaZ2CVNxpqumzTCNSOxm+SAWSuIr21Qomb+zzQWKhxKTVVgtmUPAW35xUUFREm
# DrMxSNlr/NsJyUXzdtFUUt4aS4CEeIY8y9IaaGBpPNXKFifinT7zL2gdFpBP9qh8
# SdLnEut/GcalNeJQ55IuwnKCgs+nrpuQNfVmUB5KlCX3ZA4x5HHKS+rqBvKWxdCy
# QEEGcbLe1b8Aw4wJkhU1JrPsFfxW1gaou30yZ46t4Y9F20HHfIY4/6vHespYMQmU
# iote8ladjS/nJ0+k6MvqzfpzPDOy5y6gqztiT96Fv/9bH7mQyogxG9QEPHrPV6/7
# umw052AkyiLA6tQbZl1KhBtTasySkuJDpsZGKdlsjg4u70EwgWbVRSX1Wd4+zoFp
# p4Ra+MlKM2baoD6x0VR4RjSpWM8o5a6D8bpfm4CLKczsG7ZrIGNTAgMBAAGjggFd
# MIIBWTASBgNVHRMBAf8ECDAGAQH/AgEAMB0GA1UdDgQWBBTvb1NK6eQGfHrK4pBW
# 9i/USezLTjAfBgNVHSMEGDAWgBTs1+OC0nFdZEzfLmc/57qYrhwPTzAOBgNVHQ8B
# Af8EBAMCAYYwEwYDVR0lBAwwCgYIKwYBBQUHAwgwdwYIKwYBBQUHAQEEazBpMCQG
# CCsGAQUFBzABhhhodHRwOi8vb2NzcC5kaWdpY2VydC5jb20wQQYIKwYBBQUHMAKG
# NWh0dHA6Ly9jYWNlcnRzLmRpZ2ljZXJ0LmNvbS9EaWdpQ2VydFRydXN0ZWRSb290
# RzQuY3J0MEMGA1UdHwQ8MDowOKA2oDSGMmh0dHA6Ly9jcmwzLmRpZ2ljZXJ0LmNv
# bS9EaWdpQ2VydFRydXN0ZWRSb290RzQuY3JsMCAGA1UdIAQZMBcwCAYGZ4EMAQQC
# MAsGCWCGSAGG/WwHATANBgkqhkiG9w0BAQsFAAOCAgEAF877FoAc/gc9EXZxML2+
# C8i1NKZ/zdCHxYgaMH9Pw5tcBnPw6O6FTGNpoV2V4wzSUGvI9NAzaoQk97frPBtI
# j+ZLzdp+yXdhOP4hCFATuNT+ReOPK0mCefSG+tXqGpYZ3essBS3q8nL2UwM+NMvE
# uBd/2vmdYxDCvwzJv2sRUoKEfJ+nN57mQfQXwcAEGCvRR2qKtntujB71WPYAgwPy
# WLKu6RnaID/B0ba2H3LUiwDRAXx1Neq9ydOal95CHfmTnM4I+ZI2rVQfjXQA1WSj
# jf4J2a7jLzWGNqNX+DF0SQzHU0pTi4dBwp9nEC8EAqoxW6q17r0z0noDjs6+BFo+
# z7bKSBwZXTRNivYuve3L2oiKNqetRHdqfMTCW/NmKLJ9M+MtucVGyOxiDf06VXxy
# KkOirv6o02OoXN4bFzK0vlNMsvhlqgF2puE6FndlENSmE+9JGYxOGLS/D284NHNb
# oDGcmWXfwXRy4kbu4QFhOm0xJuF2EZAOk5eCkhSxZON3rGlHqhpB/8MluDezooIs
# 8CVnrpHMiD2wL40mm53+/j7tFaxYKIqL0Q4ssd8xHZnIn/7GELH3IdvG2XlM9q7W
# P/UwgOkw/HQtyRN62JK4S1C8uw3PdBunvAZapsiI5YKdvlarEvf8EA+8hcpSM9LH
# JmyrxaFtoza2zNaQ9k+5t1wwggbtMIIE1aADAgECAhAIT9wzT35FTtvDD4/5khg1
# MA0GCSqGSIb3DQEBCwUAMGkxCzAJBgNVBAYTAlVTMRcwFQYDVQQKEw5EaWdpQ2Vy
# dCwgSW5jLjFBMD8GA1UEAxM4RGlnaUNlcnQgVHJ1c3RlZCBHNCBUaW1lU3RhbXBp
# bmcgUlNBNDA5NiBTSEEyNTYgMjAyNSBDQTEwHhcNMjYwODA1MDAwMDAwWhcNMzcx
# MTA0MjM1OTU5WjBjMQswCQYDVQQGEwJVUzEXMBUGA1UEChMORGlnaUNlcnQsIElu
# Yy4xOzA5BgNVBAMTMkRpZ2lDZXJ0IFNIQTI1NiBSU0E0MDk2IFRpbWVzdGFtcCBS
# ZXNwb25kZXIgMjAyNiAxMIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEA
# tnum8sn+zUr41JtMZbP9OMYw+HwJDpG5xkIu/lqcfNYmMX81YmsUiHLbh9ykpeWB
# GKTLhYBrAN9Tdg/QEzG32XcObmgIblnr0CoQ3WSAeDZ6nH6X6VkFyYkJw3QBJREw
# vm4UhLzSxmwPA7cFKRTEOMsmEEj6qJk/dqLEAL+oQYuOwE2UuiX1Vnul8YReIyWd
# 4kgLn9gq6LNXM0UplkR6jL/QHxmb6fMoGBJYbnaUI7XD6cKDpekK2SVMld4iDbze
# HDtOaaxldH5IxuNusQ69nd8/ZXEiB5Hbxj3RlK13cX1W4DlFXKdv/CEhM8Cj1vvl
# mvhNroyPdRGbbpBlgyf8Wdu5N6ByhFwURn0U6ozlPoxN22v+fviUhP+6DR547OZn
# pBMWDfei1f5sVGwiiW/KQTWOK97g+4RJpPzPNV4VYMAwO2jM2Aty2QYPVmOQTJm0
# msuXnJrSbl2gf9JylpkJlWXqk1Q4LJsxz+TELoQCZIljbgvTJgoPU2R12ydv8i1U
# qL/adelA0y7U9Pmmtbze9Xx3rtajC5SzQd1jgfwAwsa90v9YcSPdmeoyoBBA/27c
# CL237l5DTYYPDLQ4ON3OLTGWnvRb6jDrf/T75gMRfUzSLCBQfBusm9+mSWRlC/Df
# 6S/e9Q8i13CuhzOT2Jx+V/nlbXM4QoBwlUAhelwwJT0CAwEAAaOCAZUwggGRMAwG
# A1UdEwEB/wQCMAAwHQYDVR0OBBYEFBTJY4owLtRK+26U8+bjQH717M3iMB8GA1Ud
# IwQYMBaAFO9vU0rp5AZ8esrikFb2L9RJ7MtOMA4GA1UdDwEB/wQEAwIHgDAWBgNV
# HSUBAf8EDDAKBggrBgEFBQcDCDCBlQYIKwYBBQUHAQEEgYgwgYUwJAYIKwYBBQUH
# MAGGGGh0dHA6Ly9vY3NwLmRpZ2ljZXJ0LmNvbTBdBggrBgEFBQcwAoZRaHR0cDov
# L2NhY2VydHMuZGlnaWNlcnQuY29tL0RpZ2lDZXJ0VHJ1c3RlZEc0VGltZVN0YW1w
# aW5nUlNBNDA5NlNIQTI1NjIwMjVDQTEuY3J0MF8GA1UdHwRYMFYwVKBSoFCGTmh0
# dHA6Ly9jcmwzLmRpZ2ljZXJ0LmNvbS9EaWdpQ2VydFRydXN0ZWRHNFRpbWVTdGFt
# cGluZ1JTQTQwOTZTSEEyNTYyMDI1Q0ExLmNybDAgBgNVHSAEGTAXMAgGBmeBDAEE
# AjALBglghkgBhv1sBwEwDQYJKoZIhvcNAQELBQADggIBAI3FOmEenVIK35msCYB+
# fShAsWvSYvLBItoNdAgQ2jIqrGsVsluXMJU/+mRebBc52s6lbKAvOVPXaizmKkML
# LflEEKDZQx4CkS2t8aHPjkXha3hYZ010htFa3dhNgmalH5vuWvh3tTCf4frTS7gP
# tGc4Z/xaPhQ2AB1mR8eEe/WbH0RWHvVIl6VwQ3+g5FKNfN2N/DWJkf13w2H+2Gfq
# Efbd35Ww8CvoYBjLNIDTadcPWdgsjsiOaK/7EsKJgLjUNIVgvcaFOLLQ/GlrA+0Z
# HJoFUbOr5SJN8zykPspXIXlpDJY/gqFUZRROeab9GVgmhbdOJcD/63RhxPahFUGb
# ckRONqMe6DYAv6/mOG0pWd3cPStsdcS7buj5DyniwRY8yooMH6ptx5vpP/pZzBPB
# eZD2U4IsthyxB5Jaa8qrOkB5z160TXiM5ADMspZ0TfD9MJoq0tFpFPssKRFhWeED
# YPvcUuN7U7lvcdHl4ezQ3NT/7Ffs1sR1yh/LRbdZ3B3Vc6q2WmD8mDC0p9kzl2o7
# 3iVtS946IkEj7FkRsZGww1teYxERROC745xrtjvcw9ZyyUjHZWGRIpJeMNsPquCD
# f0fkyHtB+J4AiNZqCQk23rxh+KbpyMTNVKItJ5l92Svl20U9NbqMBOVYl1h54NEY
# LJq1/xHWFKPNK903zJZA9P2DMYIFPTCCBTkCAQEwYTBNMQswCQYDVQQGEwJCUjEe
# MBwGA1UECgwVRVhBQklUIFNvbHVjb2VzIGVtIFRJMR4wHAYDVQQDDBVFWEFCSVQg
# U29sdWNvZXMgZW0gVEkCEBrdWS49sfW3QDalnuyLRCgwDQYJYIZIAWUDBAIBBQCg
# gYQwGAYKKwYBBAGCNwIBDDEKMAigAoAAoQKAADAZBgkqhkiG9w0BCQMxDAYKKwYB
# BAGCNwIBBDAcBgorBgEEAYI3AgELMQ4wDAYKKwYBBAGCNwIBFTAvBgkqhkiG9w0B
# CQQxIgQgV0WBdevR8r1UxD+g5JNpFCHI52L4cFj7h5EsNwySAxkwDQYJKoZIhvcN
# AQEBBQAEggEAvfoGY5n6I69L1Wu2rcmMqB+Sdosq7ulvI15Y4cvuyAvLh0Jm3dFY
# ZBDeWZq4EwX/+6xwJhYUY9alUNBi0hG5lYLBbuVQ6CYv7Pwb1RFJ6UTefYUT3TJe
# 47lnn3v/S2lroaTzaz7FXj/ekOVKvNJ52LZanE0RwUq4wmPy/5bZdLrzCVTyZex5
# D/7g4SoNiic3vax5k19SezQhj3PGaWjPNCbzg+spv0J8qRFJqwSImvUU8srb47yA
# N7z7qcZfxBbKMuHmc9F+SyLGWILd2/pTrqSw3d+QEp70ubK63UC8Y1iiOSiRZvV3
# UAjPggJFgCwem0b0RWCua358sjpfRInGLKGCAyYwggMiBgkqhkiG9w0BCQYxggMT
# MIIDDwIBATB9MGkxCzAJBgNVBAYTAlVTMRcwFQYDVQQKEw5EaWdpQ2VydCwgSW5j
# LjFBMD8GA1UEAxM4RGlnaUNlcnQgVHJ1c3RlZCBHNCBUaW1lU3RhbXBpbmcgUlNB
# NDA5NiBTSEEyNTYgMjAyNSBDQTECEAhP3DNPfkVO28MPj/mSGDUwDQYJYIZIAWUD
# BAIBBQCgaTAYBgkqhkiG9w0BCQMxCwYJKoZIhvcNAQcBMBwGCSqGSIb3DQEJBTEP
# Fw0yNjExMTkwNDA4MTdaMC8GCSqGSIb3DQEJBDEiBCDUM9ZP130dRm4UuafI6lik
# jDm4bCwdBRqEN1T0ngNcyTANBgkqhkiG9w0BAQEFAASCAgCV1dd330WRqKgW8Nwp
# +g8fiP0B9zYkgE0f5vV8MbPwmYEeuzJ9LjgixzG9FK7qirP8RWqBWF6zHiNoNha7
# uPXLwsRTF6/ode9/hdUoUuZUJhTXZk/Y3s6UFzufGoE851WmHbNIhyUbkC/KfV0N
# IGcy5PtZgWY/PxYd74ejvznX2TaU9QgFYHoYUYdSxeI9wNBx5qYacKftQa8sQZz+
# FE6x1IYOeYQ6bOlyjXg3iUnNdBlE1u6gq4W3k1lg7gQLVjeEoFaRnzKqpM/HdlmR
# vZl4f6AkyEo9fQij1fRG9vMbqRuXF/FRHqyZ5aMO+4Wd+njuUnBcSMnsEBpsniW1
# Y9wfHHMWhJJ2aiEsxBTTzHSRXPsLJWvmTxG0YGvgWIc/qPI7r+Lhcs/wbpeQNwIE
# UNJP918OthWeHiUug8ppRnKzz3D9bMnZYU+0vYuFHfO8SjvRZVb8cvms/jyrfNTy
# sExis6l/0f2vweTAFD5vpAVQ4WxazQ2KCFhQd437zV33Jq6M2eC+9vYyhkRdgQ1m
# uomMe3snOdlixmhe95Sp0SJNdOjsDl2cxzfp+YQB+bRhIf16RNkwdkiI3bmr1KP7
# Q8B1pRwR1TRAWOVZAM7+ujTndGZ8AQ0A2QE1ym93iby7leZexUciZgWRjQi9wtH4
# 1uYJBy/cAY1EBDoNh+SsPdxGwg==
# SIG # End signature block
