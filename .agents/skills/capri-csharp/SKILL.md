---
name: capri-csharp
description: Alterar ou revisar o C# da CAPRI Windows e Avalonia, considerando os arquivos compartilhados e as diferenças de plataforma. Use para fluxos desktop, build e integração de UI.
---

# C# da CAPRI

Localize a raiz pelo `AGENTS.md`. Leia o fluxo afetado antes de editar.

- `Capri.Windows.csproj`: app Windows; `Capri.Setup.csproj`: instalador, ambos na raiz.
- `cross/Capri.Cross.csproj`: app Avalonia com `CROSS`.
- `app/Brain.cs`, `Engine.cs`, `Hands.cs` e `Voice.cs` são compilados também pelo projeto cross; uma alteração neles precisa considerar os dois alvos.
- Mantenha acesso Windows, P/Invoke e armazenamento específico de plataforma sob os limites existentes. Confira os ramos `#if CROSS` e as chamadas da UI.
- Preserve o ciclo de cancelamento de chat/voz e o retorno ao thread da UI. Não execute rede bloqueante em handlers de interface.
- Ao alterar seleção de modelo ou persona, consulte a skill `capri-ollama` se esse fluxo fizer parte da tarefa.

Compile com `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build.ps1`; use `-Cross` para Avalonia e `-Installer` para o instalador. Execute `scripts/Test.ps1` no mesmo formato quando alterar comportamento coberto. Não considere a compilação prova de funcionamento gráfico ou inferência.

Se Serena estiver disponível, comece por overview e busca de símbolos. Não carregue os documentos completos de persona para corrigir um evento de UI.

Detalhes e comandos: [fluxo de desenvolvimento](../../../docs/development-workflow.md).
