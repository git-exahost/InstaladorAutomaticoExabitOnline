$ErrorActionPreference = "Stop"

$assemblyInfoPath = Join-Path $PSScriptRoot "Properties\AssemblyInfo.cs"
$manifestPath = Join-Path $PSScriptRoot "app.manifest"

$assemblyInfo = Get-Content -LiteralPath $assemblyInfoPath -Raw
$manifest = Get-Content -LiteralPath $manifestPath -Raw

$assemblyVersionMatch = [regex]::Match($assemblyInfo, '\[assembly:\s*AssemblyVersion\("([^"]+)"\)\]')
$fileVersionMatch = [regex]::Match($assemblyInfo, '\[assembly:\s*AssemblyFileVersion\("([^"]+)"\)\]')
$manifestVersionMatch = [regex]::Match($manifest, '<assemblyIdentity\s+version="([^"]+)"')

if (-not $assemblyVersionMatch.Success -or -not $fileVersionMatch.Success -or -not $manifestVersionMatch.Success) {
    throw "Não foi possível ler todas as versões do assembly e do manifesto."
}

$assemblyVersion = $assemblyVersionMatch.Groups[1].Value
$fileVersion = $fileVersionMatch.Groups[1].Value
$manifestVersion = $manifestVersionMatch.Groups[1].Value

if ($assemblyVersion -ne $fileVersion -or $assemblyVersion -ne $manifestVersion) {
    throw "Versões divergentes: AssemblyVersion=$assemblyVersion, AssemblyFileVersion=$fileVersion, manifesto=$manifestVersion."
}

Write-Host "Versão validada: $assemblyVersion"
