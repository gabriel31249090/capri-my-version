. (Join-Path $PSScriptRoot 'Use-LocalTools.ps1')
. (Join-Path $PSScriptRoot 'Configure-Serena.ps1')
$CapriSerena = Join-Path $CapriTools 'venv\Scripts\serena.exe'
if (!(Test-Path -LiteralPath $CapriSerena)) { throw 'Run scripts/Initialize-Tools.ps1 first.' }
& $CapriSerena start-mcp-server --context codex --agent-interface tools --project $CapriRoot --add-mode no-onboarding --transport stdio --enable-web-dashboard false --open-web-dashboard false --enable-gui-log-window false
exit $LASTEXITCODE
