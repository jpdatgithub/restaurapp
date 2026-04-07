$ErrorActionPreference = 'Stop'

Set-Location -Path (Join-Path $PSScriptRoot "Restaurapp.BlazorServer")

if (-not (Test-Path -Path "Migrations")) {
    throw "Pasta 'Migrations' não encontrada em $(Get-Location)"
}

Write-Host "Limpando conteúdo de Migrations..."
Get-ChildItem -Path "Migrations" -Force | Remove-Item -Recurse -Force

Write-Host "Executando: dotnet ef database update 0"
dotnet ef database update 0

Write-Host "Executando: dotnet ef migrations add migration1"
dotnet ef migrations add migration1

Write-Host "Executando: dotnet ef database update"
dotnet ef database update

Write-Host "Concluído com sucesso." -ForegroundColor Green
