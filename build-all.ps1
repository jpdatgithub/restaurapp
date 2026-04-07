param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Clean,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'

Set-Location -Path $PSScriptRoot

$solution = Join-Path $PSScriptRoot 'Restaurapp.sln'

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

if (-not (Test-Path -Path $solution)) {
    throw "Solução não encontrada: $solution"
}

if ($Clean) {
    Write-Host "Executando limpeza da solução ($Configuration)..."
    Invoke-Dotnet -Arguments @('clean', $solution, '-c', $Configuration)
}

if (-not $NoRestore) {
    Write-Host "Executando restore da solução..."
    Invoke-Dotnet -Arguments @('restore', $solution)
}

Write-Host "Executando build da solução ($Configuration)..."
if ($NoRestore) {
    Invoke-Dotnet -Arguments @('build', $solution, '-c', $Configuration, '--no-restore')
}
else {
    Invoke-Dotnet -Arguments @('build', $solution, '-c', $Configuration)
}

Write-Host "Build concluído com sucesso." -ForegroundColor Green