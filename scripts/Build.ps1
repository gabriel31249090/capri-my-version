param([switch]$Cross, [switch]$Installer)
. (Join-Path $PSScriptRoot 'Use-LocalTools.ps1')
if (!(Test-Path -LiteralPath (Join-Path $env:DOTNET_ROOT 'dotnet.exe'))) { throw 'Run scripts/Initialize-Tools.ps1 first.' }
$CapriProject = if ($Cross) { 'cross/Capri.Cross.csproj' } elseif ($Installer) { 'Capri.Setup.csproj' } else { 'Capri.Windows.csproj' }
& (Join-Path $env:DOTNET_ROOT 'dotnet.exe') build $CapriProject -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed: $CapriProject" }
