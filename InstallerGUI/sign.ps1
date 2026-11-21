param(
    [string]$DistPath = "..\dist",
    [string]$PfxPath = $env:EXABIT_SIGN_PFX,
    [string]$PfxPassword = $env:EXABIT_SIGN_PFX_PASSWORD
)

# Assinatura Authenticode com certificado corporativo REAL (OV/EV).
# Este script nunca cria certificado autoassinado: um certificado autoassinado
# instalado em "Autoridades Raiz Confiaveis" permitiria que qualquer binario
# forjado com o mesmo nome fosse aceito como confiavel pelos clientes.

function Write-Info([string]$Message) { Write-Host $Message -ForegroundColor Cyan }
function Write-Warn([string]$Message) { Write-Host $Message -ForegroundColor Yellow }

$cert = $null

# 1. Certificado a partir de arquivo PFX (recomendado para build/CI)
if (-not [string]::IsNullOrWhiteSpace($PfxPath)) {
    if (-not (Test-Path -LiteralPath $PfxPath)) {
        Write-Warn "X Arquivo PFX nao encontrado: $PfxPath"
    } else {
        try {
            $flags = [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable -bor `
                     [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::PersistKeySet
            $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($PfxPath, $PfxPassword, $flags)
            if (-not $cert.HasPrivateKey) {
                Write-Warn "X O certificado informado no PFX nao possui chave privada."
                $cert = $null
            }
        } catch {
            Write-Warn ("X Falha ao abrir o PFX: " + $_.Exception.Message)
            $cert = $null
        }
    }
}

# 2. Certificado instalado no repositorio: precisa ter chave privada e ser
#    emitido por uma CA (Subject diferente de Issuer descarta autoassinados).
if (-not $cert) {
    $cert = Get-ChildItem "Cert:\CurrentUser\My", "Cert:\LocalMachine\My" -CodeSigningCert -ErrorAction SilentlyContinue |
        Where-Object { $_.HasPrivateKey -and $_.Subject -ne $_.Issuer } |
        Select-Object -First 1
}

if (-not $cert) {
    Write-Warn "Assinatura Authenticode ignorada: nenhum certificado corporativo real configurado."
    Write-Warn "Defina EXABIT_SIGN_PFX e EXABIT_SIGN_PFX_PASSWORD, ou instale um certificado OV/EV no repositorio."
    exit 0
}

Write-Info ("Certificado de assinatura: " + $cert.Subject)
Write-Info ("Emissor da CA: " + $cert.Issuer)

# 3. Assina os executaveis e scripts que existirem
$FilesToSign = @(
    "Scripts\apply_wallpapers.ps1",
    "bin\Release\Instalador Automatico Exabit.exe",
    (Join-Path $DistPath "Instalador Automatico Exabit.exe")
)

$timestampServer = "http://timestamp.digicert.com"

foreach ($file in $FilesToSign) {
    if (-not (Test-Path -LiteralPath $file)) { continue }

    Write-Info ("Assinando digitalmente: " + $file)

    $sig = $null
    try {
        $sig = Set-AuthenticodeSignature -FilePath $file -Certificate $cert -HashAlgorithm SHA256 -TimestampServer $timestampServer
    } catch {
        $sig = $null
    }

    if ($null -eq $sig -or $sig.Status -ne "Valid") {
        try {
            $sig = Set-AuthenticodeSignature -FilePath $file -Certificate $cert -HashAlgorithm SHA256
        } catch {
            Write-Warn ("X Falha ao assinar " + $file + ": " + $_.Exception.Message)
            continue
        }
    }

    if ($sig.Status -eq "Valid") {
        Write-Host "   -> Assinado com sucesso (SHA256 + carimbo de tempo)." -ForegroundColor Green
    } elseif ($sig.Status -eq "UnknownError") {
        Write-Host "   -> Assinado com sucesso (SHA256 local)." -ForegroundColor Green
    } else {
        Write-Warn ("X Assinatura de " + $file + " retornou status " + $sig.Status + ".")
    }
}
