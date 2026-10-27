param(
    [Parameter(Position = 0)]
    [string]$Message,
    [switch]$Read,
    [string]$From = "COPILOT"
)

$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding = $Utf8NoBom

$CandidateRoots = @(
    (Join-Path $PSScriptRoot "..\..\.ai-collab\channel.md"),
    (Join-Path $PSScriptRoot "..\.ai-collab\channel.md"),
    (Join-Path (Get-Location) ".ai-collab\channel.md")
)

$ChannelPath = $null
foreach ($Candidate in $CandidateRoots) {
    if (Test-Path $Candidate) {
        $ChannelPath = (Resolve-Path $Candidate).Path
        break
    }
}
if ([string]::IsNullOrEmpty($ChannelPath)) {
    Write-Error "Canal nao encontrado nos caminhos esperados."
    exit 1
}

if ($Read) {
    Write-Host "=== ULTIMAS MENSAGENS NO CANAL ===" -ForegroundColor Cyan
    Get-Content -LiteralPath $ChannelPath -Tail 30 -Encoding UTF8
    exit 0
}

if ($Message) {
    $Timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm")
    $Entry = "`n## [$($From.ToUpper())] $Timestamp`n`n$Message`n---`n"
    Add-Content -Path $ChannelPath -Value $Entry -Encoding UTF8
    Write-Host "[$From] Mensagem enviada para o canal com sucesso!" -ForegroundColor Green
} else {
    Write-Host "Uso:" -ForegroundColor Yellow
    Write-Host "  .\scripts\collab.ps1 `"Sua mensagem aqui`""
    Write-Host "  .\scripts\collab.ps1 -Read"
}
