# One command for local dev with Railway Postgres:
#   .\scripts\dev.ps1
#
# Installs CLI, SSH key, login/link, tunnel, dotnet run.

param(
    [int]$Port = 0,
    [switch]$TunnelOnly
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "railway-common.ps1")

$repoRoot = Get-RepoRoot
$tunnel = $null

try {
    Write-Host "GlowBook dev - setting up Railway..." -ForegroundColor Green
    $setup = Initialize-RailwayDevEnvironment -RepoRoot $repoRoot

    Write-Host "Opening SSH tunnel to $($setup.Link.serviceName)..." -ForegroundColor Cyan
    $tunnel = Start-RailwayPostgresTunnel `
        -RepoRoot $repoRoot `
        -LocalPort $Port `
        -ServiceName $setup.Link.serviceName

    $null = Set-PostgresTunnelEnvironment -PostgresEnv $setup.Postgres -Port $tunnel.Port

    Write-Host ""
    Write-Host "Tunnel: 127.0.0.1:$($tunnel.Port) -> Railway Postgres" -ForegroundColor Green
    Write-Host "DATABASE_URL is set for this process."
    Write-Host ""

    if ($TunnelOnly) {
        Write-Host "Tunnel-only mode. Ctrl+C to stop." -ForegroundColor Yellow
        Wait-Process -Id $tunnel.Process.Id
        return
    }

    Push-Location (Join-Path $repoRoot "src/GlowBook.Web")
    try {
        dotnet run
    }
    finally {
        Pop-Location
    }
}
finally {
    if ($tunnel) {
        if (-not $tunnel.Process.HasExited) {
            Write-Host "Closing tunnel..." -ForegroundColor DarkGray
            Stop-Process -Id $tunnel.Process.Id -Force -ErrorAction SilentlyContinue
        }

        if ($tunnel.PidFile -and (Test-Path $tunnel.PidFile)) {
            Remove-Item $tunnel.PidFile -Force -ErrorAction SilentlyContinue
        }
    }
}
