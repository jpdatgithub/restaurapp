param(
    [string]$VmName = "unfailing-duckbill",
    [string]$ServiceName = "restaurapp-server-staging",
    [string]$ProjectPath = ".\Restaurapp.BlazorServer\Restaurapp.BlazorServer.csproj",
    [string]$StartupProjectPath = ".\Restaurapp.BlazorServer\Restaurapp.BlazorServer.csproj",
    [string]$SqlOutputPath,
    [string]$DbConnectionString,
    [switch]$SkipServiceRestart
)

$ErrorActionPreference = "Stop"

Set-Location -Path $PSScriptRoot

function Invoke-Dotnet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao executar: dotnet $($Arguments -join ' ')"
    }
}

function Invoke-Multipass {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & multipass @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao executar: multipass $($Arguments -join ' ')"
    }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet não encontrado no PATH."
}

if (-not (Get-Command multipass -ErrorAction SilentlyContinue)) {
    throw "multipass não encontrado no PATH."
}

if (-not (Test-Path -Path $ProjectPath)) {
    throw "ProjectPath não encontrado: $ProjectPath"
}

if (-not (Test-Path -Path $StartupProjectPath)) {
    throw "StartupProjectPath não encontrado: $StartupProjectPath"
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
if ([string]::IsNullOrWhiteSpace($SqlOutputPath)) {
    $SqlOutputPath = Join-Path $PSScriptRoot "artifacts\staging-migrations-$timestamp.sql"
}

$sqlDir = Split-Path -Path $SqlOutputPath -Parent
if (-not [string]::IsNullOrWhiteSpace($sqlDir)) {
    New-Item -ItemType Directory -Path $sqlDir -Force | Out-Null
}

Write-Host "[1/6] Gerando script idempotente de migrations..." -ForegroundColor Cyan
Invoke-Dotnet -Arguments @(
    "ef", "migrations", "script",
    "--idempotent",
    "--project", $ProjectPath,
    "--startup-project", $StartupProjectPath,
    "-o", $SqlOutputPath
)

$vmTempSqlPath = "/tmp/staging-migrations-$timestamp.sql"

Write-Host "[2/6] Transferindo script para a VM '$VmName'..." -ForegroundColor Cyan
Invoke-Multipass -Arguments @("transfer", $SqlOutputPath, "$VmName`:$vmTempSqlPath")

if ([string]::IsNullOrWhiteSpace($DbConnectionString)) {
    Write-Host "[3/6] Descobrindo connection string via systemd ($ServiceName)..." -ForegroundColor Cyan

    $discoverCmd = "sudo systemctl show $ServiceName -p Environment --value | grep -o \"ConnectionStrings__DefaultConnection=[^ ]*\" | head -n 1 | sed 's/^ConnectionStrings__DefaultConnection=//'"
    $discovered = & multipass exec $VmName -- bash -lc $discoverCmd

    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao ler Environment do serviço '$ServiceName'."
    }

    $DbConnectionString = ($discovered | Out-String).Trim()

    if ([string]::IsNullOrWhiteSpace($DbConnectionString) -and -not [string]::IsNullOrWhiteSpace($env:STAGING_DB_CONNECTION_STRING)) {
        $DbConnectionString = $env:STAGING_DB_CONNECTION_STRING
        Write-Host "[3/6] Connection string obtida da variável STAGING_DB_CONNECTION_STRING." -ForegroundColor Yellow
    }

    if ([string]::IsNullOrWhiteSpace($DbConnectionString)) {
        $manualConnection = Read-Host "ConnectionStrings__DefaultConnection não encontrada no serviço '$ServiceName'. Informe a connection string de staging"
        if (-not [string]::IsNullOrWhiteSpace($manualConnection)) {
            $DbConnectionString = $manualConnection.Trim()
        }
    }

    if ([string]::IsNullOrWhiteSpace($DbConnectionString)) {
        throw "ConnectionStrings__DefaultConnection não encontrada no serviço '$ServiceName' e não informada manualmente."
    }
}
else {
    Write-Host "[3/6] Usando connection string informada por parâmetro." -ForegroundColor Cyan
}

Write-Host "[4/6] Aplicando migrations no Postgres de staging..." -ForegroundColor Cyan
$applyCmd = "psql '$DbConnectionString' -v ON_ERROR_STOP=1 -f '$vmTempSqlPath'"
Invoke-Multipass -Arguments @("exec", $VmName, "--", "bash", "-lc", $applyCmd)

if (-not $SkipServiceRestart) {
    Write-Host "[5/6] Reiniciando serviço '$ServiceName'..." -ForegroundColor Cyan
    Invoke-Multipass -Arguments @("exec", $VmName, "--", "bash", "-lc", "sudo systemctl restart $ServiceName && systemctl is-active $ServiceName")
}
else {
    Write-Host "[5/6] SkipServiceRestart habilitado. Serviço não reiniciado." -ForegroundColor Yellow
}

Write-Host "[6/6] Limpando SQL temporário da VM..." -ForegroundColor Cyan
Invoke-Multipass -Arguments @("exec", $VmName, "--", "bash", "-lc", "rm -f '$vmTempSqlPath'")

Write-Host "Migrations aplicadas com sucesso no staging." -ForegroundColor Green
Write-Host "Arquivo SQL gerado em: $SqlOutputPath"