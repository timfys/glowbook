# Railway → локальный Postgres на 127.0.0.1:5432 для DBeaver / dotnet run
#
# SSH НЕ лежит в Settings сервиса. Нужен Railway CLI.
#
# Один раз:
#   1) Установи CLI: https://docs.railway.com/guides/cli
#   2) railway login
#   3) cd в репо → railway link  (проект glowbook, сервис Postgres)
#   4) railway ssh keys add     (если ещё не добавлял ключ)
#
# Потом каждый раз (окно НЕ закрывай):
#   .\scripts\postgres-tunnel.ps1
#
# DBeaver / appsettings.Development.json:
#   Host=127.0.0.1  Port=5432  DB=railway  User=postgres
#   Password из Variables Postgres (PGPASSWORD)
#   SSL=disable

param(
    # Имя Postgres-сервиса в Railway (как на канвасе)
    [string]$Service = "Postgres",
    [int]$LocalPort = 5432
)

if (-not (Get-Command railway -ErrorAction SilentlyContinue)) {
    Write-Host @"
Railway CLI не найден.

Windows (PowerShell от админа или обычный — как получится):
  iwr https://railway.com/install.ps1 | iex

Потом:
  railway login
  railway link
  .\scripts\postgres-tunnel.ps1
"@
    exit 1
}

Write-Host "Tunnel: 127.0.0.1:$LocalPort  ← Railway $Service (SSH)"
Write-Host "Окно не закрывай. Ctrl+C — стоп."
Write-Host ""

railway connect $Service --tunnel-only -P $LocalPort
