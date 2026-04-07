param(
    [switch]$Https
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$serverProject = Join-Path $root 'Restaurapp.BlazorServer\Restaurapp.BlazorServer.csproj'
$clientProject = Join-Path $root 'Restaurapp.ClienteWasm\Restaurapp.ClienteWasm.csproj'

if (-not (Test-Path $serverProject)) {
    throw "Projeto do servidor não encontrado: $serverProject"
}

if (-not (Test-Path $clientProject)) {
    throw "Projeto do cliente não encontrado: $clientProject"
}

$launchProfile = if ($Https) { 'https' } else { 'http' }
$serverUrl = if ($Https) { 'https://localhost:7011' } else { 'http://localhost:5197' }
$clientUrl = if ($Https) { 'https://localhost:7170' } else { 'http://localhost:5129' }

Write-Host "Iniciando servidor ($launchProfile)..."
$serverProcess = Start-Process -FilePath 'dotnet' `
    -ArgumentList @('run', '--project', $serverProject, '--launch-profile', $launchProfile) `
    -WorkingDirectory $root `
    -PassThru

Write-Host "Iniciando cliente WASM ($launchProfile)..."
$clientProcess = Start-Process -FilePath 'dotnet' `
    -ArgumentList @('run', '--project', $clientProject, '--launch-profile', $launchProfile) `
    -WorkingDirectory $root `
    -PassThru

Start-Sleep -Seconds 3

Write-Host ''
Write-Host 'Projetos iniciados com sucesso.'
Write-Host "Servidor: $serverUrl (PID: $($serverProcess.Id))"
Write-Host "Cliente : $clientUrl (PID: $($clientProcess.Id))"
Write-Host ''
Write-Host 'Para encerrar depois:'
Write-Host "Stop-Process -Id $($serverProcess.Id),$($clientProcess.Id)"
