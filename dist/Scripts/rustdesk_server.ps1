# ==============================================================================
# RUSTDESK - CONFIGURACAO SERVIDOR PROPRIO EXABIT + LOGOUT DE CONTA
# Executar no PowerShell como Administrador
# ==============================================================================

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$AdminPassword = ""
)

$ServerHost = "rustdesk.exabit.com.br"
$PublicKey = "vh1s9x96k3ReaRVL2z+KtgPAAhePvFhiluSZpqipmQ8="
$ExePath = "C:\Program Files\RustDesk\rustdesk.exe"

if (-not (Test-Path $ExePath)) {
    Write-Host "RustDesk nao encontrado em $ExePath. Verificando instalacao..." -ForegroundColor Yellow
    $LocalInstaller = Join-Path $PSScriptRoot "..\Programas\rustdesk_x86_64.exe"
    $InstallerToRun = $null

    $HasInternet = $false
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $testReq = [System.Net.HttpWebRequest]::Create("http://www.msftconnecttest.com/connecttest.txt")
        $testReq.Timeout = 4000
        $testResp = $testReq.GetResponse()
        if ($testResp.StatusCode -eq [System.Net.HttpStatusCode]::OK) {
            $HasInternet = $true
        }
        $testResp.Close()
    } catch {
        $HasInternet = $false
    }

    if ($HasInternet) {
        Write-Host "Conexao com a internet detectada. Verificando versao mais recente do RustDesk..." -ForegroundColor Cyan
        try {
            $Release = Invoke-RestMethod -Uri "https://api.github.com/repos/rustdesk/rustdesk/releases/latest" -Headers @{"User-Agent"="Mozilla/5.0"} -TimeoutSec 10
            $Asset = $Release.assets | Where-Object { $_.name -like "*x86_64.exe" } | Select-Object -First 1
            if ($Asset -and $Asset.browser_download_url) {
                $TargetFolder = Join-Path $PSScriptRoot "..\Programas"
                if (-not (Test-Path $TargetFolder)) {
                    $TargetFolder = Join-Path $env:LOCALAPPDATA "Exabit\AutoInstall\Updates"
                    if (-not (Test-Path $TargetFolder)) { New-Item -ItemType Directory -Path $TargetFolder -Force | Out-Null }
                }
                $TargetExe = Join-Path $TargetFolder $Asset.name
                Write-Host "Baixando $($Asset.name)..." -ForegroundColor Cyan
                Invoke-WebRequest -Uri $Asset.browser_download_url -OutFile $TargetExe -UseBasicParsing -TimeoutSec 120
                if (Test-Path $TargetExe) {
                    $InstallerToRun = $TargetExe
                    Write-Host "Versao mais recente baixada com sucesso." -ForegroundColor Green
                }
            }
        } catch {
            Write-Host "Aviso: Nao foi possivel baixar da API do GitHub. Tentando obter versao local..." -ForegroundColor Yellow
        }
    }

    if (-not $InstallerToRun -and (Test-Path $LocalInstaller)) {
        $InstallerToRun = $LocalInstaller
        Write-Host "Usando instalador local do RustDesk da pasta Programas..." -ForegroundColor Cyan
    }

    if ($InstallerToRun) {
        Write-Host "Instalando RustDesk silenciosamente..." -ForegroundColor Cyan
        Start-Process -FilePath $InstallerToRun -ArgumentList "--silent-install" -Wait
        Start-Sleep -Seconds 5
    } else {
        Write-Host "X Nenhum instalador do RustDesk disponivel." -ForegroundColor Red
    }
}

Write-Host "1. Parando servico e processos do RustDesk..." -ForegroundColor Cyan
Stop-Service -Name "RustDesk" -Force -ErrorAction SilentlyContinue
Stop-Process -Name "rustdesk" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

$TargetDirs = New-Object System.Collections.Generic.List[string]
$TargetDirs.Add("C:\Windows\ServiceProfiles\LocalService\AppData\Roaming\RustDesk\config")
$TargetDirs.Add("C:\Users\Default\AppData\Roaming\RustDesk\config")

Get-ChildItem "C:\Users" -Directory -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -notin @("Public", "Default", "Default User", "All Users")
} | ForEach-Object {
    $UserRustDesk = Join-Path $_.FullName "AppData\Roaming\RustDesk\config"
    if (-not $TargetDirs.Contains($UserRustDesk)) {
        $TargetDirs.Add($UserRustDesk)
    }
}

$TomlContent = @"
rendezvous_server = '$ServerHost`:21116'
nat_type = 1
serial = 0
unlock_pin = ''

[options]
custom-rendezvous-server = '$ServerHost'
relay-server = '$ServerHost'
key = '$PublicKey'
api-server = ''
access_token = ''
user_info = ''
approve-mode = 'password-click'
verification-method = 'use-both-passwords'
"@

$Utf8NoBom = New-Object System.Text.UTF8Encoding $false
$AuthRegex = "(?ms)^\s*(access_token|user_info|api-server|login_device_token|oauth_.*|oidc_.*|account_.*|token)\s*=\s*('[^']*'|`"[^`"]*`"|[^\r\n]*)\r?\n?"

Write-Host "2. Desconectando conta e aplicando servidor EXABIT..." -ForegroundColor Cyan
foreach ($Dir in $TargetDirs) {
    if (-not (Test-Path $Dir)) {
        New-Item -ItemType Directory -Path $Dir -Force | Out-Null
    }

    foreach ($TomlFile in @("RustDesk.toml", "RustDesk_default.toml", "RustDesk_local.toml")) {
        $FullPath = Join-Path $Dir $TomlFile
        if (Test-Path $FullPath) {
            $Raw = Get-Content $FullPath -Raw -ErrorAction SilentlyContinue
            if ($Raw) {
                $Clean = [System.Text.RegularExpressions.Regex]::Replace($Raw, $AuthRegex, "")
                if ($Clean -match "approve-mode\s*=") {
                    $Clean = [System.Text.RegularExpressions.Regex]::Replace($Clean, "approve-mode\s*=\s*('[^']*'|`"[^`"]*`"|[^\r\n]*)", "approve-mode = 'password-click'")
                }
                if ($Clean -match "verification-method\s*=") {
                    $Clean = [System.Text.RegularExpressions.Regex]::Replace($Clean, "verification-method\s*=\s*('[^']*'|`"[^`"]*`"|[^\r\n]*)", "verification-method = 'use-both-passwords'")
                }
                [System.IO.File]::WriteAllText($FullPath, $Clean, $Utf8NoBom)
            }
        }
    }

    $Toml2Path = Join-Path $Dir "RustDesk2.toml"
    [System.IO.File]::WriteAllText($Toml2Path, $TomlContent, $Utf8NoBom)
    Write-Host "   -> Configurado e deslogado: $Dir" -ForegroundColor Gray
}

Write-Host "3. Iniciando servico RustDesk..." -ForegroundColor Cyan
Start-Service -Name "RustDesk" -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3

if (Test-Path $ExePath) {
    Write-Host "4. Aplicando modos de permissao no RustDesk..." -ForegroundColor Cyan
    Start-Process -FilePath $ExePath -ArgumentList "--option approve-mode password-click" -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue
    Start-Process -FilePath $ExePath -ArgumentList "--option verification-method use-both-passwords" -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue

    if (-not [string]::IsNullOrWhiteSpace($AdminPassword)) {
        Write-Host "   -> Configurando senha permanente (mesma do Admin)..." -ForegroundColor Cyan
        Start-Process -FilePath $ExePath -ArgumentList "--password `"$AdminPassword`"" -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue
        Write-Host "   -> Senha definitiva configurada com sucesso." -ForegroundColor Green
    }

    Start-Process -FilePath $ExePath
}

$MensagemFinal = @"

======================================================================
 IMPORTANTE: RUSTDESK CONFIGURADO PARA O SERVIDOR EXABIT
======================================================================
 
 Concluido!
 - Servidor: rustdesk.exabit.com.br
 - Chave publica aplicada: $PublicKey
 
 Modos de acesso liberados:
 - Senha temporaria
 - Aprovacao por clique do usuario
 - Senha definitiva (mesma senha do Admin)
 
 Sua conta anterior nao deve ser mais utilizada para acessar 
 os dispositivos remotos.
  
======================================================================

"@
Clear-Host; Write-Host $MensagemFinal -ForegroundColor Green
# SIG # Begin signature block
# MIIcjwYJKoZIhvcNAQcCoIIcgDCCHHwCAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCCOuFzeltehYc/L
# 90b1iAt8CEq7YSSw0POuR55JPV2xV6CCFqgwggNqMIICUqADAgECAhAa3VkuPbH1
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
# CQQxIgQgloZgxjgEMfSIs7XyPAKKakp0nS5RR8pxK0iOrQVZALkwDQYJKoZIhvcN
# AQEBBQAEggEAPpHDAhec1enZlTFYnaERhw9A9Z2USVKH37/fiEXGZSOKPaR4P15J
# YkgFodVgSuoorBe/PgDSLyQSIRdcn8kWkSnV+lZKfepsa3qb2BHBkeZMcuN5qhmi
# 8SXc2nCDNl4m37HDdgl8ZCa7P8PS0Ph1Z8F1z4IQPrpezIcUzZyFSDyrI9Dr0ZYX
# b81siROicHasqGm9vuaIXAfY0/8h5nAbooEf/okk/ZY1176BpGtlR1SqRbnjeUBO
# v+gKPMEdHK8XqX9h1WhRSkZUPgOZhsBkGr2UhEF/oKMWI4an3L31fneHPytjt//w
# GJKa83HkU6Bhg105NO73oYgbcFh7TvIs26GCAyYwggMiBgkqhkiG9w0BCQYxggMT
# MIIDDwIBATB9MGkxCzAJBgNVBAYTAlVTMRcwFQYDVQQKEw5EaWdpQ2VydCwgSW5j
# LjFBMD8GA1UEAxM4RGlnaUNlcnQgVHJ1c3RlZCBHNCBUaW1lU3RhbXBpbmcgUlNB
# NDA5NiBTSEEyNTYgMjAyNSBDQTECEAhP3DNPfkVO28MPj/mSGDUwDQYJYIZIAWUD
# BAIBBQCgaTAYBgkqhkiG9w0BCQMxCwYJKoZIhvcNAQcBMBwGCSqGSIb3DQEJBTEP
# Fw0yNjExMTkxNDE5NTRaMC8GCSqGSIb3DQEJBDEiBCDIDkldjdJrQkKIFQepTqyw
# 3JwrudmGX+qSHwnJS579kDANBgkqhkiG9w0BAQEFAASCAgCRC/ufyVDHTxZWIeOB
# 77NR58LzkFz5vGL5uV4s58tAehB2kr/dlDni2H2sBRbud+gbPjKgzx5lUfsTZd18
# v7BiOCMhTGXwj2P5qaC/VTWM1/en0wcq5T627BTAqXVtxbe8C1IPTzApo5Yjm6r+
# 3NcZ6P50NX6kzUz1mRaDU1PETJcPqCLQYmN3H/L4VCOy46AcMZMpPs4CojaI59km
# gv2r94+VRIeAvhgaUjXJ8mzWx4y0OLz6cpjumn7GuCby0sA4Pqg6SNmBTgpCo0Xj
# k1FbfFlM7INbBTt2CKH5WgkfH3uQKUtCCE7gAf1ybkQ/5UAhs2iHTuA5frbu0P1K
# xv/BunRsEHpiJR6BMluXSeAC2KpDbLcNkxSCYAvdCbW+55spyAGMGulLUnNkOf3c
# CgfXRR9YS1wiU9W2yb5saQ+KpSCEbgPDz9u69AajP91ef0VlggB/pBAiPOTJwADR
# RistxPUJOihuazpXAZ6+0r/pZ4UIpHgQDB/LJjF154R9Xw0robXL9TZ5kyKV97/B
# qNhuX7Y03C49pLDUiebpazXwTLwtqxG7zYs9mf4JBw8vUPwuusBgsp5VRqjcrn0m
# 1o7F2hl2DmeYHShb0OX928D7/GtEMUqjFLfShs4beFHNk36dwIz/onrKMInnOede
# MtaAG8UBl8m7Mrt3LS+nDxnjqg==
# SIG # End signature block
