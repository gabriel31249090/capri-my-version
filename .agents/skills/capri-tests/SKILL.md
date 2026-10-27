---
name: capri-tests
description: Executar e interpretar as verificações da CAPRI, ou acrescentar regressões para mudanças de comportamento em chat, ferramentas e voz. Use para validar alterações e diagnosticar falhas de build ou testes.
---

# Verificar a CAPRI

Localize a raiz pelo `AGENTS.md`. Prefira os scripts mantidos pelo projeto:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test.ps1
```

No comando de build, acrescente `-Cross` para Avalonia ou `-Installer` para o instalador. `Test.ps1` executa as verificações do workflow e o projeto `tests/Capri.Core.Tests/Capri.Core.Tests.csproj`; `-Serena` acrescenta o teste do servidor MCP. O SDK local está em `.tools/dotnet/dotnet.exe`; saídas ficam em `.artifacts/` ou `bin/obj` ignorados pelo Git.

- Teste o resultado observável da mudança. Para HTTP, use respostas controladas e limites de tempo; a suíte padrão não deve exigir contas, créditos ou modelos baixados.
- Cubra regressões de seleção de modelo, resposta inválida, cancelamento e bloqueio de ações quando forem afetadas pela tarefa.
- Em código compartilhado, verifique Windows e cross; sucesso em um alvo não comprova o outro.
- Reserve testes gráficos, microfone e inferência real para o ambiente que possua essas capacidades. Relate esses checks separadamente.
- Ajustes reversíveis de texto/documentação normalmente pedem revisão e validação de links, não novos testes que comparem frases literais.

Se uma falha depende do ambiente, registre comando, código de saída e causa observada. Não classifique teste não executado como aprovado nem elimine uma asserção para ocultar o erro.

Para contexto exportável, `node scripts/context.mjs --profile brain` ou `--profile tools` pode ajudar; nenhum perfil substitui executar o comportamento.

[Fluxo de desenvolvimento](../../../docs/development-workflow.md).
