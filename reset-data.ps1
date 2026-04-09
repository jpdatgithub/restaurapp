Invoke-RestMethod `
    -Method Post `
    -Uri "http://localhost:5197/api/admin/maintenance/reset-data?confirm=true" `
    -Headers @{ "X-Admin-Reset-Secret" = "dev-reset-secret-change-me" }