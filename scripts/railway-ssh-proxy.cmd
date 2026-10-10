@echo off
setlocal
REM OpenSSH ProxyCommand shim (avoids Windows quoting issues).
REM Host/port args are optional; .mjs defaults to ssh.railway.com:22.
where node >nul 2>nul
if errorlevel 1 (
  echo railway-ssh-proxy: node.exe not found on PATH 1>&2
  exit /b 1
)
node "%~dp0railway-ssh-proxy.mjs" %*
exit /b %ERRORLEVEL%
