function Get-RepoRoot {
    $root = Split-Path -Parent $PSScriptRoot
    return (Resolve-Path $root).Path
}

function Read-RailwayPostgresEnv {
    param([string]$RepoRoot)

    $path = Join-Path $RepoRoot "railway-postgres.env"
    if (-not (Test-Path $path)) {
        throw "Missing railway-postgres.env in repo root."
    }

    $values = @{}
    Get-Content $path | ForEach-Object {
        $line = $_.Trim()
        if ($line -and -not $line.StartsWith("#") -and $line -match "^([^=]+)=(.*)$") {
            $values[$matches[1]] = $matches[2]
        }
    }

    foreach ($key in @("POSTGRES_USER", "POSTGRES_PASSWORD", "POSTGRES_DB")) {
        if (-not $values.ContainsKey($key) -or [string]::IsNullOrWhiteSpace($values[$key])) {
            throw "railway-postgres.env is missing $key"
        }
    }

    return $values
}

function Ensure-RailwayCli {
    if (Get-Command railway -ErrorAction SilentlyContinue) {
        return
    }

    Write-Host "Railway CLI not found - installing via npm..." -ForegroundColor Cyan

    if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
        throw @"
Node.js/npm not found. Install Node.js 16+ from https://nodejs.org
Then run this script again.
"@
    }

    npm i -g @railway/cli
    if ($LASTEXITCODE -ne 0) {
        throw "npm i -g @railway/cli failed."
    }

    if (-not (Get-Command railway -ErrorAction SilentlyContinue)) {
        throw "railway installed but not on PATH. Restart PowerShell and retry."
    }
}

function Ensure-SshKey {
    $sshDir = Join-Path $env:USERPROFILE ".ssh"
    $privateKey = Join-Path $sshDir "id_ed25519"

    if (-not (Test-Path $privateKey)) {
        Write-Host "No SSH key - creating ed25519..." -ForegroundColor Cyan
        New-Item -ItemType Directory -Force -Path $sshDir | Out-Null
        ssh-keygen -t ed25519 -f $privateKey -N '""' -C "$env:USERNAME@glowbook-railway" | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "ssh-keygen failed."
        }
    }
}

function Ensure-RailwayAuth {
    railway whoami 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) {
        return
    }

    Write-Host "Railway login (browser opens once per machine)..." -ForegroundColor Cyan
    railway login
    if ($LASTEXITCODE -ne 0) {
        throw "railway login failed."
    }
}

function Get-RailwayLinkConfig {
    param([string]$RepoRoot)

    $path = Join-Path $RepoRoot "scripts/railway-link.json"
    if (-not (Test-Path $path)) {
        throw "Missing scripts/railway-link.json"
    }

    return Get-Content $path -Raw | ConvertFrom-Json
}

function Test-RailwayLinked {
    param([string]$RepoRoot)

    $configPath = Join-Path $env:USERPROFILE ".railway\config.json"
    if (-not (Test-Path $configPath)) {
        return $false
    }

    $linkedRoot = (Resolve-Path $RepoRoot).Path
    $config = Get-Content $configPath -Raw | ConvertFrom-Json
    return $null -ne $config.projects.$linkedRoot
}

function Ensure-RailwayLink {
    param([string]$RepoRoot)

    if (Test-RailwayLinked -RepoRoot $RepoRoot) {
        return
    }

    $link = Get-RailwayLinkConfig -RepoRoot $RepoRoot
    Write-Host "Linking repo to Railway project..." -ForegroundColor Cyan

    Push-Location $RepoRoot
    try {
        railway link -p $link.projectId -s $link.serviceId -e $link.environmentId
        if ($LASTEXITCODE -ne 0) {
            throw "railway link failed."
        }
    }
    finally {
        Pop-Location
    }
}

function Ensure-RailwaySshKeyRegistered {
    railway ssh keys add 2>&1 | Out-String | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "railway ssh keys add failed."
    }
}

function Find-FreeTcpPort {
    param(
        [int]$StartPort = 5432,
        [int]$EndPort = 5450
    )

    for ($port = $StartPort; $port -le $EndPort; $port++) {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $port)
        try {
            $listener.Start()
            $listener.Stop()
            return $port
        }
        catch {
            continue
        }
    }

    throw "No free TCP port in range $StartPort-$EndPort."
}

function Wait-ForTcpPort {
    param(
        [int]$Port,
        [int]$TimeoutSec = 45
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        $client = $null
        try {
            $client = [System.Net.Sockets.TcpClient]::new()
            $task = $client.ConnectAsync("127.0.0.1", $Port)
            if ($task.Wait(500) -and $client.Connected) {
                return
            }
        }
        catch {
        }
        finally {
            if ($client) { $client.Dispose() }
        }

        Start-Sleep -Milliseconds 400
    }

    throw "Tunnel did not open on 127.0.0.1:$Port within ${TimeoutSec}s."
}

function Get-RailwayProcessStart {
    param([string[]]$RailwayArgs)

    $candidates = @(
        (Join-Path $env:APPDATA "npm/node_modules/@railway/cli/bin/railway.js"),
        (Join-Path $env:ProgramFiles "nodejs/node_modules/@railway/cli/bin/railway.js")
    )

    foreach ($railwayJs in $candidates) {
        if (Test-Path $railwayJs) {
            return @{
                FilePath = "node"
                ArgumentList = @($railwayJs) + $RailwayArgs
            }
        }
    }

    throw "Railway CLI JS entry not found. Run: npm i -g @railway/cli"
}

function Stop-StalePostgresTunnel {
    param([string]$RepoRoot)

    $pidFile = Join-Path $RepoRoot "scripts/.postgres-tunnel.pid"
    if (-not (Test-Path $pidFile)) {
        return
    }

    $raw = Get-Content $pidFile -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($raw -match "^\d+$") {
        $oldPid = [int]$raw
        $proc = Get-Process -Id $oldPid -ErrorAction SilentlyContinue
        if ($proc -and -not $proc.HasExited) {
            Write-Host "Stopping previous tunnel (PID $oldPid)..." -ForegroundColor Yellow
            Stop-Process -Id $oldPid -Force -ErrorAction SilentlyContinue
            Start-Sleep -Milliseconds 400
        }
    }

    Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
}

function Start-RailwayPostgresTunnel {
    param(
        [string]$RepoRoot,
        [int]$LocalPort = 0,
        [string]$ServiceName
    )

    if ($LocalPort -le 0) {
        $LocalPort = Find-FreeTcpPort
    }
    else {
        try {
            $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $LocalPort)
            $listener.Start()
            $listener.Stop()
        }
        catch {
            Write-Host "Port $LocalPort is busy - finding another..." -ForegroundColor Yellow
            $LocalPort = Find-FreeTcpPort -StartPort ($LocalPort + 1)
        }
    }

    Stop-StalePostgresTunnel -RepoRoot $RepoRoot

    $runId = Get-Date -Format "yyyyMMdd-HHmmss-fff"
    $logPath = Join-Path $RepoRoot "scripts/.postgres-tunnel-$runId.log"
    $errPath = Join-Path $RepoRoot "scripts/.postgres-tunnel-$runId.err"
    $pidFile = Join-Path $RepoRoot "scripts/.postgres-tunnel.pid"

    $start = Get-RailwayProcessStart -RailwayArgs @("connect", $ServiceName, "--tunnel-only", "-P", "$LocalPort")

    $proc = Start-Process `
        -FilePath $start.FilePath `
        -ArgumentList $start.ArgumentList `
        -WorkingDirectory $RepoRoot `
        -RedirectStandardOutput $logPath `
        -RedirectStandardError $errPath `
        -PassThru `
        -WindowStyle Hidden

    try {
        Wait-ForTcpPort -Port $LocalPort
    }
    catch {
        if (-not $proc.HasExited) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        }

        $tail = @()
        if (Test-Path $logPath) { $tail += Get-Content $logPath -Tail 10 -ErrorAction SilentlyContinue }
        if (Test-Path $errPath) { $tail += Get-Content $errPath -Tail 10 -ErrorAction SilentlyContinue }
        $tailText = ($tail | Out-String)
        throw "Failed to open SSH tunnel.`n$tailText"
    }

    Set-Content -Path $pidFile -Value $proc.Id -NoNewline

    return [PSCustomObject]@{
        Process  = $proc
        Port     = $LocalPort
        LogPath  = $logPath
        PidFile  = $pidFile
    }
}

function Set-PostgresTunnelEnvironment {
    param(
        [hashtable]$PostgresEnv,
        [int]$Port
    )

    $user = $PostgresEnv.POSTGRES_USER
    $password = $PostgresEnv.POSTGRES_PASSWORD
    $database = $PostgresEnv.POSTGRES_DB

    $encodedPassword = [uri]::EscapeDataString($password)
    $url = "postgresql://${user}:${encodedPassword}@127.0.0.1:${Port}/${database}"

    $env:DATABASE_URL = $url
    $env:PGHOST = "127.0.0.1"
    $env:PGPORT = "$Port"
    $env:PGUSER = $user
    $env:PGPASSWORD = $password
    $env:PGDATABASE = $database

    return $url
}

function Initialize-RailwayDevEnvironment {
    param([string]$RepoRoot)

    Ensure-RailwayCli
    Ensure-SshKey
    Ensure-RailwayAuth
    Ensure-RailwayLink -RepoRoot $RepoRoot
    Ensure-RailwaySshKeyRegistered

    $link = Get-RailwayLinkConfig -RepoRoot $RepoRoot
    $postgres = Read-RailwayPostgresEnv -RepoRoot $RepoRoot

    return [PSCustomObject]@{
        Link     = $link
        Postgres = $postgres
    }
}
