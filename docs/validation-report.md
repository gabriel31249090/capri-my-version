# Entrega e validação da CAPRI Desktop

Relatório consolidado em 27/10/2026. As medições comparativas abaixo foram registradas em 26/10/2026; os artefatos guardam o instante de cada execução. A base unificada é este diretório `capri-desktop`, derivado do commit oficial `13af210327fb3861d51c81921d2888555850c7f0`. As duas pastas recebidas e os documentos originais foram preservados.

## Resultado da integração

O fonte original incorpora diretamente a persona NEON SOUL compacta, um loader UTF-8 com fallback e preferência inicial por `capri:latest`, preservando uma seleção salva válida. O Modelfile usa `qwen3.5:2b`. O projeto não depende de reaplicar substituições textuais do patch antigo.

As funcionalidades upstream continuam no fonte. As ferramentas do Windows usam consentimento local nos canais de texto e voz; cancelamento e encerramento da sessão invalidam operações pendentes. Foram corrigidos os caminhos de envio por Enter/Return e a alteração indevida de maiúsculas em URLs. O instalador distribui a persona e preserva uma cópia já editada pelo usuário.

O app Windows agora integra memória SQLite por opt-in, Guardian vinculado ao SID Windows, Chronicle com cadeia/âncora verificável e Evolver supervisionado para persona e parâmetros tipados. A UI Avalonia não tem paridade desses módulos. Autorização separada por subpasso, sandbox do sistema operacional e os dois módulos cognitivos continuam planejados; inferência real, credenciais interativas e ações reais não são comprovadas pelos testes isolados. [Arquitetura e rastreabilidade do dossiê](architecture.md).

## Ferramentas realmente configuradas

| Ferramenta | Estado e evidência |
| --- | --- |
| Serena 2.1.0 | Instalado no venv local; MCP STDIO no contexto `codex`, Roslyn C# e 10 ferramentas. Handshake, símbolos, referências e edição de fixture compilável passaram. |
| Repomix 1.18.1 | Dependência npm local. Perfis `default`, `brain`, `tools`, `voice`, `web`; scanner de segredos ativo, manifests SHA256 e medição `o200k_base`. |
| ast-grep 0.51.0 | CLI local funcional, busca estrutural C# e regra informativa de revisão de `Process.Start`; sem outro MCP. |
| Context7 | Consultas reais de documentação efetuadas pela conexão disponível no cliente. A configuração do projeto reutiliza essa conexão sob demanda. |
| Skills CAPRI | Quatro skills locais validadas: `capri-csharp`, `capri-ollama`, `capri-tests`, `capri-security-review`. |
| Runtimes de desenvolvimento | Node 24.22.1, uv 0.13.5, Python 3.13.17 e SDK .NET 10.0.403 instalados em `.tools/`. |

Fontes de configuração: [Serena](https://oraios.github.io/serena/02-usage/010_installation.html), [configuração Serena/C#](https://oraios.github.io/serena/02-usage/050_configuration.html), [Repomix](https://repomix.com/guide/configuration), [ast-grep](https://ast-grep.github.io/reference/cli/run.html), [Context7](https://github.com/upstash/context7), [OpenAI plugins](https://github.com/openai/plugins). O repositório de plugins serviu como referência de composição de skills/MCP; a integração usa um único servidor MCP local adicional.

`Initialize-Tools.ps1` fixa versões e hashes dos arquivos portáteis, usa locks locais e configura Python com `--no-bin --no-registry`. A inscrição automática de Python criada durante a primeira instalação foi desfeita com autorização do usuário, validando que apontava para este workspace. PATH, política PowerShell e variáveis dos wrappers têm escopo de processo. O cliente global não foi reconfigurado.

Para carregar o servidor na interface do Codex, abra a raiz unificada como projeto confiável e reinicie a sessão. O teste independente do protocolo comprova o funcionamento local; salvar a configuração não adiciona ferramentas retroativamente a uma conversa aberta.

## Arquivos alterados ou acrescentados

| Grupo | Arquivos |
| --- | --- |
| Integração e correções | `app/Brain.cs`, `Capri.cs`, `Computer.cs`, `Hands.cs`, `Setup.cs`; novos `app/CapriProfile.cs` e `app/ToolPermission.cs` |
| Build | `Capri.Windows.csproj`, `Capri.Setup.csproj`, `Capri.slnx`, `Directory.Build.props`, `NuGet.Config`, `global.json`, `cross/Capri.Cross.csproj` |
| Persona | `CAPRI/PERSONA_DESKTOP.txt`, `CAPRI/Modelfile` |
| Contexto e instruções | `AGENTS.md`, `.agents/skills/*/SKILL.md`, `.codex/config.toml`, `.serena/project.yml`, `.serena/.gitignore` |
| Tooling | `package.json`, `package-lock.json`, `requirements-tools.txt`, `requirements-tools.lock`, `repomix.config.json`, `repomix.baseline.config.json`, `sgconfig.yml`, `rules/review-process-launch.yml`, `.gitignore` |
| Scripts | `scripts/Build.ps1`, `Test.ps1`, `Test-Workflow.ps1`, `Initialize-Tools.ps1`, `Use-LocalTools.ps1`, `Setup-Ollama.ps1`, `Start-Serena.ps1`, `Configure-Serena.ps1`, `configure_serena.py`, `context.mjs`, `test_serena_mcp.py` |
| Testes | `tests/Capri.Core.Tests/Capri.Core.Tests.csproj` e `Program.cs` |
| Documentação | `README-CAPRI.md`, aviso inicial em `README.md`, `docs/architecture.md`, `development-workflow.md`, `upstream-provenance.json`, `token-measurements.json`, este relatório e cópias de referência em `docs/reference/` |

Também foi acrescentado um `AGENTS.md` de encaminhamento na pasta pai. Site, worker, imagens e texto da licença upstream foram preservados. Os hashes das referências e a verificação de licença estão em [upstream-provenance.json](upstream-provenance.json).

## Testes realizados

| Verificação | Resultado |
| --- | --- |
| App Windows net48 | Build Release nesta validação: zero erros e zero avisos |
| Instalador Windows net48 | Build Release nesta validação: zero erros e zero avisos |
| Avalonia net10.0 | Build Release nesta validação: zero erros e zero avisos |
| Suíte C# | 187 checks aprovados: loader, seleção de modelo, ferramentas, SQLite, memória, Guardian, Chronicle, evolução supervisionada, conflitos, hashes, rollback e contrato Ollama com servidor falso em loopback |
| Contrato Ollama | `Brain.Ask` real com servidor falso TCP/HTTP em loopback: rota, modelo, persona e mensagem enviados; resposta válida recebida |
| Workflow | 6 checks aprovados nesta validação: versões, parser PowerShell, sintaxe/perfis, equivalência de configs, AST C# e regra de revisão em fixture inerte |
| Interface NEON | `--neon-smoke` renderizou quatro imagens distintas das abas; janela fora da tela e sem ativação, sem credenciais, inferência ou ações |
| Serena MCP | 10 ferramentas expostas; busca de `Brain/PickModel`, referências a `CapriProfile/Persona` e edição simbólica de fixture seguida de compilação |
| Instalador/payload | Reflexão sem executar instalação: SHA256 do EXE embutido igual ao app atual e persona UTF-8 idêntica |
| Skills | Quatro `SKILL.md` aprovados por `quick_validate.py` |
| JavaScript | `node --check` em `site/app.js`, `site/guide.js` e `worker/src/index.js` aprovado |
| Contexto | Perfis `default` e `brain` medidos e manifests verificados; perfis adicionais exercitados separadamente |
| Fontes | `git diff --check` e inspeção de arquivos de fonte vazios aprovados após recuperação do espaço |

Comandos de reprodução principais:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Initialize-Tools.ps1 -CheckOnly
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build.ps1 -Installer
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build.ps1 -Cross
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test.ps1 -Serena
```

O app está em `.artifacts/bin/Capri.Windows/Release/net48/Capri.exe`; o instalador em `.artifacts/bin/Capri.Setup/Release/net48/Capri-Setup.exe`. Logs e evidências regeneráveis ficam em `.artifacts/` e são ignorados pelo Git.

## Comparação real de contexto exportado

Repomix 1.18.1, tokenizer `o200k_base`, XML com números de linha e sem compressão. A mesma baseline, formato e filtros foram usados nos dois pares; só a seleção de arquivos muda. Lockfiles, binários, caches, referências integrais e o relatório gerado ficam fora de ambos os lados.

| Pacote | Arquivos | Tokens contabilizados | Redução frente à baseline |
| --- | ---: | ---: | ---: |
| Baseline filtrada | 81 | 336.006 | — |
| Mapa inicial `default` | 14 | 11.433 | 96,60% |
| Fluxo de cérebro `brain` | 18 | 34.277 | 89,80% |

Esses números medem o tamanho dos pacotes produzidos pelo tokenizer escolhido. Os escopos são diferentes: um mapa inicial e um fluxo específico não substituem todo o fonte. Não foi medida economia no total de tokens da conversa/API, ganho de precisão, latência ou qualidade das alterações. Os resultados completos, hashes e instantes estão em [token-measurements.json](token-measurements.json).

```powershell
& .\.tools\node-v24.22.1-win-x64\node.exe scripts/context.mjs --profile default --measure
& .\.tools\node-v24.22.1-win-x64\node.exe scripts/context.mjs --profile brain --measure
& .\.tools\node-v24.22.1-win-x64\node.exe scripts/context.mjs --profile brain --check
```

## Limitações encontradas

- Não havia servidor Ollama acessível em `127.0.0.1:11434`. Nenhum modelo foi baixado ou registrado; inferência real Qwen permanece sem validação.
- Não foram exercitados GUI, microfone, voz externa, captura de tela, controle real do computador ou instalação/desinstalação. Compilar e testar componentes não comprova esses fluxos em uso.
- Avalonia compilou neste Windows; execução e pacotes específicos macOS/Linux não foram testados.
- O dossiê propõe respostas maiores e histórico ampliado. A interface e os limites de resposta curta herdados foram conservados; o Modelfile, sozinho, não elimina o corte do aplicativo.
- O disco ficou sem espaço durante restore e geração de artefatos. Downloads e caches gerados foram limpos após autorização explícita; fontes afetados por gravações interrompidas foram restaurados e novamente testados. Novas instalações e builds exigem manter espaço livre suficiente.
- O Roslyn excedeu o limite de caminhos do Windows no primeiro cache. O cache local curto `.tools/s` resolveu o teste neste caminho; mover o projeto para um caminho mais profundo pode exigir novo ajuste.
- Dados de configuração e executáveis são locais ao projeto. As medições não incluem o contexto já carregado pelo cliente nem tornam a allowlist MCP uma sandbox.
