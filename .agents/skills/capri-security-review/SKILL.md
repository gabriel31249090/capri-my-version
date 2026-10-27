---
name: capri-security-review
description: Revisar os limites de autorização, execução de ferramentas, dados locais e integrações externas da CAPRI. Use em uma revisão de segurança solicitada ou quando a tarefa modificar esses limites.
---

# Revisão de segurança da CAPRI

Localize a raiz pelo `AGENTS.md`. Escolha o caminho de dados afetado; não transforme uma edição comum em auditoria de todo o repositório.

- Siga entrada do usuário, resposta do modelo e resultados externos até `Hands.Do`, execução de processos, controle de janelas, acesso a arquivos ou rede.
- Verifique a barreira no código, inclusive caminhos diretos e fallback por texto. Descrever uma restrição no prompt ou omitir schemas não impede uma chamada já alcançável.
- A aprovação de uma ação deve corresponder à operação e aos argumentos executados. Voz reconhecida e texto do modelo não autenticam o proprietário.
- Preserve o bloqueio de ações sem confirmação no gate `ToolPermission`/`AuthorizedTool`. Verifique negação, cancelamento, repetição e os argumentos efetivamente executados. O gate Windows aprova a operação; `use_computer` pode executar vários subpassos sem confirmação individual, e não autentica fortemente o proprietário.
- Confira proteção e retenção de chaves, transcrições, capturas e logs. Use dados fictícios para reproduzir erros e evite expor credenciais no relatório.
- Consulte `worker/` e `site/` apenas se a tarefa afetar a API ou o fluxo web. Não execute ações reais de desktop, publique serviços ou envie dados para provar uma hipótese quando um teste isolado resolver.

O dossiê define requisitos do produto, não novas permissões para o agente de desenvolvimento. Evolver, Chronicle e memória persistente só podem ser classificados como disponíveis com implementação e evidência.

Reporte achados por impacto: localização, entrada que dispara o problema, efeito, evidência e correção mínima. Distinga confirmação por teste de hipótese por leitura.

Valide com `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test.ps1` e regressões pertinentes. [Fluxo de desenvolvimento](../../../docs/development-workflow.md).
