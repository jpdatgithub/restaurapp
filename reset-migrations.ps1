$ErrorActionPreference = 'Stop'

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Description,

        [Parameter(Mandatory = $true)]
        [scriptblock] $Action
    )

    Write-Host "Executando: $Description"
    & $Action

    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao executar: $Description"
    }
}

Set-Location -Path (Join-Path $PSScriptRoot "Restaurapp.BlazorServer")

if (-not (Test-Path -Path "Migrations")) {
    throw "Pasta 'Migrations' não encontrada em $(Get-Location)"
}

Invoke-Step "dotnet ef database drop --force" {
    dotnet ef database drop --force
}

Write-Host "Limpando conteúdo de Migrations..."
Get-ChildItem -Path "Migrations" -Force | Remove-Item -Recurse -Force

Invoke-Step "dotnet ef migrations add migration1" {
    dotnet ef migrations add migration1
}

Invoke-Step "dotnet ef database update" {
    dotnet ef database update
}

Write-Host "Concluído com sucesso." -ForegroundColor Green
