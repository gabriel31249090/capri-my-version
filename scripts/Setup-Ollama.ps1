param([switch]$Pull, [switch]$Register)
$ErrorActionPreference = 'Stop'
$CapriRoot = Split-Path -Parent $PSScriptRoot
$CapriTags = Invoke-RestMethod -Uri 'http://127.0.0.1:11434/api/tags' -TimeoutSec 5
$CapriTags.models | Select-Object name, size
if ($Pull -or $Register) {
    $CapriOllama = Get-Command ollama -ErrorAction SilentlyContinue
    if (!$CapriOllama) { throw 'Ollama is not on PATH. Install/start it separately; this script does not install system software.' }
    if ($Pull) {
        & $CapriOllama.Source pull qwen3.5:2b
        if ($LASTEXITCODE -ne 0) { throw 'Ollama pull failed.' }
    }
    if ($Register) {
        & $CapriOllama.Source create capri -f (Join-Path $CapriRoot 'CAPRI\Modelfile')
        if ($LASTEXITCODE -ne 0) { throw 'Ollama registration failed.' }
    }
}
