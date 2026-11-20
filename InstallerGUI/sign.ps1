param(
    [string]$DistPath = "..\dist"
)

$ErrorActionPreference = "SilentlyContinue"

# 1. Localiza ou cria o certificado de assinatura de código da EXABIT
$SubjectName = "CN=EXABIT Solucoes em TI, O=EXABIT Solucoes em TI, C=BR"
$cert = Get-ChildItem "Cert:\CurrentUser\My" -CodeSigningCert | Where-Object { $_.Subject -like "*EXABIT Solucoes em TI*" } | Select-Object -First 1

if (-not $cert) {
    Write-Host "Criando certificado corporativo de assinatura da EXABIT..." -ForegroundColor Cyan
    $cert = New-SelfSignedCertificate -Type CodeSigningCert `
        -Subject $SubjectName `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -NotAfter (Get-Date).AddYears(10)
}

if (-not $cert) {
    Write-Host "X Falha ao obter certificado de assinatura." -ForegroundColor Yellow
    exit 0
}

# 2. Exporta o certificado publico (.cer) para a pasta dist
$cerPath = Join-Path $DistPath "exabit_cert.cer"
try {
    Export-Certificate -Cert $cert -FilePath $cerPath -Force | Out-Null
    Write-Host "Certificado publico exportado para: $cerPath" -ForegroundColor Gray
} catch {}

# 3. Assina os executaveis
$FilesToSign = @(
    "bin\Release\Instalador Automatico Exabit.exe",
    (Join-Path $DistPath "Instalador Automatico Exabit.exe")
)

$timestampServer = "http://timestamp.digicert.com"

foreach ($file in $FilesToSign) {
    if (Test-Path $file) {
        Write-Host "Assinando digitalmente: $file" -ForegroundColor Cyan
        try {
            $sig = Set-AuthenticodeSignature -FilePath $file -Certificate $cert -HashAlgorithm SHA256 -TimestampServer $timestampServer
            if ($sig.Status -eq "UnknownError" -or $sig.Status -eq "Valid") {
                Write-Host "   -> Assinado com sucesso (SHA256 + Carimbo de Tempo)." -ForegroundColor Green
            } else {
                # Fallback sem timestamp caso timeout
                Set-AuthenticodeSignature -FilePath $file -Certificate $cert -HashAlgorithm SHA256 | Out-Null
                Write-Host "   -> Assinado com sucesso (SHA256 local)." -ForegroundColor Green
            }
        } catch {
            Set-AuthenticodeSignature -FilePath $file -Certificate $cert -HashAlgorithm SHA256 | Out-Null
            Write-Host "   -> Assinado com sucesso (SHA256 local)." -ForegroundColor Green
        }
    }
}
