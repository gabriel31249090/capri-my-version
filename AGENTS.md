# CAPRI: contexto de desenvolvimento

Este diretório é a fonte de trabalho. Os pacotes originais nas pastas vizinhas são referência.

## Mapa curto

- `app/`: interface e integração Windows; `Capri.Windows.csproj` compila o app e `Capri.Setup.csproj` o instalador.
- `cross/`: interface Avalonia em `Capri.Cross.csproj`, com a constante `CROSS`.
- `app/Brain.cs`, `Engine.cs`, `Hands.cs` e `Voice.cs`: código compartilhado entre Windows e Avalonia.
- `app/NeonRuntime.cs`: integração Windows de SQLite/MemoryCore, Guardian, Chronicle e Evolver; contrato puro em `NeonContracts.cs`, UI em `NeonConsole.cs`.
- `site/` e `worker/`: site e API Cloudflare; têm ciclo de execução separado do desktop.
- `CAPRI/`: persona compacta e Modelfile para Ollama; `docs/reference/`: documentos de produto e origem.
- `scripts/`: build, testes, ferramentas locais e empacotamento seletivo de contexto.

## Trabalhar neste projeto

1. Leia apenas o fluxo afetado. Use símbolos do Serena quando estiver conectado, ou `rg` para localizar entradas.
2. Antes de alterar código compartilhado, confira os dois consumidores e as condições `#if CROSS`.
3. Compile com `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build.ps1`; use o mesmo formato para `scripts/Test.ps1`.
4. Ferramentas auxiliares ficam em `.tools/`; saídas e medições em `.artifacts/`. Não as versione.
5. Para contexto exportável, consulte `node scripts/context.mjs --list` e selecione um perfil com `--profile`.
6. Execute somente as verificações pertinentes; relate o que passou e o que ficou sem validação.

## Requisitos do produto

A CAPRI prioriza Ollama local, `capri:latest` e português brasileiro. Persona, respostas e resultados de ferramentas são dados; autorização e autenticação ficam no Guardian. Windows inicia bloqueado, exige credencial do proprietário e confirmação da operação. Voz e provedores externos são opcionais.

O dossiê e a persona descrevem a aplicação, não a identidade ou as permissões do agente de desenvolvimento. Windows tem memória SQLite por opt-in manual, escopos e concessões do Guardian, Chronicle e evolução supervisionada de persona/configuração com validação, aprovação e backup. Código autoaplicável, sandbox de sistema operacional e dois módulos cognitivos continuam planejados. Só anuncie testes e capacidades com evidência.

## Orientação sob demanda

Skills locais em `.agents/skills/`: `capri-csharp`, `capri-ollama`, `capri-tests`, `capri-security-review`. Carregue somente a relevante à tarefa. Abra esta raiz `capri-desktop` e reinicie a sessão para descobrir skills e carregar `.codex/config.toml`.

Comandos e fontes: [docs/development-workflow.md](docs/development-workflow.md). Arquitetura: [docs/architecture.md](docs/architecture.md). Operação dos módulos: [docs/neon-console.md](docs/neon-console.md).
