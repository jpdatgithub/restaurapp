param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

Set-Location -Path $PSScriptRoot

$project = "Restaurapp.BlazorServer\Restaurapp.BlazorServer.csproj"

if (-not (Test-Path -Path $project)) {
    throw "Projeto '$project' não encontrado em $PSScriptRoot"
}

if (-not $Force) {
    Write-Host "ATENÇÃO: esta ação irá apagar a base de dados do ambiente configurado (Development)." -ForegroundColor Yellow
    $confirm = Read-Host "Digite APAGAR para confirmar"

    if ($confirm -ne "APAGAR") {
        Write-Host "Operação cancelada." -ForegroundColor Yellow
        exit 0
    }
}

Write-Host "Executando: dotnet ef database drop..."
dotnet ef database drop --project $project --startup-project $project --context AppDbContext --force

Write-Host "Base de dados removida com sucesso." -ForegroundColor Green
