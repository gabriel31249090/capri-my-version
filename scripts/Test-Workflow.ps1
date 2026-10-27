$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Use-LocalTools.ps1')
$WorkflowRoot = Split-Path -Parent $PSScriptRoot
$WorkflowNode = Join-Path $WorkflowRoot '.tools\node-v24.22.1-win-x64\node.exe'
$WorkflowAst = Join-Path $WorkflowRoot 'node_modules\.bin\ast-grep.cmd'
$WorkflowChecks = [System.Collections.Generic.List[object]]::new()
$WorkflowReport = Join-Path $WorkflowRoot '.artifacts\workflow-checks.json'

function Invoke-WorkflowCheck([string]$Name, [scriptblock]$Check) {
    try {
        & $Check
        $WorkflowChecks.Add([pscustomobject]@{ name = $Name; status = 'pass' })
        Write-Host "PASS $Name"
    } catch {
        $WorkflowChecks.Add([pscustomobject]@{ name = $Name; status = 'fail'; message = $_.Exception.Message })
        throw
    }
}

$WorkflowPassed = $false
try {
    Invoke-WorkflowCheck 'local-tool-versions' {
        if (!(Test-Path -LiteralPath $WorkflowNode) -or !(Test-Path -LiteralPath $WorkflowAst)) {
            throw 'Local Node/ast-grep missing. Run scripts/Initialize-Tools.ps1 first.'
        }
        $nodeVersion = & $WorkflowNode --version
        if ($LASTEXITCODE -ne 0 -or [version]($nodeVersion.TrimStart('v')) -lt [version]'22.0.0') {
            throw 'Node 22 or newer is required.'
        }
        $astVersion = & $WorkflowAst --version
        if ($LASTEXITCODE -ne 0 -or $astVersion -notmatch '0\.51\.0') { throw 'Expected ast-grep 0.51.0.' }
        $package = Get-Content -Raw -LiteralPath (Join-Path $WorkflowRoot 'node_modules\repomix\package.json') | ConvertFrom-Json
        if ($package.version -ne '1.18.1') { throw 'Expected Repomix 1.18.1.' }
    }
    Invoke-WorkflowCheck 'powershell-syntax' {
        foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1') {
            $parseTokens = $null
            $parseErrors = $null
            [System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$parseTokens, [ref]$parseErrors) | Out-Null
            if ($parseErrors.Count -gt 0) { throw ($file.Name + ': ' + $parseErrors[0].Message) }
        }
    }
    Invoke-WorkflowCheck 'context-script-syntax-and-profiles' {
        & $WorkflowNode --check (Join-Path $PSScriptRoot 'context.mjs')
        if ($LASTEXITCODE -ne 0) { throw 'Context script has a syntax error.' }
        $profiles = @(& $WorkflowNode (Join-Path $PSScriptRoot 'context.mjs') --list)
        if ($LASTEXITCODE -ne 0 -or ($profiles -join ',') -ne 'default,brain,tools,voice,web') {
            throw 'Context profiles are missing or invalid.'
        }
    }
    Invoke-WorkflowCheck 'repomix-comparison-settings' {
        $selection = Get-Content -Raw -LiteralPath (Join-Path $WorkflowRoot 'repomix.config.json') | ConvertFrom-Json
        $baseline = Get-Content -Raw -LiteralPath (Join-Path $WorkflowRoot 'repomix.baseline.config.json') | ConvertFrom-Json
        $selection.include = @()
        $baseline.include = @()
        $selection.output.filePath = ''
        $baseline.output.filePath = ''
        if (($selection | ConvertTo-Json -Depth 20 -Compress) -cne ($baseline | ConvertTo-Json -Depth 20 -Compress)) {
            throw 'Baseline and selected settings differ beyond inclusion/output path.'
        }
        if ($selection.tokenCount.encoding -ne 'o200k_base' -or !$selection.security.enableSecurityCheck) {
            throw 'The documented tokenizer and secret scan must remain enabled.'
        }
        if ($selection.ignore.customPatterns -notcontains 'docs/reference/**') { throw 'Reference dossiers must stay outside context packs.' }
    }
    Invoke-WorkflowCheck 'ast-grep-csharp-structural-search' {
        $fixture = 'class Smoke { void Test() { var brain = new Brain { Act = null }; } }'
        $json = $fixture | & $WorkflowAst run --lang csharp --pattern 'new Brain { Act = $ACTION }' --stdin --json=compact
        if ($LASTEXITCODE -ne 0) { throw 'C# structural search failed.' }
        $matches = @(($json -join "`n") | ConvertFrom-Json)
        if ($matches.Count -ne 1 -or $matches[0].text -notmatch 'new Brain') { throw 'Expected one C# AST match.' }
    }
    Invoke-WorkflowCheck 'ast-grep-security-review-rule' {
        # This string is parsed as data. No executable or source fixture is launched.
        $fixture = 'class Smoke { void Test() { Process.Start("not-executed.exe"); } }'
        $json = $fixture | & $WorkflowAst scan --rule (Join-Path $WorkflowRoot 'rules\review-process-launch.yml') --stdin --json=compact
        if ($LASTEXITCODE -ne 0) { throw 'Security review rule could not scan C# input.' }
        $matches = @(($json -join "`n") | ConvertFrom-Json)
        if ($matches.Count -ne 1 -or $matches[0].ruleId -ne 'capri-review-process-launch') {
            throw 'Expected the process-launch review rule to match once.'
        }
    }
    $WorkflowPassed = $true
} finally {
    $report = [pscustomobject]@{
        generatedAt = [DateTime]::UtcNow.ToString('o')
        passed = $WorkflowPassed
        checks = @($WorkflowChecks.ToArray())
    }
    [System.IO.Directory]::CreateDirectory((Split-Path -Parent $WorkflowReport)) | Out-Null
    [System.IO.File]::WriteAllText($WorkflowReport, ($report | ConvertTo-Json -Depth 8), [System.Text.UTF8Encoding]::new($false))
}
