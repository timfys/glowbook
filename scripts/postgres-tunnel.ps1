# Railway -> local Postgres for DBeaver and other clients.
# For full dev cycle use: .\scripts\dev.ps1

param(
    [string]$Service = "",
    [int]$LocalPort = 0
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "railway-common.ps1")

$repoRoot = Get-RepoRoot
$tunnel = $null

try {
    Write-Host "GlowBook - setting up Railway..." -ForegroundColor Green
    $setup = Initialize-RailwayDevEnvironment -RepoRoot $repoRoot

    if ([string]::IsNullOrWhiteSpace($Service)) {
        $Service = $setup.Link.serviceName
    }

    $tunnel = Start-RailwayPostgresTunnel `
        -RepoRoot $repoRoot `
        -LocalPort $LocalPort `
        -ServiceName $Service

    $postgres = $setup.Postgres
    Write-Host ""
    Write-Host "PostgreSQL tunnel open:" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Host:     127.0.0.1"
    Write-Host "  Port:     $($tunnel.Port)"
    Write-Host "  User:     $($postgres.POSTGRES_USER)"
    Write-Host "  Password: $($postgres.POSTGRES_PASSWORD)"
    Write-Host "  Database: $($postgres.POSTGRES_DB)"
    Write-Host "  SSL:      Disable"
    Write-Host ""
    Write-Host "Keep this window open. Ctrl+C to stop." -ForegroundColor Yellow
    Write-Host ""

    Wait-Process -Id $tunnel.Process.Id
}
finally {
    if ($tunnel) {
        if (-not $tunnel.Process.HasExited) {
            Stop-Process -Id $tunnel.Process.Id -Force -ErrorAction SilentlyContinue
        }

        if ($tunnel.PidFile -and (Test-Path $tunnel.PidFile)) {
            Remove-Item $tunnel.PidFile -Force -ErrorAction SilentlyContinue
        }
    }
}
