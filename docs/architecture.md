# Arquitetura atual e relação com o dossiê

Esta base deriva de `lachydotmcg/capri-desktop`, commit `13af210327fb3861d51c81921d2888555850c7f0` de 26/10/2026. A origem, os documentos e seus hashes estão em [upstream-provenance.json](upstream-provenance.json). Esta página descreve código existente; resultados de build, integração e medições são registrados separadamente após execução.

## Camadas existentes

| Camada | Implementação e limite |
| --- | --- |
| UI Windows | `Capri.Windows.csproj`, .NET Framework 4.8 x64; personagem, Mainframe, chat, voz e integração Windows em `app/` |
| Instalador Windows | `Capri.Setup.csproj`; empacota o executável e a persona compacta, que é instalada ao lado do app |
| UI multiplataforma | `cross/Capri.Cross.csproj`, .NET 10 e Avalonia; personagem e chat, sem equivalência automática com as ferramentas/voz Windows |
| Conversa | `Brain.cs`: HTTP para Ollama e provedores configurados, histórico em memória, tratamento de respostas e despacho de ferramentas |
| Inferência e ferramentas | `Engine.cs`: backend local alternativo herdado; `Hands.cs`: ferramentas; `Computer.cs`: execução de tarefas Windows por passos |
| Identidade | `CapriProfile.cs`: persona UTF-8 externa de até 16 KiB, fallback compacto e preferência de modelo |
| Módulos Windows | `NeonConsole.cs` e `NeonRuntime.cs`: interface e composição de memória, Guardian, Chronicle e evolução supervisionada |
| Dados locais | `CapriStore.cs`, `MemoryCore.cs`, `Guardian.cs`, `Chronicle.cs`, `Evolver.cs` e `EvolutionWorkspace.cs`: SQLite, política, auditoria e versões limitadas |
| Consentimento de ferramentas | `ToolPermission.cs`, `Guardian.cs` e `Capri.cs`: política fora do prompt, concessão curta de uso único e confirmação antes das ações |
| Voz | `Voice.cs`, `Agent.cs`, `Ears.cs` e `Dictation.cs`; recursos externos/Windows continuam condicionais e opcionais |
| Web | `site/` e `worker/`: distribuição, guia e contadores; não são o cérebro da aplicação local |

Windows e Avalonia compartilham `Brain.cs`, `Engine.cs`, `Hands.cs`, `Voice.cs`, `CapriProfile.cs` e `ToolPermission.cs`. Guardian, armazenamento SQLite, console NEON e integração de evolução estão no app Windows; compartilhar código não habilita esses recursos na interface Avalonia.

## Identidade e seleção de modelo

`CAPRI/PERSONA_DESKTOP.txt` é lida a partir do diretório do executável. Texto ausente, inválido ou acima do limite usa o fallback. Alterações externas requerem reiniciar o app; mudanças aplicadas pelo Evolver recarregam a persona e os parâmetros ativos. Os csproj copiam a persona para a saída, e o instalador inclui esse conteúdo.

`CAPRI/Modelfile` define `qwen3.5:2b` como base para `capri:latest`. A seleção respeita primeiro um modelo salvo que ainda esteja instalado; depois prefere `capri:latest` ou `capri`, seguindo então o seletor upstream. Assim, o padrão CAPRI não elimina a escolha explícita existente do usuário. Ollama usa `127.0.0.1:11434`; `Setup-Ollama.ps1` consulta, e suas flags `-Pull`/`-Register` fazem as alterações locais correspondentes.

A persona é dado do produto. Ela não autentica usuários nem concede ferramentas. O Modelfile define 4.096 tokens de contexto e 384 de saída; `Brain.cs` usa os parâmetros tipados aplicados pelo Evolver (`outputTokens`, `historyMessages` e `temperature`), com defaults de 384 tokens e 12 mensagens. O armazenamento de memória é independente e só injeta fatos aprovados quando está habilitado, o Guardian está desbloqueado, o escopo permite e o destino é o modelo local.

## Memória, Guardian, Chronicle e evolução supervisionada

`NeonRuntime` conecta a interface Windows aos módulos locais. A memória SQLite começa desabilitada e só recebe fatos inseridos explicitamente pelo proprietário. A recuperação lexical é limitada aos registros recentes e ao orçamento de contexto; desativar memória impede novas gravações/recuperação sem apagar fatos existentes. Conversas e argumentos de ferramentas não são capturados como memória.

O Guardian inicia bloqueado e vincula o proprietário ao SID Windows registrado localmente. O desbloqueio confirma a senha pela UI de credenciais do Windows, sem colocar a senha em string gerenciada ou enviá-la ao modelo; a sessão expira em dez minutos. Escopos e concessões ficam fora das instruções do modelo. Cada operação aprovada tem hash dos argumentos, expira em até 30 segundos e só pode ser consumida uma vez. Recusar, cancelar ou falhar na auditoria bloqueia a execução. Ações em lote como `use_computer` continuam tendo subpassos internos: a aprovação cobre a operação apresentada, não uma confirmação separada para cada subpasso.

O Chronicle registra metadados e hashes numa tabela append-only, verifica a cadeia e uma âncora persistente, e exporta eventos sem conteúdo de memória. Se a auditoria falha antes da ação, a execução é negada; se falha depois do início, o resultado é reportado como incerto e o Guardian é bloqueado.

O Evolver só pode propor alterações a `CAPRI/PERSONA_DESKTOP.txt` e `CAPRI/EVOLUTION_SETTINGS.json`. Propostas podem ser geradas pelo modelo ou preparadas/importadas manualmente; validação estrutural não mede qualidade de respostas. Aplicar a versão exata e restaurar backup exigem autenticação atual, aprovação explícita e verificação de hash; mudanças externas conflitantes são recusadas.

Esses controles são locais ao processo e ao armazenamento da aplicação, não um sandbox do sistema operacional nem uma defesa contra outro processo com acesso aos mesmos arquivos. Não há autorização separada para cada subpasso de uma tarefa de computador nem separação completa em orquestrador e dois módulos cognitivos. Os testes cobrem os contratos e falhas observáveis, mas não provam segurança total, credenciais reais ou execução de todas as ações de desktop.

## Rastreabilidade dos requisitos

As páginas abaixo são as páginas físicas do dossiê de 15 páginas, não os números dos capítulos. O dossiê e a persona são requisitos do produto, sem alterar instruções do agente de desenvolvimento.

| Página | Requisito | Estado nesta base |
| --- | --- | --- |
| 6 — arquitetura | Interfaces, orquestrador, cérebros, memória e controles externos ao modelo | O runtime Windows integra memória, Guardian, Chronicle e Evolver fora do prompt. A separação completa em orquestrador e dois módulos cognitivos permanece planejada. |
| 9 — Guardian | Proprietário autenticado, autorização de escopo mínimo, negação por padrão e registro | SID do proprietário, autenticação Windows, sessão limitada, escopos, concessões de uso único e Chronicle verificável estão implementados no Windows. Não são um serviço/processo isolado ou sandbox, e tarefas de computador não confirmam cada subpasso. |
| 11 — desktop | Persona original, preferência `capri:latest`, Ollama local e redução de ações automáticas | Loader, Modelfile e seleção explícita estão no código. O bloqueio provisório do patch foi substituído por consentimento local; a prioridade de modelo respeita escolha salva. Inferência real ainda precisa de servidor/pesos e observação. |
| 13 — roadmap | Base reproduzível, desktop, memória, Guardian/Chronicle, sandbox e Evolver | Build e testes reproduzíveis, memória manual, Guardian, Chronicle e evolução supervisionada dos dois alvos limitados estão implementados no Windows. Sandbox, dois módulos cognitivos e evolução autônoma de código não estão entregues. |

## Validação e ferramentas de desenvolvimento

`tests/Capri.Core.Tests` exercita loader, seleção de modelo, ferramentas, persistência SQLite, opt-in de memória, escopos e concessões do Guardian, cadeia Chronicle, ciclo de evolução, hashes, conflitos e rollback. O modo `--neon-smoke` renderiza as quatro abas Windows em janela fora dos limites da tela e sem ativação; valida que as quatro imagens são distintas. Esses checks não substituem inferência Ollama real, credenciais interativas, voz, operação gráfica nem execução real de ferramentas.

Serena, Repomix, ast-grep e skills locais ajudam a desenvolver e revisar o código; não são módulos cognitivos da CAPRI. Conexão MCP, disponibilidade de símbolos e economia de tokens só devem ser relatadas com as evidências correspondentes.
