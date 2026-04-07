param(
    [string]$VmName = "desenvolve-mentos",
    [string]$RunnerUser = "actions",
    [switch]$DockerOnly
)

$ErrorActionPreference = "Stop"

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "[OK]   $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-Err {
    param([string]$Message)
    Write-Host "[ERRO] $Message" -ForegroundColor Red
}

function Ask-YesNo {
    param([string]$Question)
    $answer = Read-Host "$Question (s/N)"
    return $answer -match '^(s|S|y|Y)$'
}

function Invoke-VmBash {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command
    )

    $output = & multipass exec $VmName -- bash -lc $Command 2>&1
    $exitCode = $LASTEXITCODE

    [pscustomobject]@{
        ExitCode = $exitCode
        Output   = ($output -join "`n")
    }
}

function Ensure-VmCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Description,
        [Parameter(Mandatory = $true)]
        [string]$TestCommand,
        [Parameter(Mandatory = $true)]
        [string]$FixPrompt,
        [Parameter(Mandatory = $true)]
        [string]$FixCommand
    )

    Write-Info $Description
    $test = Invoke-VmBash -Command $TestCommand
    if ($test.ExitCode -eq 0) {
        Write-Ok "$Description"
        return
    }

    Write-Warn "$Description failed."
    if ($test.Output) {
        Write-Host $test.Output
    }

    if (-not (Ask-YesNo $FixPrompt)) {
        Write-Warn "Skipping fix for this step."
        return
    }

    $fix = Invoke-VmBash -Command $FixCommand
    if ($fix.ExitCode -ne 0) {
        Write-Err "Could not fix: $Description"
        if ($fix.Output) {
            Write-Host $fix.Output
        }
        return
    }

    $retest = Invoke-VmBash -Command $TestCommand
    if ($retest.ExitCode -eq 0) {
        Write-Ok "Fixed: $Description"
    }
    else {
        Write-Err "Fix did not validate: $Description"
        if ($retest.Output) {
            Write-Host $retest.Output
        }
    }
}

function Copy-TemplateToVm {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LocalPath,
        [Parameter(Mandatory = $true)]
        [string]$VmTempPath,
        [Parameter(Mandatory = $true)]
        [string]$InstallCommand
    )

    if (-not (Test-Path $LocalPath)) {
        Write-Err "Local template not found: $LocalPath"
        return $false
    }

    & multipass transfer $LocalPath "$VmName`:$VmTempPath" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Err "Failed to transfer template to VM: $LocalPath"
        return $false
    }

    $install = Invoke-VmBash -Command $InstallCommand
    if ($install.ExitCode -ne 0) {
        Write-Err "Failed to install template in VM."
        if ($install.Output) {
            Write-Host $install.Output
        }
        return $false
    }

    return $true
}

Write-Host ""
Write-Host "=== Multipass Deploy Interactive Checker ===" -ForegroundColor Magenta
Write-Host "Target VM: $VmName | runner user: $RunnerUser"
Write-Host ""

if (-not (Get-Command multipass -ErrorAction SilentlyContinue)) {
    Write-Err "multipass not found in PATH."
    exit 1
}

Write-Info "Checking if VM exists..."
$vmInfo = & multipass info $VmName 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Err "VM '$VmName' not found."
    Write-Host $vmInfo
    exit 1
}
Write-Ok "VM found."

if (($vmInfo -join "`n") -notmatch "State:\s+Running") {
    Write-Warn "VM is not running."
    if (Ask-YesNo "Do you want to start the VM now?") {
        & multipass start $VmName | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Err "Could not start VM."
            exit 1
        }
        Write-Ok "VM started."
    }
    else {
        Write-Err "Cannot continue without a running VM."
        exit 1
    }
}
else {
    Write-Ok "VM is running."
}

$repoRoot = $PSScriptRoot
$deployScriptLocal = Join-Path $repoRoot "ci-cd-templates/server-repo/scripts/deploy-server.sh"
$deployClientScriptLocal = Join-Path $repoRoot "ci-cd-templates/client-repo/scripts/deploy-client.sh"
$rollbackScriptLocal = Join-Path $repoRoot "ci-cd-templates/server-repo/scripts/rollback-server.sh"
$systemdProdLocal = Join-Path $repoRoot "ci-cd-templates/server-repo/infra/systemd/restaurapp-server.service"
$systemdStagingLocal = Join-Path $repoRoot "ci-cd-templates/server-repo/infra/systemd/restaurapp-server-staging.service"
$nginxLocal = Join-Path $repoRoot "ci-cd-templates/server-repo/infra/nginx/restaurapp.conf"
$serverDockerProdLocal = Join-Path $repoRoot "ci-cd-templates/server-repo/infra/docker/docker-compose.production.yml"
$serverDockerStagingLocal = Join-Path $repoRoot "ci-cd-templates/server-repo/infra/docker/docker-compose.staging.yml"
$databaseDockerProdLocal = Join-Path $repoRoot "ci-cd-templates/database/docker/docker-compose.production.yml"
$databaseDockerStagingLocal = Join-Path $repoRoot "ci-cd-templates/database/docker/docker-compose.staging.yml"

Ensure-VmCommand -Description "Runner user exists ($RunnerUser)" `
    -TestCommand "id $RunnerUser >/dev/null 2>&1" `
    -FixPrompt "User $RunnerUser does not exist. Create it?" `
    -FixCommand "sudo adduser --disabled-password --gecos '' $RunnerUser && sudo usermod -aG sudo $RunnerUser"

$dirs = @(
    "/opt/restaurapp/scripts",
    "/opt/restaurapp/staging/server/releases",
    "/opt/restaurapp/production/server/releases",
    "/opt/restaurapp/docker/server",
    "/opt/restaurapp/docker/database",
    "/var/www/restaurapp/staging/client/releases",
    "/var/www/restaurapp/production/client/releases",
    "/var/www/restaurapp/uploads",
    "/var/www/restaurapp/uploads-staging"
)

foreach ($dir in $dirs) {
    Ensure-VmCommand -Description "Directory exists: $dir" `
        -TestCommand "test -d '$dir'" `
        -FixPrompt "Directory $dir does not exist. Create it?" `
        -FixCommand "sudo mkdir -p '$dir'"
}

Ensure-VmCommand -Description "Runner can write to /opt/restaurapp" `
    -TestCommand "sudo -u $RunnerUser test -w /opt/restaurapp" `
    -FixPrompt "Runner cannot write in /opt/restaurapp. Set owner to $RunnerUser?" `
    -FixCommand "sudo chown -R ${RunnerUser}:${RunnerUser} /opt/restaurapp"

Ensure-VmCommand -Description "Runner can write to /var/www/restaurapp" `
    -TestCommand "sudo -u $RunnerUser test -w /var/www/restaurapp" `
    -FixPrompt "Runner cannot write in /var/www/restaurapp. Set owner to $RunnerUser?" `
    -FixCommand "sudo chown -R ${RunnerUser}:${RunnerUser} /var/www/restaurapp"

Ensure-VmCommand -Description "Script exists and is executable: /opt/restaurapp/scripts/deploy-server.sh" `
    -TestCommand "test -x /opt/restaurapp/scripts/deploy-server.sh" `
    -FixPrompt "deploy-server.sh is missing or not executable. Copy from local template?" `
    -FixCommand "true"

if ((Invoke-VmBash -Command "test -x /opt/restaurapp/scripts/deploy-server.sh").ExitCode -ne 0) {
    if (Ask-YesNo "Copy deploy-server.sh to VM now?") {
        $ok = Copy-TemplateToVm -LocalPath $deployScriptLocal -VmTempPath "/tmp/deploy-server.sh" -InstallCommand "sed -i 's/\r$//' /tmp/deploy-server.sh && sudo install -m 755 /tmp/deploy-server.sh /opt/restaurapp/scripts/deploy-server.sh"
        if ($ok) { Write-Ok "deploy-server.sh installed." }
    }
}

Ensure-VmCommand -Description "Script exists and is executable: /opt/restaurapp/scripts/deploy-client.sh" `
    -TestCommand "test -x /opt/restaurapp/scripts/deploy-client.sh" `
    -FixPrompt "deploy-client.sh is missing or not executable. Copy from local template?" `
    -FixCommand "true"

if ((Invoke-VmBash -Command "test -x /opt/restaurapp/scripts/deploy-client.sh").ExitCode -ne 0) {
    if (Ask-YesNo "Copy deploy-client.sh to VM now?") {
        $ok = Copy-TemplateToVm -LocalPath $deployClientScriptLocal -VmTempPath "/tmp/deploy-client.sh" -InstallCommand "sed -i 's/\r$//' /tmp/deploy-client.sh && sudo install -m 755 /tmp/deploy-client.sh /opt/restaurapp/scripts/deploy-client.sh"
        if ($ok) { Write-Ok "deploy-client.sh installed." }
    }
}

Ensure-VmCommand -Description "Script exists and is executable: /opt/restaurapp/scripts/rollback-server.sh" `
    -TestCommand "test -x /opt/restaurapp/scripts/rollback-server.sh" `
    -FixPrompt "rollback-server.sh is missing or not executable. Copy from local template?" `
    -FixCommand "true"

if ((Invoke-VmBash -Command "test -x /opt/restaurapp/scripts/rollback-server.sh").ExitCode -ne 0) {
    if (Ask-YesNo "Copy rollback-server.sh to VM now?") {
        $ok = Copy-TemplateToVm -LocalPath $rollbackScriptLocal -VmTempPath "/tmp/rollback-server.sh" -InstallCommand "sed -i 's/\r$//' /tmp/rollback-server.sh && sudo install -m 755 /tmp/rollback-server.sh /opt/restaurapp/scripts/rollback-server.sh"
        if ($ok) { Write-Ok "rollback-server.sh installed." }
    }
}

Write-Info "Checking Nginx installation"
$nginxCheck = Invoke-VmBash -Command "command -v nginx >/dev/null 2>&1"
if ($nginxCheck.ExitCode -ne 0) {
    Write-Warn "por favor instalar nginx, encerrando"
    exit 1
}
Write-Ok "Nginx installed"

Write-Info "Checking Docker installation"
$dockerCheck = Invoke-VmBash -Command "command -v docker >/dev/null 2>&1"
if ($dockerCheck.ExitCode -ne 0) {
    Write-Warn "por favor instalar docker, encerrando"
    exit 1
}
Write-Ok "Docker installed"

Ensure-VmCommand -Description "Docker daemon is running" `
    -TestCommand "sudo docker info >/dev/null 2>&1" `
    -FixPrompt "Docker daemon is not available. Try enabling and starting docker service?" `
    -FixCommand "sudo systemctl enable --now docker"

Ensure-VmCommand -Description "Docker Compose plugin available" `
    -TestCommand "docker compose version >/dev/null 2>&1" `
    -FixPrompt "Docker Compose plugin not found. Skip this check for now?" `
    -FixCommand "true"

Ensure-VmCommand -Description "Runner user is in docker group" `
    -TestCommand "id -nG $RunnerUser | grep -qw docker" `
    -FixPrompt "Runner user is not in docker group. Add now?" `
    -FixCommand "sudo usermod -aG docker $RunnerUser"

Write-Warn "If docker group was changed, log out/in (or restart runner session) before running deploy workflows."

Ensure-VmCommand -Description "Nginx config exists: /etc/nginx/sites-available/restaurapp.conf" `
    -TestCommand "test -f /etc/nginx/sites-available/restaurapp.conf" `
    -FixPrompt "Nginx config not found. Copy from local template?" `
    -FixCommand "true"

if ((Invoke-VmBash -Command "test -f /etc/nginx/sites-available/restaurapp.conf").ExitCode -ne 0) {
    if (Ask-YesNo "Copy restaurapp.conf to /etc/nginx/sites-available now?") {
        $ok = Copy-TemplateToVm -LocalPath $nginxLocal -VmTempPath "/tmp/restaurapp.conf" -InstallCommand "sed -i 's/\r$//' /tmp/restaurapp.conf && sudo install -m 644 /tmp/restaurapp.conf /etc/nginx/sites-available/restaurapp.conf"
        if ($ok) { Write-Ok "restaurapp.conf installed." }
    }
}

Ensure-VmCommand -Description "Nginx config enabled (symlink)" `
    -TestCommand "test -L /etc/nginx/sites-enabled/restaurapp.conf" `
    -FixPrompt "Nginx symlink does not exist. Create it?" `
    -FixCommand "sudo ln -sfn /etc/nginx/sites-available/restaurapp.conf /etc/nginx/sites-enabled/restaurapp.conf"

Ensure-VmCommand -Description "Nginx config test" `
    -TestCommand "sudo nginx -t >/dev/null 2>&1" `
    -FixPrompt "Nginx test failed. Try test and reload now?" `
    -FixCommand "sudo nginx -t && sudo systemctl reload nginx"

Ensure-VmCommand -Description "Server docker compose exists: production" `
    -TestCommand "test -f /opt/restaurapp/docker/server/docker-compose.production.yml" `
    -FixPrompt "Server production compose is missing. Copy from local template?" `
    -FixCommand "true"

if ((Invoke-VmBash -Command "test -f /opt/restaurapp/docker/server/docker-compose.production.yml").ExitCode -ne 0) {
    if (Ask-YesNo "Copy server docker-compose.production.yml to VM now?") {
        $ok = Copy-TemplateToVm -LocalPath $serverDockerProdLocal -VmTempPath "/tmp/server-docker-compose.production.yml" -InstallCommand "sed -i 's/\r$//' /tmp/server-docker-compose.production.yml && sudo install -m 644 /tmp/server-docker-compose.production.yml /opt/restaurapp/docker/server/docker-compose.production.yml"
        if ($ok) { Write-Ok "Server production compose installed." }
    }
}

Ensure-VmCommand -Description "Server docker compose exists: staging" `
    -TestCommand "test -f /opt/restaurapp/docker/server/docker-compose.staging.yml" `
    -FixPrompt "Server staging compose is missing. Copy from local template?" `
    -FixCommand "true"

if ((Invoke-VmBash -Command "test -f /opt/restaurapp/docker/server/docker-compose.staging.yml").ExitCode -ne 0) {
    if (Ask-YesNo "Copy server docker-compose.staging.yml to VM now?") {
        $ok = Copy-TemplateToVm -LocalPath $serverDockerStagingLocal -VmTempPath "/tmp/server-docker-compose.staging.yml" -InstallCommand "sed -i 's/\r$//' /tmp/server-docker-compose.staging.yml && sudo install -m 644 /tmp/server-docker-compose.staging.yml /opt/restaurapp/docker/server/docker-compose.staging.yml"
        if ($ok) { Write-Ok "Server staging compose installed." }
    }
}

Ensure-VmCommand -Description "Database docker compose exists: production" `
    -TestCommand "test -f /opt/restaurapp/docker/database/docker-compose.production.yml" `
    -FixPrompt "Database production compose is missing. Copy from local template?" `
    -FixCommand "true"

if ((Invoke-VmBash -Command "test -f /opt/restaurapp/docker/database/docker-compose.production.yml").ExitCode -ne 0) {
    if (Ask-YesNo "Copy database docker-compose.production.yml to VM now?") {
        $ok = Copy-TemplateToVm -LocalPath $databaseDockerProdLocal -VmTempPath "/tmp/database-docker-compose.production.yml" -InstallCommand "sed -i 's/\r$//' /tmp/database-docker-compose.production.yml && sudo install -m 644 /tmp/database-docker-compose.production.yml /opt/restaurapp/docker/database/docker-compose.production.yml"
        if ($ok) { Write-Ok "Database production compose installed." }
    }
}

Ensure-VmCommand -Description "Database docker compose exists: staging" `
    -TestCommand "test -f /opt/restaurapp/docker/database/docker-compose.staging.yml" `
    -FixPrompt "Database staging compose is missing. Copy from local template?" `
    -FixCommand "true"

if ((Invoke-VmBash -Command "test -f /opt/restaurapp/docker/database/docker-compose.staging.yml").ExitCode -ne 0) {
    if (Ask-YesNo "Copy database docker-compose.staging.yml to VM now?") {
        $ok = Copy-TemplateToVm -LocalPath $databaseDockerStagingLocal -VmTempPath "/tmp/database-docker-compose.staging.yml" -InstallCommand "sed -i 's/\r$//' /tmp/database-docker-compose.staging.yml && sudo install -m 644 /tmp/database-docker-compose.staging.yml /opt/restaurapp/docker/database/docker-compose.staging.yml"
        if ($ok) { Write-Ok "Database staging compose installed." }
    }
}

Ensure-VmCommand -Description "Server compose config valid: production" `
    -TestCommand "cd /opt/restaurapp/docker/server && docker compose -f docker-compose.production.yml config >/dev/null 2>&1" `
    -FixPrompt "Server production compose config failed. Retry check now?" `
    -FixCommand "cd /opt/restaurapp/docker/server && docker compose -f docker-compose.production.yml config"

Ensure-VmCommand -Description "Server compose config valid: staging" `
    -TestCommand "cd /opt/restaurapp/docker/server && docker compose -f docker-compose.staging.yml config >/dev/null 2>&1" `
    -FixPrompt "Server staging compose config failed. Retry check now?" `
    -FixCommand "cd /opt/restaurapp/docker/server && docker compose -f docker-compose.staging.yml config"

Ensure-VmCommand -Description "Database compose config valid: production" `
    -TestCommand "cd /opt/restaurapp/docker/database && docker compose -f docker-compose.production.yml config >/dev/null 2>&1" `
    -FixPrompt "Database production compose config failed. Retry check now?" `
    -FixCommand "cd /opt/restaurapp/docker/database && docker compose -f docker-compose.production.yml config"

Ensure-VmCommand -Description "Database compose config valid: staging" `
    -TestCommand "cd /opt/restaurapp/docker/database && docker compose -f docker-compose.staging.yml config >/dev/null 2>&1" `
    -FixPrompt "Database staging compose config failed. Retry check now?" `
    -FixCommand "cd /opt/restaurapp/docker/database && docker compose -f docker-compose.staging.yml config"

Ensure-VmCommand -Description "Runner has passwordless sudo for docker/nginx checks" `
    -TestCommand "sudo -u $RunnerUser bash -lc 'sudo -n docker info >/dev/null 2>&1 && sudo -n nginx -v >/dev/null 2>&1'" `
    -FixPrompt "Runner passwordless sudo for docker/nginx is not configured. Create minimal sudoers for $RunnerUser?" `
    -FixCommand "echo '$RunnerUser ALL=(ALL) NOPASSWD:/usr/bin/docker,/usr/sbin/nginx,/usr/bin/nginx,/usr/bin/systemctl,/bin/systemctl,/usr/bin/journalctl' | sudo tee /etc/sudoers.d/$RunnerUser-deploy >/dev/null && sudo chmod 440 /etc/sudoers.d/$RunnerUser-deploy && sudo visudo -cf /etc/sudoers.d/$RunnerUser-deploy >/dev/null"

if (-not $DockerOnly) {
    Write-Info "DockerOnly not enabled: running legacy checks for dotnet/systemd as well."

    Write-Info "Checking dotnet installation"
    $dotnetCheck = Invoke-VmBash -Command "command -v dotnet >/dev/null 2>&1"
    if ($dotnetCheck.ExitCode -ne 0) {
        Write-Warn "por favor instalar dotnet runtime, encerrando"
        exit 1
    }
    Write-Ok "dotnet found in PATH"

    Ensure-VmCommand -Description "Service file exists: restaurapp-server.service" `
        -TestCommand "test -f /etc/systemd/system/restaurapp-server.service" `
        -FixPrompt "Production service file is missing. Copy from local template?" `
        -FixCommand "true"

    if ((Invoke-VmBash -Command "test -f /etc/systemd/system/restaurapp-server.service").ExitCode -ne 0) {
        if (Ask-YesNo "Copy restaurapp-server.service to /etc/systemd/system now?") {
            $ok = Copy-TemplateToVm -LocalPath $systemdProdLocal -VmTempPath "/tmp/restaurapp-server.service" -InstallCommand "sed -i 's/\r$//' /tmp/restaurapp-server.service && sed -i 's/^User=.*/User=$RunnerUser/' /tmp/restaurapp-server.service && sudo install -m 644 /tmp/restaurapp-server.service /etc/systemd/system/restaurapp-server.service"
            if ($ok) { Write-Ok "Production service installed." }
        }
    }

    Ensure-VmCommand -Description "Service file exists: restaurapp-server-staging.service" `
        -TestCommand "test -f /etc/systemd/system/restaurapp-server-staging.service" `
        -FixPrompt "Staging service file is missing. Copy from local template?" `
        -FixCommand "true"

    if ((Invoke-VmBash -Command "test -f /etc/systemd/system/restaurapp-server-staging.service").ExitCode -ne 0) {
        if (Ask-YesNo "Copy restaurapp-server-staging.service to /etc/systemd/system now?") {
            $ok = Copy-TemplateToVm -LocalPath $systemdStagingLocal -VmTempPath "/tmp/restaurapp-server-staging.service" -InstallCommand "sed -i 's/\r$//' /tmp/restaurapp-server-staging.service && sed -i 's/^User=.*/User=$RunnerUser/' /tmp/restaurapp-server-staging.service && sudo install -m 644 /tmp/restaurapp-server-staging.service /etc/systemd/system/restaurapp-server-staging.service"
            if ($ok) { Write-Ok "Staging service installed." }
        }
    }

    Ensure-VmCommand -Description "Systemd daemon-reload" `
        -TestCommand "sudo systemctl daemon-reload" `
        -FixPrompt "daemon-reload failed. Try again?" `
        -FixCommand "sudo systemctl daemon-reload"

    $services = @("restaurapp-server", "restaurapp-server-staging")
    foreach ($svc in $services) {
        Write-Info "Service enabled check (no auto-enable): $svc"
        $enabledCheck = Invoke-VmBash -Command "systemctl is-enabled $svc >/dev/null 2>&1"
        if ($enabledCheck.ExitCode -eq 0) {
            Write-Ok "Service enabled: $svc"
        }
        else {
            Write-Warn "Service $svc is disabled. Skipping enable by design."
        }
    }

    Ensure-VmCommand -Description "Runner has passwordless sudo for deploy commands" `
        -TestCommand "sudo -u $RunnerUser bash -lc 'sudo -n systemctl --version >/dev/null 2>&1 && sudo -n nginx -v >/dev/null 2>&1'" `
        -FixPrompt "Runner passwordless sudo is not configured. Create minimal sudoers for $RunnerUser?" `
        -FixCommand "echo '$RunnerUser ALL=(ALL) NOPASSWD:/usr/bin/systemctl,/bin/systemctl,/usr/sbin/nginx,/usr/bin/nginx,/usr/bin/journalctl' | sudo tee /etc/sudoers.d/$RunnerUser-deploy >/dev/null && sudo chmod 440 /etc/sudoers.d/$RunnerUser-deploy && sudo visudo -cf /etc/sudoers.d/$RunnerUser-deploy >/dev/null"
}
else {
    Write-Warn "DockerOnly enabled: skipping legacy checks for dotnet/systemd services."
}

Write-Info "Checking GitHub runner service status..."
$runnerUnits = Invoke-VmBash -Command "systemctl list-units --all 'actions.runner*' --no-legend"
if ($runnerUnits.ExitCode -eq 0 -and $runnerUnits.Output.Trim().Length -gt 0) {
    Write-Ok "Runner units detected:"
    Write-Host $runnerUnits.Output
}
else {
    Write-Warn "No systemd runner unit found."
    Write-Warn "If you run with ./run.sh manually, this is expected."
}

Write-Host ""
Write-Host "=== Check complete ===" -ForegroundColor Magenta
Write-Host "Review warnings above. If all is OK, run workflow_dispatch for staging." -ForegroundColor Cyan
