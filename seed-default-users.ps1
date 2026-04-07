$ErrorActionPreference = "Stop"

Write-Host "Executando seed dos usuarios padrao..." -ForegroundColor Cyan

# Executa o projeto de seeding dedicado, separado do runtime do servidor.
dotnet run --project .\tools\DefaultUsersSeeder\DefaultUsersSeeder.csproj

if ($LASTEXITCODE -ne 0) {
    throw "Falha ao executar seed dos usuarios."
}

Write-Host "Seed finalizado com sucesso." -ForegroundColor Green
Write-Host "Servidor: teste / teste@gmail.com / Teste123@"
Write-Host "Cliente:  uteste / uteste@gmail.com / Teste123@"
