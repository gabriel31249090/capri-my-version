param([switch]$Serena)
. (Join-Path $PSScriptRoot 'Use-LocalTools.ps1')
& (Join-Path $PSScriptRoot 'Test-Workflow.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Workflow tests failed.' }
& (Join-Path $env:DOTNET_ROOT 'dotnet.exe') run --project tests/Capri.Core.Tests/Capri.Core.Tests.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'C# tests failed.' }
if ($Serena) {
    & (Join-Path $CapriTools 'venv\Scripts\python.exe') scripts/test_serena_mcp.py
    if ($LASTEXITCODE -ne 0) { throw 'Serena MCP tests failed.' }
}
