param(
    [string]$ServerDockerDir = "ci-cd-templates/server-repo/infra/docker",
    [string]$DatabaseDockerDir = "ci-cd-templates/database/docker",
    [switch]$RotateDatabasePasswords,
    [switch]$ShowGeneratedValues
)

$ErrorActionPreference = "Stop"

function Get-CryptoRandomBytes {
    param([int]$NumBytes)

    $bytes = New-Object byte[] $NumBytes
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
    }
    finally {
        if ($null -ne $rng) {
            $rng.Dispose()
        }
    }

    return $bytes
}

function New-Base64Secret {
    param([int]$NumBytes = 64)

    $bytes = Get-CryptoRandomBytes -NumBytes $NumBytes
    return [Convert]::ToBase64String($bytes)
}

function New-UrlSafeSecret {
    param([int]$NumBytes = 48)

    $bytes = Get-CryptoRandomBytes -NumBytes $NumBytes
    return ([Convert]::ToBase64String($bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function New-Password {
    param([int]$NumBytes = 24)

    $bytes = Get-CryptoRandomBytes -NumBytes $NumBytes
    return ([Convert]::ToBase64String($bytes)).TrimEnd('=').Replace('+', 'A').Replace('/', 'b')
}

function Initialize-EnvFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ExamplePath
    )

    if (-not (Test-Path -Path $Path)) {
        if (-not (Test-Path -Path $ExamplePath)) {
            throw "Arquivo de exemplo nao encontrado: $ExamplePath"
        }

        Copy-Item -Path $ExamplePath -Destination $Path
        Write-Host "Criado: $Path" -ForegroundColor Yellow
    }
}

function Set-OrAddEnvVar {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Key,
        [Parameter(Mandatory = $true)][string]$Value
    )

    if (-not (Test-Path -Path $Path)) {
        throw "Arquivo nao encontrado: $Path"
    }

    $content = Get-Content -Path $Path -Raw -Encoding UTF8
    $escapedKey = [Regex]::Escape($Key)
    $pattern = "(?m)^$escapedKey=.*$"
    $replacement = "$Key=$Value"

    if ($content -match $pattern) {
        $content = [Regex]::Replace($content, $pattern, [System.Text.RegularExpressions.MatchEvaluator] { param($m) $replacement })
    }
    else {
        if ($content.Length -gt 0 -and -not $content.EndsWith("`r`n")) {
            $content += "`r`n"
        }

        $content += "$replacement`r`n"
    }

    Set-Content -Path $Path -Value $content -Encoding UTF8
}

function Get-EnvVarValue {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Key
    )

    $escapedKey = [Regex]::Escape($Key)
    $line = Select-String -Path $Path -Pattern "^$escapedKey=" -SimpleMatch:$false | Select-Object -First 1
    if (-not $line) {
        return $null
    }

    return ($line.Line -replace "^$escapedKey=", "")
}

function Update-ConnectionStringPassword {
    param(
        [Parameter(Mandatory = $true)][string]$ConnectionString,
        [Parameter(Mandatory = $true)][string]$ReplacementValue
    )

    if ($ConnectionString -match "(?i)(^|;)Password=") {
        return [Regex]::Replace($ConnectionString, "(?i)(^|;)Password=[^;]*", "`$1Password=$ReplacementValue")
    }

    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        return "Password=$ReplacementValue"
    }

    return "$ConnectionString;Password=$ReplacementValue"
}

function Format-SecretPreview {
    param([string]$Value)

    if ([string]::IsNullOrEmpty($Value)) {
        return "(vazio)"
    }

    if ($Value.Length -le 10) {
        return "**********"
    }

    return ($Value.Substring(0, 6) + "..." + $Value.Substring($Value.Length - 4))
}

$serverProdEnv = Join-Path $ServerDockerDir ".env.production"
$serverStgEnv = Join-Path $ServerDockerDir ".env.staging"
$serverProdExample = Join-Path $ServerDockerDir ".env.production.example"
$serverStgExample = Join-Path $ServerDockerDir ".env.staging.example"

$dbProdEnv = Join-Path $DatabaseDockerDir ".env.production"
$dbStgEnv = Join-Path $DatabaseDockerDir ".env.staging"
$dbProdExample = Join-Path $DatabaseDockerDir ".env.production.example"
$dbStgExample = Join-Path $DatabaseDockerDir ".env.staging.example"

Initialize-EnvFile -Path $serverProdEnv -ExamplePath $serverProdExample
Initialize-EnvFile -Path $serverStgEnv -ExamplePath $serverStgExample

$jwtProd = New-Base64Secret -NumBytes 64
$jwtStg = New-Base64Secret -NumBytes 64
$provisionProd = New-UrlSafeSecret -NumBytes 48
$provisionStg = New-UrlSafeSecret -NumBytes 48

Set-OrAddEnvVar -Path $serverProdEnv -Key "Jwt__Key" -Value $jwtProd
Set-OrAddEnvVar -Path $serverProdEnv -Key "PROVISIONING_SECRET" -Value $provisionProd
Set-OrAddEnvVar -Path $serverStgEnv -Key "Jwt__Key" -Value $jwtStg
Set-OrAddEnvVar -Path $serverStgEnv -Key "PROVISIONING_SECRET" -Value $provisionStg

if ($RotateDatabasePasswords) {
    Initialize-EnvFile -Path $dbProdEnv -ExamplePath $dbProdExample
    Initialize-EnvFile -Path $dbStgEnv -ExamplePath $dbStgExample

    $dbPasswordProd = New-Password -NumBytes 24
    $dbPasswordStg = New-Password -NumBytes 24

    $prodConn = Get-EnvVarValue -Path $serverProdEnv -Key "ConnectionStrings__DefaultConnection"
    $stgConn = Get-EnvVarValue -Path $serverStgEnv -Key "ConnectionStrings__DefaultConnection"

    if ([string]::IsNullOrWhiteSpace($prodConn)) {
        throw "ConnectionStrings__DefaultConnection ausente em $serverProdEnv"
    }

    if ([string]::IsNullOrWhiteSpace($stgConn)) {
        throw "ConnectionStrings__DefaultConnection ausente em $serverStgEnv"
    }

    $newProdConn = Update-ConnectionStringPassword -ConnectionString $prodConn -ReplacementValue $dbPasswordProd
    $newStgConn = Update-ConnectionStringPassword -ConnectionString $stgConn -ReplacementValue $dbPasswordStg

    Set-OrAddEnvVar -Path $serverProdEnv -Key "ConnectionStrings__DefaultConnection" -Value $newProdConn
    Set-OrAddEnvVar -Path $serverStgEnv -Key "ConnectionStrings__DefaultConnection" -Value $newStgConn

    Set-OrAddEnvVar -Path $dbProdEnv -Key "POSTGRES_PASSWORD" -Value $dbPasswordProd
    Set-OrAddEnvVar -Path $dbStgEnv -Key "POSTGRES_PASSWORD" -Value $dbPasswordStg

    Write-Host "Senha de banco rotacionada em server/db de producao e staging." -ForegroundColor Green

    if ($ShowGeneratedValues) {
        Write-Host "POSTGRES_PASSWORD (prod): $dbPasswordProd" -ForegroundColor Cyan
        Write-Host "POSTGRES_PASSWORD (staging): $dbPasswordStg" -ForegroundColor Cyan
    }
    else {
        Write-Host "POSTGRES_PASSWORD (prod): $(Format-SecretPreview $dbPasswordProd)" -ForegroundColor DarkCyan
        Write-Host "POSTGRES_PASSWORD (staging): $(Format-SecretPreview $dbPasswordStg)" -ForegroundColor DarkCyan
    }
}

Write-Host "Segredos de JWT e Provisioning atualizados." -ForegroundColor Green

if ($ShowGeneratedValues) {
    Write-Host "Jwt__Key (prod): $jwtProd" -ForegroundColor Cyan
    Write-Host "PROVISIONING_SECRET (prod): $provisionProd" -ForegroundColor Cyan
    Write-Host "Jwt__Key (staging): $jwtStg" -ForegroundColor Cyan
    Write-Host "PROVISIONING_SECRET (staging): $provisionStg" -ForegroundColor Cyan
}
else {
    Write-Host "Jwt__Key (prod): $(Format-SecretPreview $jwtProd)" -ForegroundColor DarkCyan
    Write-Host "PROVISIONING_SECRET (prod): $(Format-SecretPreview $provisionProd)" -ForegroundColor DarkCyan
    Write-Host "Jwt__Key (staging): $(Format-SecretPreview $jwtStg)" -ForegroundColor DarkCyan
    Write-Host "PROVISIONING_SECRET (staging): $(Format-SecretPreview $provisionStg)" -ForegroundColor DarkCyan
}

Write-Host "Concluido." -ForegroundColor Green
