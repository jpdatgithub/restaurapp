# API de Provisioning

## Visão Geral

O sistema utiliza uma Provisioning Secret configurada para gerar magic links de registro de forma segura.

## Configuração

### Provisioning Secret

A secret pode ser configurada de duas formas:

1. **Via appsettings.json:**

```json
{
  "Provisioning": {
    "Secret": "sua-secret-segura-aqui"
  }
}
```

2. **Via variável de ambiente (recomendado para produção):**

```bash
# Windows
$env:PROVISIONING_SECRET="sua-secret-segura-aqui"

# Linux/Mac
export PROVISIONING_SECRET="sua-secret-segura-aqui"
```

⚠️ **IMPORTANTE**: Altere a secret padrão em produção!

## Como Usar

### Gerar Magic Link de Registro

Use a Provisioning Secret para gerar magic links de registro para novos usuários.

**Endpoint:** `POST /api/provisioning/generate-magic-link`

**Requisição:**

```json
{
  "provisioningSecret": "sua-secret-configurada",
  "durationInMinutes": 60
}
```

**Parâmetros:**

- `provisioningSecret` (obrigatório): A secret configurada no sistema
- `durationInMinutes` (opcional): Duração de validade do link em minutos (padrão: 60)

**Resposta:**

```json
{
  "magicLink": "https://seu-dominio.com/register?token=xxxxxxxxxx",
  "expiresAt": "2026-02-12T11:30:00Z"
}
```

## Exemplo de Uso com curl

```bash
curl -X POST https://seu-dominio.com/api/provisioning/generate-magic-link \
  -H "Content-Type: application/json" \
  -d '{
    "provisioningSecret": "sua-secret-configurada",
    "durationInMinutes": 120
  }'
```

## Segurança

- A Provisioning Secret deve ser mantida em segredo absoluto
- Em produção, use variáveis de ambiente ao invés de appsettings.json
- O sistema registra tentativas de acesso com secret inválida
- Altere a secret padrão imediatamente após o deploy

## Exemplo de Automação

Você pode integrar este endpoint em seus scripts de automação, CI/CD, ou ferramentas de gerenciamento de usuários:

```powershell
# PowerShell
$secret = $env:PROVISIONING_SECRET
$response = Invoke-RestMethod -Uri "https://api.seudominio.com/api/provisioning/generate-magic-link" `
    -Method Post `
    -Body (@{
        provisioningSecret = $secret
        durationInMinutes = 60
    } | ConvertTo-Json) `
    -ContentType "application/json"

Write-Host "Magic Link: $($response.magicLink)"
```

```bash
# Bash
SECRET=$PROVISIONING_SECRET
curl -X POST https://api.seudominio.com/api/provisioning/generate-magic-link \
  -H "Content-Type: application/json" \
  -d "{
    \"provisioningSecret\": \"$SECRET\",
    \"durationInMinutes\": 60
  }"
```
