---
name: capri-ollama
description: Configurar ou revisar o chat local da CAPRI com Ollama, seleção de capri:latest, Modelfile e persona NEON SOUL. Use para integração de modelos e diagnóstico de inferência local.
---

# Ollama e identidade do produto

Localize a raiz pelo `AGENTS.md`. `CAPRI/Modelfile` e a persona compacta alimentam o produto; `app/Brain.cs` e `app/Engine.cs` controlam a integração compartilhada.

- Prefira `capri:latest` quando instalado. Um servidor acessível sem esse modelo não significa que a CAPRI esteja pronta.
- Use o modelo base definido no Modelfile; não troque a família ou o tamanho silenciosamente. A instalação do celular não disponibiliza o modelo no PC.
- Diferencie servidor Ollama indisponível, modelo ausente, timeout e resposta inválida. Preserve cancelamento e mensagens úteis para diagnóstico.
- Confirme que o prompt enviado pelo app não reintroduz a personagem upstream nem contradiz o Modelfile.
- A persona pede português brasileiro, voz feminina na apresentação, curiosidade e humor moderado. Ela não autoriza ferramentas, inventa memórias ou comprova consciência.
- Contexto e limite de saída exigem medição; tamanho do download não comprova capacidade de inferência nem uso de RAM.

`powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Setup-Ollama.ps1` consulta os modelos do servidor local. Para preparar o modelo, `-Pull` baixa os pesos e `-Register` cria/atualiza a CAPRI pelo Modelfile; combine as flags quando essas operações fizerem parte da tarefa.

Teste HTTP e seleção com `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test.ps1`. Uma conversa real exige Ollama e pesos instalados; identifique separadamente respostas simuladas e inferência observada. Não dependa de ElevenLabs ou de chaves de nuvem para validar texto local.

Leia apenas os trechos necessários de `docs/reference/dossie-extraido.txt` (interface e modelo) e da persona (linguagem, memória e honestidade). [Fluxo e fontes](../../../docs/development-workflow.md).
