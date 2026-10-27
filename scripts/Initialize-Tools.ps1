# Install pinned portable development tools inside this repository only.
param([switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Use-LocalTools.ps1')
$ProgressPreference = 'SilentlyContinue'
New-Item -ItemType Directory -Path $CapriTools -Force | Out-Null
function Get-CapriArchive([string]$Url, [string]$Name, [string]$Hash, [string]$Algorithm = 'SHA256') {
    $CapriArchive = Join-Path $CapriTools $Name
    if (!(Test-Path -LiteralPath $CapriArchive)) { Invoke-WebRequest -Uri $Url -OutFile $CapriArchive -UseBasicParsing }
    if ((Get-FileHash -LiteralPath $CapriArchive -Algorithm $Algorithm).Hash.ToLowerInvariant() -ne $Hash.ToLowerInvariant()) {
        throw "Checksum mismatch: $CapriArchive. Inspect it before retrying."
    }
    return $CapriArchive
}
$CapriUv = Join-Path $CapriTools 'uv\uv.exe'
$CapriNode = Join-Path $CapriTools 'node-v24.22.1-win-x64\node.exe'
$CapriDotnet = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
$CapriPython = Join-Path $CapriTools 'venv\Scripts\python.exe'
if (!$CheckOnly) {
    if (!(Test-Path -LiteralPath $CapriUv)) {
        $CapriArchive = Get-CapriArchive 'https://github.com/astral-sh/uv/releases/download/0.13.5/uv-x86_64-pc-windows-msvc.zip' 'uv.zip' 'a38b8d1902b7cfbc76b423ba1bb456f623f42064cdc72628b6075b89afd1019a'
        Expand-Archive -LiteralPath $CapriArchive -DestinationPath (Join-Path $CapriTools 'uv') -Force
        Remove-Item -LiteralPath $CapriArchive
    }
    if (!(Test-Path -LiteralPath $CapriNode)) {
        $CapriArchive = Get-CapriArchive 'https://nodejs.org/dist/v24.22.1/node-v24.22.1-win-x64.zip' 'node.zip' 'd2046d28c2c3dcd211ac5145c0bf30e2047ef9dfb306693c8c6e2eccd8b95fa5'
        Expand-Archive -LiteralPath $CapriArchive -DestinationPath $CapriTools -Force
        Remove-Item -LiteralPath $CapriArchive
    }
    if (!(Test-Path -LiteralPath $CapriDotnet)) {
        $CapriArchive = Get-CapriArchive 'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/dotnet-sdk-10.0.403-win-x64.zip' 'dotnet.zip' '7a114c318fa7f1d9822991b534b2814d0e78386ceda816b371f5141acefe741d87ccb606ede06cadac8f9ac5070e32b708a67bbd1619ff5e5a7858a27874ef48' 'SHA512'
        Expand-Archive -LiteralPath $CapriArchive -DestinationPath $env:DOTNET_ROOT -Force
        Remove-Item -LiteralPath $CapriArchive
    }
    & $CapriUv python install 3.13.17 --no-bin --no-registry
    if ($LASTEXITCODE -ne 0) { throw 'Local Python installation failed.' }
    if (!(Test-Path -LiteralPath $CapriPython)) {
        & $CapriUv venv (Join-Path $CapriTools 'venv') --python 3.13.17
        if ($LASTEXITCODE -ne 0) { throw 'Local virtual environment creation failed.' }
    }
    & $CapriUv pip sync --python $CapriPython (Join-Path $CapriRoot 'requirements-tools.lock') --require-hashes
    if ($LASTEXITCODE -ne 0) { throw 'Python tool installation failed.' }
    & (Join-Path $CapriTools 'node-v24.22.1-win-x64\npm.cmd') ci --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'Local Node dependency installation failed.' }
    . (Join-Path $PSScriptRoot 'Configure-Serena.ps1')
    & (Join-Path $CapriTools 'venv\Scripts\serena.exe') download-ls-dependencies csharp
    if ($LASTEXITCODE -ne 0) { throw 'Local Roslyn dependency installation failed.' }
    $CapriMcpConfig = Join-Path $CapriRoot '.codex\config.toml'
    $CapriMcpText = Get-Content -LiteralPath $CapriMcpConfig -Raw -Encoding UTF8
    $CapriLaunchPath = Join-Path $CapriRoot 'scripts\Start-Serena.ps1'
    $CapriMcpText = [regex]::Replace($CapriMcpText, "'[^']*Start-Serena\.ps1'", [System.Text.RegularExpressions.MatchEvaluator]{ param($CapriMatch) "'" + $CapriLaunchPath + "'" })
    [System.IO.File]::WriteAllText($CapriMcpConfig, $CapriMcpText, (New-Object System.Text.UTF8Encoding($false)))
}
foreach ($CapriExecutable in @($CapriUv, $CapriNode, $CapriDotnet, $CapriPython)) {
    if (!(Test-Path -LiteralPath $CapriExecutable)) { throw "Missing local tool: $CapriExecutable" }
    & $CapriExecutable --version
    if ($LASTEXITCODE -ne 0) { throw "Local tool check failed: $CapriExecutable" }
}
& (Join-Path $CapriTools 'venv\Scripts\serena.exe') --version
if ($LASTEXITCODE -ne 0) { throw 'Serena version check failed.' }
Write-Host 'Local tools checked. Model setup is a separate explicit operation.'
