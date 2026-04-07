# Caminho do projeto (ajuste se necessário)
$rootPath = "C:\dev\MinhaEmpresaMudBlazor"

# Caminho do arquivo .env
$envFile = "$rootPath\ci-cd-templates\server-repo\infra\docker\.env.staging"

# Nome da imagem
$imageName = "restaurapp"

# Nome do container
$containerName = "restaurapp-staging"

# Ir para a raiz do projeto
Set-Location $rootPath

# Parar e remover container antigo (se existir)
docker rm -f $containerName 2>$null

# Build da imagem
docker build -t $imageName -f Restaurapp.BlazorServer/Dockerfile .

# Rodar container com env file
docker run -d `
    -p 8080:8080 `
    --env-file $envFile `
    --name $containerName `
    $imageName

Write-Host "Container '$containerName' rodando em http://localhost:8080"