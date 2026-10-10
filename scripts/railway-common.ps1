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

function Get-RailwayExecutable {
    $cmd = Get-Command railway -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source -and ($cmd.Source -like "*.exe")) {
        return $cmd.Source
    }

    $candidates = @(
        (Join-Path $env:APPDATA "npm/node_modules/@railway/cli/bin/railway.exe"),
        (Join-Path $env:LOCALAPPDATA "npm/node_modules/@railway/cli/bin/railway.exe"),
        (Join-Path $env:ProgramFiles "nodejs/node_modules/@railway/cli/bin/railway.exe")
    )

    foreach ($path in $candidates) {
        if (Test-Path $path) {
            return $path
        }
    }

    $jsCandidates = @(
        (Join-Path $env:APPDATA "npm/node_modules/@railway/cli/bin/railway.js"),
        (Join-Path $env:ProgramFiles "nodejs/node_modules/@railway/cli/bin/railway.js")
    )

    foreach ($railwayJs in $jsCandidates) {
        if (Test-Path $railwayJs) {
            return @{ FilePath = "node"; ArgumentListPrefix = @($railwayJs) }
        }
    }

    throw "Railway CLI not found. Run: npm i -g @railway/cli"
}

function Get-RailwayStartInfo {
    param([string[]]$RailwayArgs)

    $exe = Get-RailwayExecutable
    if ($exe -is [hashtable]) {
        return @{
            FilePath     = $exe.FilePath
            ArgumentList = @($exe.ArgumentListPrefix) + $RailwayArgs
        }
    }

    return @{
        FilePath     = $exe
        ArgumentList = $RailwayArgs
    }
}

function Invoke-Railway {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$RailwayArgs,

        [string]$WorkingDirectory = "",

        [switch]$PassThru,

        [switch]$Interactive
    )

    $start = Get-RailwayStartInfo -RailwayArgs $RailwayArgs

    if ($WorkingDirectory) {
        Push-Location $WorkingDirectory
    }

    # Native CLIs (railway/ssh) write normal progress to stderr. With the caller's
    # $ErrorActionPreference=Stop that becomes a terminating RemoteException.
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        if ($PassThru) {
            $output = & $start.FilePath @($start.ArgumentList) 2>&1 | ForEach-Object { "$_" }
            return [PSCustomObject]@{
                ExitCode = $LASTEXITCODE
                Output   = ($output -join "`n")
            }
        }

        if ($Interactive) {
            & $start.FilePath @($start.ArgumentList) | Out-Host
            return $LASTEXITCODE
        }

        & $start.FilePath @($start.ArgumentList) 2>&1 | ForEach-Object { Write-Host $_ }
        return $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $prevEap
        if ($WorkingDirectory) {
            Pop-Location
        }
    }
}

function Ensure-OpenSshClient {
    if (Get-Command ssh -ErrorAction SilentlyContinue) {
        return
    }

    throw @"
OpenSSH client (ssh) not found.
Install it, then retry:
  Settings -> Apps -> Optional features -> OpenSSH Client
Or in admin PowerShell:
  Add-WindowsCapability -Online -Name OpenSSH.Client~~~~0.0.1.0
"@
}

function Get-GlowBookSshKeyPaths {
    $sshDir = Join-Path $env:USERPROFILE ".ssh"
    return [PSCustomObject]@{
        Dir        = $sshDir
        PrivateKey = Join-Path $sshDir "id_ed25519"
        PublicKey  = Join-Path $sshDir "id_ed25519.pub"
    }
}

function Ensure-SshKey {
    Ensure-OpenSshClient

    $keys = Get-GlowBookSshKeyPaths
    if (Test-Path $keys.PrivateKey) {
        return $keys
    }

    Write-Host "No SSH key - creating ed25519..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $keys.Dir | Out-Null
    $comment = "$env:USERNAME@$env:COMPUTERNAME-glowbook-railway"
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & ssh-keygen -t ed25519 -f $keys.PrivateKey -N '""' -C $comment 2>&1 | Out-Null
    }
    finally {
        $ErrorActionPreference = $prevEap
    }
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $keys.PrivateKey)) {
        throw "ssh-keygen failed."
    }

    return $keys
}

function Ensure-RailwaySshKnownHost {
    # Do not depend on ssh-keyscan under PowerShell: its stderr banners become
    # terminating errors when $ErrorActionPreference=Stop, and -q often yields empty stdout.
    # Pin Railway's current ssh.railway.com host key; first connect also uses accept-new.
    $keys = Get-GlowBookSshKeyPaths
    $knownHosts = Join-Path $keys.Dir "known_hosts"
    New-Item -ItemType Directory -Force -Path $keys.Dir | Out-Null

    if ((Test-Path $knownHosts) -and (Select-String -Path $knownHosts -Pattern "(^|[,\s])ssh\.railway\.com([,\s]|$)" -Quiet)) {
        return
    }

    Write-Host "Adding ssh.railway.com to known_hosts..." -ForegroundColor Cyan

    $pinned = @(
        "ssh.railway.com ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIJ8X3z81/tuP7CvmK3ZqWgwEvHUR6b04oi2lQJGld2C1"
    )

    # Avoid UTF-8 BOM: OpenSSH rejects a BOM at the start of known_hosts.
    $text = ($pinned -join "`n") + "`n"
    [System.IO.File]::AppendAllText($knownHosts, $text, [System.Text.UTF8Encoding]::new($false))
}

function Ensure-RailwayAuth {
    $who = Invoke-Railway -RailwayArgs @("whoami") -PassThru
    if ($who.ExitCode -eq 0) {
        return
    }

    Write-Host "Railway login (browser opens once per machine)..." -ForegroundColor Cyan
    $code = Invoke-Railway -RailwayArgs @("login") -Interactive
    if ($code -ne 0) {
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
    $projects = $config.projects
    if (-not $projects) {
        return $false
    }

    foreach ($prop in $projects.PSObject.Properties) {
        if ([string]::Equals($prop.Name, $linkedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Ensure-RailwayLink {
    param([string]$RepoRoot)

    if (Test-RailwayLinked -RepoRoot $RepoRoot) {
        return
    }

    $link = Get-RailwayLinkConfig -RepoRoot $RepoRoot
    Write-Host "Linking repo to Railway project..." -ForegroundColor Cyan

    $code = Invoke-Railway -WorkingDirectory $RepoRoot -RailwayArgs @(
        "link",
        "-p", $link.projectId,
        "-s", $link.serviceId,
        "-e", $link.environmentId
    )
    if ($code -ne 0) {
        throw "railway link failed."
    }
}

function Test-RailwaySshKeyListed {
    param(
        [string]$ListOutput,
        [string]$PublicKeyPath
    )

    $fingerprint = (& ssh-keygen -lf $PublicKeyPath 2>$null | Out-String).Trim()
    if ($fingerprint -match "SHA256:([A-Za-z0-9+/=]+)") {
        if ($ListOutput -match [regex]::Escape("SHA256:$($matches[1])")) {
            return $true
        }
    }

    $pubText = (Get-Content $PublicKeyPath -Raw).Trim()
    if ($pubText -match "\s(\S+)$") {
        $comment = $matches[1]
        if ($comment -and ($ListOutput -match [regex]::Escape($comment))) {
            return $true
        }
    }

    return $false
}

function Ensure-RailwaySshKeyRegistered {
    param($SshKeys)

    if (-not $SshKeys) {
        $SshKeys = Ensure-SshKey
    }

    $pub = $SshKeys.PublicKey
    if (-not (Test-Path $pub)) {
        throw "Missing public key: $pub"
    }

    $listed = Invoke-Railway -RailwayArgs @("ssh", "keys", "list") -PassThru
    if ($listed.ExitCode -eq 0 -and (Test-RailwaySshKeyListed -ListOutput $listed.Output -PublicKeyPath $pub)) {
        return
    }

    $name = "$env:USERNAME@$env:COMPUTERNAME-glowbook"
    Write-Host "Registering SSH key with Railway ($name)..." -ForegroundColor Cyan

    $added = Invoke-Railway -RailwayArgs @(
        "ssh", "keys", "add",
        "--key", $pub,
        "--name", $name
    ) -PassThru

    if ($added.ExitCode -eq 0) {
        return
    }

    $retry = Invoke-Railway -RailwayArgs @("ssh", "keys", "list") -PassThru
    if ($retry.ExitCode -eq 0 -and (Test-RailwaySshKeyListed -ListOutput $retry.Output -PublicKeyPath $pub)) {
        return
    }

    throw "railway ssh keys add failed.`n$($added.Output)"
}

function Ensure-RailwaySshConfig {
    param(
        [string]$RepoRoot,
        [string]$ServiceName,
        [string]$PrivateKeyPath,
        [string]$Alias = "glowbook-postgres"
    )

    Write-Host "Writing OpenSSH config host '$Alias'..." -ForegroundColor Cyan

    $code = Invoke-Railway -WorkingDirectory $RepoRoot -RailwayArgs @(
        "ssh", "config",
        "-s", $ServiceName,
        "--alias", $Alias,
        "-i", $PrivateKeyPath
    )
    if ($code -ne 0) {
        throw "railway ssh config failed for service '$ServiceName'."
    }

    return $Alias
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
        [int]$TimeoutSec = 60,
        [System.Diagnostics.Process]$Process = $null
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if ($Process -and $Process.HasExited) {
            throw "Tunnel process exited early (code $($Process.ExitCode))."
        }

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

function Get-NodeExecutable {
    $cmd = Get-Command node -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source) {
        return $cmd.Source
    }

    throw @"
Node.js (node) not found on PATH.
Install Node.js 16+ from https://nodejs.org and retry.
"@
}

function Get-RailwaySshProxyCommand {
    param([string]$RepoRoot)

    $null = Get-NodeExecutable
    $shim = Join-Path $RepoRoot "scripts\railway-ssh-proxy.cmd"
    $proxy = Join-Path $RepoRoot "scripts\railway-ssh-proxy.mjs"
    if (-not (Test-Path $shim) -or -not (Test-Path $proxy)) {
        throw "Missing SSH proxy helper scripts in scripts/"
    }

    # .cmd shim + defaults in .mjs (ssh.railway.com:22). Do not append host/port
    # here: Start-Process joins ArgumentList with spaces and breaks -o values that
    # contain spaces, which turns "-L ..." into a remote command.
    return (($shim -replace '\\', '/'))
}

function Start-RailwayPostgresTunnel {
    param(
        [string]$RepoRoot,
        [int]$LocalPort = 0,
        [string]$ServiceName,
        [int]$RemotePort = 5432
    )

    Ensure-OpenSshClient
    $null = Get-NodeExecutable
    $sshKeys = Ensure-SshKey
    Ensure-RailwaySshKnownHost

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

    $alias = Ensure-RailwaySshConfig `
        -RepoRoot $RepoRoot `
        -ServiceName $ServiceName `
        -PrivateKeyPath $sshKeys.PrivateKey

    $runId = Get-Date -Format "yyyyMMdd-HHmmss-fff"
    $logPath = Join-Path $RepoRoot "scripts/.postgres-tunnel-$runId.log"
    $errPath = Join-Path $RepoRoot "scripts/.postgres-tunnel-$runId.err"
    $pidFile = Join-Path $RepoRoot "scripts/.postgres-tunnel.pid"

    # ProxyCommand fixes Windows OpenSSH "send client banner first" stall against Railway.
    # Pass a single ArgumentList string so quoted -o values survive Start-Process.
    $proxyCommand = Get-RailwaySshProxyCommand -RepoRoot $RepoRoot
    $sshArgLine = @(
        "-N"
        "-o BatchMode=yes"
        "-o ExitOnForwardFailure=yes"
        "-o IdentitiesOnly=yes"
        "-o StrictHostKeyChecking=accept-new"
        "-o `"ProxyCommand=$proxyCommand`""
        "-L ${LocalPort}:127.0.0.1:${RemotePort}"
        $alias
    ) -join " "

    Write-Host "Opening SSH tunnel 127.0.0.1:${LocalPort} -> ${ServiceName}:${RemotePort} ..." -ForegroundColor Cyan

    $proc = Start-Process `
        -FilePath "ssh" `
        -ArgumentList $sshArgLine `
        -WorkingDirectory $RepoRoot `
        -RedirectStandardOutput $logPath `
        -RedirectStandardError $errPath `
        -PassThru `
        -WindowStyle Hidden

    try {
        Wait-ForTcpPort -Port $LocalPort -Process $proc -TimeoutSec 45
    }
    catch {
        $tail = @()
        if (-not $proc.HasExited) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            Start-Sleep -Milliseconds 200
        }

        if (Test-Path $logPath) { $tail += Get-Content $logPath -Tail 20 -ErrorAction SilentlyContinue }
        if (Test-Path $errPath) { $tail += Get-Content $errPath -Tail 20 -ErrorAction SilentlyContinue }
        $tailText = (($tail | Where-Object { $_ }) -join "`n")

        $hint = @"
Failed to open SSH tunnel on 127.0.0.1:$LocalPort

$tailText

Fix checklist:
  1) railway login
  2) Node.js + OpenSSH Client installed
  3) Retry: .\scripts\postgres-tunnel.ps1
"@
        throw $hint
    }

    Set-Content -Path $pidFile -Value $proc.Id -NoNewline

    return [PSCustomObject]@{
        Process = $proc
        Port    = $LocalPort
        LogPath = $logPath
        PidFile = $pidFile
        Alias   = $alias
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
    Ensure-OpenSshClient
    $sshKeys = Ensure-SshKey
    Ensure-RailwaySshKnownHost
    Ensure-RailwayAuth
    Ensure-RailwayLink -RepoRoot $RepoRoot
    Ensure-RailwaySshKeyRegistered -SshKeys $sshKeys

    $link = Get-RailwayLinkConfig -RepoRoot $RepoRoot
    $postgres = Read-RailwayPostgresEnv -RepoRoot $RepoRoot

    return [PSCustomObject]@{
        Link     = $link
        Postgres = $postgres
        SshKeys  = $sshKeys
    }
}
