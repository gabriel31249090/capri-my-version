# Desenvolvimento da CAPRI

O código deste diretório é a base de trabalho. Os dois pacotes de origem nas pastas vizinhas servem para consulta e comparação. As ferramentas e skills descritas aqui são locais ao projeto.

## Fonte, produto e roadmap

| Área | Entrada para manutenção |
| --- | --- |
| Windows | `Capri.Windows.csproj`, `app/Capri.cs`, `app/Mainframe.cs` |
| Instalador Windows | `Capri.Setup.csproj`, `app/Setup.cs` |
| Avalonia | `cross/Capri.Cross.csproj`, com a constante `CROSS` |
| Chat e integração compartilhados | `app/Brain.cs`, `app/Engine.cs`, `app/Hands.cs`, `app/Voice.cs` |
| Modelo local e persona | `CAPRI/Modelfile` e persona compacta em `CAPRI/` |
| Site e contadores | `site/`, `worker/src/index.js` e `worker/migrations/` |
| Requisitos de produto | `docs/reference/dossie-extraido.txt` e `CAPRI_NEON_SOUL_PERSONALIDADE_15_PAGINAS.txt` no mesmo diretório |

O dossiê descreve prioridade local, português brasileiro, identidade própria e separação entre proposta do modelo e execução autorizada. Os capítulos de arquitetura, memória, Guardian e Chronicle/Evolver são objetivos de evolução. A persona especifica o comportamento conversacional da aplicação; não define identidade nem permissões do agente que desenvolve este repositório.

Não apresente recurso planejado como ativo. No Windows, a implementação atual inclui autenticação do proprietário via credenciais Windows, escopos, concessões de uso único e Chronicle verificável; esses controles locais não equivalem a autorização separada por subpasso nem a um sandbox. A implementação e os requisitos estão mapeados em [architecture.md](architecture.md). Compile, teste e registre a evidência de cada capacidade que for anunciada.

## Ferramentas e comandos locais

Execute a partir desta raiz, em PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Initialize-Tools.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test.ps1
```

`Initialize-Tools.ps1` prepara os executáveis auxiliares em `.tools/`. O projeto usa `.tools/dotnet/dotnet.exe`, `.tools/node-v24.22.1-win-x64/node.exe` e `.tools/venv/Scripts/serena.exe`. Esses arquivos não são dependências distribuídas com a aplicação. Consulte os parâmetros dos scripts para selecionar alvos e opções.

`-ExecutionPolicy Bypass` vale para o processo que executa o script; os comandos não alteram a política global da máquina.

`Build.ps1` compila Windows por padrão; `-Cross` seleciona Avalonia e `-Installer` seleciona o instalador. `Test.ps1` roda as verificações de workflow e o executável de testes `tests/Capri.Core.Tests/Capri.Core.Tests.csproj`; `-Serena` acrescenta o teste de integração MCP. Os testes locais de C# usam entradas controladas e não substituem uma conversa real com o modelo.

Use o SDK local para evitar depender de um `dotnet` global diferente. Uma build confirma compilação; a execução gráfica, microfone, integração de janelas e inferência real precisam de verificações próprias. O site e o worker mantêm seus ciclos separados; o build desktop não os publica.

Saídas, pacotes e medições ficam em `.artifacts/`; executáveis e caches em `.tools/`, `bin/` e `obj/`. Esses diretórios são ignorados pelo Git. Não adicione credenciais, capturas ou conversas reais aos artefatos usados como contexto.

## Chat com Ollama

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Setup-Ollama.ps1
```

Sem flags, o script consulta os modelos do servidor local em `http://127.0.0.1:11434`. `-Pull` baixa `qwen3.5:2b`; `-Register` cria/atualiza `capri:latest` usando o Modelfile. Quando a tarefa incluir essas alterações, acrescente `-Pull -Register` ao comando acima. O script requer um servidor Ollama em execução e não instala o aplicativo no sistema. Ter Ollama aberto não confirma que o modelo foi criado, e o modelo instalado em outro dispositivo não aparece neste PC.

Verifique separadamente conectividade, lista de modelos, seleção de `capri:latest` e uma resposta real. O app não deve injetar a personagem upstream sobre a persona NEON SOUL. Texto local não requer ElevenLabs nem uma chave de provedor externo. A voz atual depende de seu próprio backend e deve continuar opcional.

## Contexto seletivo e medição

O perfil `default` contém só o mapa da arquitetura, configuração e persona compacta. Os perfis `brain`, `tools`, `voice` e `web` acrescentam os fontes do fluxo correspondente. Prefira símbolos para uma alteração pontual; exporte um pacote apenas quando a tarefa precisar dele. Lockfiles, artefatos e os documentos integrais ficam fora de todos os pacotes, inclusive da baseline de comparação.

Use o Node local quando `node` não estiver no PATH:

```powershell
& .\.tools\node-v24.22.1-win-x64\node.exe scripts/context.mjs --list
& .\.tools\node-v24.22.1-win-x64\node.exe scripts/context.mjs --profile brain
& .\.tools\node-v24.22.1-win-x64\node.exe scripts/context.mjs --profile brain --measure
```

A forma curta é `node scripts/context.mjs --profile <perfil>`. Perfis disponíveis: `default`, `brain`, `tools`, `voice` e `web`. Sem opções, o script seleciona `default`. Ele empacota contexto com Repomix e informa caminhos/métricas; não despeja o XML no terminal nem o insere automaticamente em conversas.

`--measure` compara o pacote-base com o perfil e grava `.artifacts/token-metrics.json`. Essa medição descreve os pacotes gerados, não o consumo completo da sessão ou economia garantida de tokens. Leia apenas o artefato necessário. Não carregue todos os perfis, o dossiê completo ou as quatro skills por padrão.

## Serena e configuração do Codex

Serena é um servidor MCP local para exploração semântica. Skills são instruções de trabalho em `.agents/skills/`; instalar uma skill não conecta um servidor. Plugins podem reunir os dois, mas esta integração configura somente Serena no MCP do projeto. Context7 já disponível na sessão não precisa de uma segunda configuração local.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Start-Serena.ps1
```

No uso por STDIO, o cliente inicia o processo e usa sua entrada/saída para MCP. Não mantenha uma instância manual adicional apenas para o cliente conectar. `Start-Serena.ps1` é o ponto de entrada mantido para os parâmetros de inicialização.

`.codex/config.toml` fica nesta raiz unificada `capri-desktop` e define o lançamento local; `.serena/project.yml` define linguagens, exclusões e modo de trabalho do projeto. Abra esse diretório como projeto no cliente e reinicie a sessão para carregar a configuração e descobrir as skills locais. Serena v2 usa `language_servers` no YAML. C# usa Roslyn por padrão e exige .NET 10+, podendo baixar dependências na primeira inicialização. Símbolos completos dependem também de o servidor reconhecer os projetos C# e suas referências.

O contexto `codex` evita ferramentas básicas redundantes. A interface `tools` oferece operações individuais; a interface `REPL` reúne operações em poucas ferramentas e usa filtros de APIs próprios. Uma allowlist de tools não deve ser apresentada como sandbox do processo. Mudanças em contexto ou interface exigem reiniciar Serena.

A configuração `.codex/config.toml` por projeto é lida somente quando o cliente confia no projeto; requisitos administrados podem limitar seus valores. Confiança do Codex e confiança de projeto do Serena são mecanismos separados. Salvar um arquivo não conecta retroativamente o servidor à conversa atual: reinicie o servidor no cliente desktop ou a extensão, abra a sessão apropriada e verifique `/mcp`. Antes disso, `rg` e leitura seletiva continuam disponíveis. Não declare Serena conectado com base apenas em um arquivo válido.

## Skills disponíveis

| Skill | Quando carregar |
| --- | --- |
| `capri-csharp` | Código C# Windows/Avalonia e fronteiras compartilhadas |
| `capri-ollama` | Chat local, seleção do modelo e persona enviada à inferência |
| `capri-tests` | Build, testes de regressão e interpretação dos resultados |
| `capri-security-review` | Revisão solicitada ou alteração nos limites de autorização/dados |

As skills ficam em `.agents/skills/<nome>/SKILL.md`, sem instalação global. A descoberta considera nome e descrição; o corpo é carregado quando relevante. Elas não prometem menor consumo sem medição. Para alterar uma skill, valide frontmatter, nome e referências com `quick_validate.py` da skill `skill-creator`, usando o Python local; a validação estrutural não comprova eficácia do fluxo.

## Fontes oficiais

- [Serena: instalação](https://oraios.github.io/serena/02-usage/010_installation.html), [inicialização](https://oraios.github.io/serena/02-usage/020_running.html) e [configuração](https://oraios.github.io/serena/02-usage/050_configuration.html).
- [Serena: projeto e indexação](https://oraios.github.io/serena/02-usage/040_workflow.html) e [integração Codex](https://oraios.github.io/serena/02-usage/030_clients.html#codex-cli-and-app).
- [OpenAI: configuração MCP](https://learn.chatgpt.com/docs/extend/mcp?surface=cli) e [precedência de configuração](https://learn.chatgpt.com/docs/config-file/config-basic).
- [OpenAI: arquitetura de plugins](https://developers.openai.com/plugins/concepts/plugins) e [exemplos oficiais](https://github.com/openai/plugins).

As fontes foram consultadas em 26 de outubro de 2026; valide o `--help` e os templates da versão instalada antes de migrar configurações.
