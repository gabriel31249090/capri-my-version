New-Item -ItemType Directory -Path $env:SERENA_HOME -Force | Out-Null
$CapriSerenaConfig = Join-Path $env:SERENA_HOME 'serena_config.yml'
if (!(Test-Path -LiteralPath $CapriSerenaConfig)) {
    $CapriTrustedRoot = $CapriRoot.Replace("'", "''")
    $CapriSettings = "projects: []`nlanguage_backend: LSP`nagent_interface: tools`nweb_dashboard: false`nweb_dashboard_open_on_launch: false`ngui_log_window: false`ntrusted_project_path_patterns:`n  - '$CapriTrustedRoot'`n"
    [System.IO.File]::WriteAllText($CapriSerenaConfig, $CapriSettings, (New-Object System.Text.UTF8Encoding($false)))
}
elseif ((Get-Content -LiteralPath $CapriSerenaConfig -Raw -Encoding UTF8) -notmatch '(?m)^projects:') {
    [System.IO.File]::AppendAllText($CapriSerenaConfig, "`nprojects: []`n", (New-Object System.Text.UTF8Encoding($false)))
}
& (Join-Path $CapriTools 'venv\Scripts\python.exe') (Join-Path $PSScriptRoot 'configure_serena.py') $CapriRoot $CapriSerenaConfig
if ($LASTEXITCODE -ne 0) { throw 'Serena local configuration failed.' }
