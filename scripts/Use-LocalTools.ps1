$ErrorActionPreference = 'Stop'
$CapriRoot = Split-Path -Parent $PSScriptRoot
$CapriTools = Join-Path $CapriRoot '.tools'
$env:DOTNET_ROOT = Join-Path $CapriTools 'dotnet'
$env:DOTNET_CLI_HOME = Join-Path $CapriTools 'dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_NOLOGO = '1'
$env:NUGET_PACKAGES = Join-Path $CapriTools 'nuget'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $CapriTools 'nuget-http-cache'
$env:UV_CACHE_DIR = Join-Path $CapriTools 'uv-cache'
$env:UV_PYTHON_INSTALL_DIR = Join-Path $CapriTools 'python'
$env:UV_PYTHON_BIN_DIR = Join-Path $CapriTools 'python-bin'
# Short cache name keeps Roslyn's longest DLL/config paths below Windows MAX_PATH.
$env:SERENA_HOME = Join-Path $CapriTools 's'
$env:npm_config_cache = Join-Path $CapriTools 'npm-cache'
$env:PYTHONUTF8 = '1'
$env:PATH = (Join-Path $CapriTools 'node-v24.22.1-win-x64') + ';' + $env:DOTNET_ROOT + ';' + (Join-Path $CapriTools 'venv\Scripts') + ';' + $env:PATH
Set-Location -LiteralPath $CapriRoot
