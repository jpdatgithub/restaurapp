param(
    [string]$VmName = "desenvolve-mentos"
)

$ErrorActionPreference = "Stop"

function Write-Info([string]$msg) { Write-Host "[INFO] $msg" -ForegroundColor Cyan }
function Write-Ok([string]$msg) { Write-Host "[OK]   $msg" -ForegroundColor Green }
function Write-Warn([string]$msg) { Write-Host "[WARN] $msg" -ForegroundColor Yellow }

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Error "Execute este script em PowerShell como Administrador."
}

if (-not (Get-Command multipass -ErrorAction SilentlyContinue)) {
    Write-Error "multipass nao encontrado no PATH."
}

Write-Info "Lendo IP atual da VM $VmName..."
$ipLines = (& multipass info $VmName 2>$null | Select-String "IPv4:\s+|^\s{2,}[0-9]+\.")
$ips = @()
foreach ($line in $ipLines) {
    $text = $line.ToString().Trim()
    $text = $text -replace "^IPv4:\s+", ""
    if ($text -match "^([0-9]{1,3}\.){3}[0-9]{1,3}$") {
        $ips += $text
    }
}

# Preferir IP de LAN (192.168.x.x) para evitar enderecos internos de bridge/NAT
$vmIp = $ips | Where-Object { $_ -like "192.168.*" } | Select-Object -First 1
if (-not $vmIp) {
    $vmIp = $ips | Where-Object { $_ -notlike "172.17.0.*" } | Select-Object -First 1
}
if (-not $vmIp) {
    $vmIp = $ips | Select-Object -First 1
}

if (-not $vmIp) {
    Write-Error "Nao foi possivel obter o IPv4 da VM $VmName."
}

Write-Ok "IP atual da VM: $vmIp"

Write-Info "Atualizando regras de portproxy (80 e 443)..."
& netsh interface portproxy delete v4tov4 listenaddress=0.0.0.0 listenport=80 | Out-Null
& netsh interface portproxy delete v4tov4 listenaddress=0.0.0.0 listenport=443 | Out-Null
& netsh interface portproxy add v4tov4 listenaddress=0.0.0.0 listenport=80 connectaddress=$vmIp connectport=80 | Out-Null
& netsh interface portproxy add v4tov4 listenaddress=0.0.0.0 listenport=443 connectaddress=$vmIp connectport=443 | Out-Null

Write-Info "Garantindo regras de firewall no Windows..."
if (-not (Get-NetFirewallRule -DisplayName "Restaurapp HTTP" -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName "Restaurapp HTTP" -Direction Inbound -Protocol TCP -LocalPort 80 -Action Allow -Profile Any | Out-Null
    Write-Ok "Regra criada: Restaurapp HTTP"
}
if (-not (Get-NetFirewallRule -DisplayName "Restaurapp HTTPS" -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName "Restaurapp HTTPS" -Direction Inbound -Protocol TCP -LocalPort 443 -Action Allow -Profile Any | Out-Null
    Write-Ok "Regra criada: Restaurapp HTTPS"
}

Write-Info "Estado final do portproxy:"
& netsh interface portproxy show all

Write-Info "Teste local rapido (host header staging):"
& curl.exe -I -H "Host: restaurappempresa-staging.servti.com.br" http://127.0.0.1/

Write-Host ""
Write-Ok "Concluido. Se o celular nao abrir, confirme DNS local/publico apontando para o IP do host Windows."
