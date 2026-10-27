# CAPRI Desktop / NEON SOUL

Base única do projeto: fonte oficial `lachydotmcg/capri-desktop` com a persona NEON SOUL, integração local Qwen/Ollama e ferramentas de desenvolvimento isoladas neste diretório. As pastas recebidas foram preservadas. A origem e os documentos fornecidos estão registrados em [docs/upstream-provenance.json](docs/upstream-provenance.json).

## Compilar e verificar no Windows

Os executáveis auxiliares já configurados ficam em `.tools/`. Para reproduzir a instalação em outra cópia Windows x64, execute o primeiro comando; ele baixa dependências locais e requer espaço em disco e internet.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Initialize-Tools.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test.ps1
```

`Initialize-Tools.ps1 -CheckOnly` verifica os executáveis sem reinstalar. A política de execução do PowerShell muda apenas nesse processo. Nenhuma ferramenta é instalada globalmente.

O aplicativo compilado fica em `.artifacts/bin/Capri.Windows/Release/net48/Capri.exe`. Para produzir o instalador, use `scripts/Build.ps1 -Installer` com a mesma chamada PowerShell. Para Avalonia, use `-Cross`.

## Modelo e personalidade

Inicie sua instalação do Ollama para Windows. Este projeto não instala o serviço nem baixa modelos automaticamente.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Setup-Ollama.ps1
```

Sem flags, o comando consulta `127.0.0.1:11434/api/tags`. Se você quiser baixar o modelo e registrar a CAPRI, acrescente `-Pull -Register`; isso baixa `qwen3.5:2b` e cria/atualiza `capri:latest`. Texto local não precisa de chave de API. Modelos de outro aparelho são instalações separadas.

O app usa `capri:latest` como preferência inicial quando disponível e conserva uma seleção salva válida. A persona de execução é o arquivo `CAPRI/PERSONA_DESKTOP.txt` ao lado do executável. Mudanças aplicadas pelo Evolver são recarregadas na aplicação; após edição externa, reinicie o app. O instalador conserva arquivos de persona já existentes.

O documento completo de personalidade e as 15 páginas do dossiê estão em `docs/reference/`. O prompt compacto deriva deles; os documentos integrais não são enviados a cada conversa.

## Memória, Guardian, Chronicle e evolução

No Mainframe Windows, abra **MEMÓRIA / EVOLUÇÃO**. A nova janela tem quatro abas e mostra o estado dos módulos. O Guardian inicia bloqueado; use a senha da conta Windows vinculada para desbloquear. PIN do Windows Hello não é aceito por esse diálogo. A sessão dura até dez minutos, e cada operação precisa de confirmação com concessão de uso único.

- A busca do cabeçalho filtra localmente memórias, propostas e auditoria, com contador de resultados; `Ctrl+F` foca o campo e `Esc` limpa a busca. O texto não é enviado ao modelo nem salvo.
- **Memória SQLite:** fatos manuais com opt-in, origem, edição, exclusão, exportação e recuperação por relevância para o modelo local. A conversa inteira não é salva automaticamente; as memórias não são injetadas em provedores de nuvem.
- **Guardian:** proprietário vinculado ao SID, bloqueio, escopos e operações autorizadas por hash. Texto de chat e voz não autenticam o proprietário.
- **Chronicle:** eventos e resultados como metadados/hash, com verificação de cadeia/âncora e exportação sem credenciais.
- **Evolver:** propostas reais de persona/configuração por modelo ou edição/importação manual, diff completo, validação estrutural, aprovação da versão exata, backup e restauração. Aplicar e restaurar exigem autenticação atual.

Com o modelo desconectado, os módulos persistentes e o ciclo manual continuam utilizáveis após desbloqueio; apenas a geração por IA depende de inferência. [Guia de operação e JSON manual](docs/neon-console.md).

## Recursos e limites

Personagem, chat, voz opcional, visão opcional e ferramentas upstream continuam no código. Os módulos novos estão integrados ao app Windows; não se afirma equivalência automática no preview Avalonia. Uma tarefa `use_computer` pode incluir vários passos. Guardian não é um serviço independente nem um sandbox de sistema operacional, e ações externas já iniciadas não são uma transação reversível.

A evolução aplica somente `CAPRI/PERSONA_DESKTOP.txt` e `CAPRI/EVOLUTION_SETTINGS.json`. Alteração automática de código, scripts, binários ou permissões e a arquitetura de dois módulos cognitivos continuam planejadas. A validação mede estrutura e parâmetros; não promete respostas melhores, menor latência ou economia de RAM. Consulte [a arquitetura atual](docs/architecture.md) e o relatório de validação antes de considerar uma capacidade testada.

Para conferir somente a renderização dos quatro painéis, o executável oferece `--neon-smoke <diretório>`; ele cria estado de teste separado e quatro PNGs distintos usando uma janela sem ativação, fora dos limites da tela. Não apresenta uma janela interativa, pede credenciais ou executa ações reais. Esse modo não substitui testes funcionais ou inferência real.

## Desenvolvimento com contexto seletivo

Abra esta pasta `capri-desktop` no Codex e reinicie a sessão para carregar o MCP Serena e descobrir as quatro skills em `.agents/skills/`. Context7 já disponível no cliente é reutilizado sob demanda. Repomix e ast-grep são CLIs locais, sem servidores MCP adicionais.

Comandos, fontes oficiais e medição reproduzível: [docs/development-workflow.md](docs/development-workflow.md). A licença original [LICENSE.md](LICENSE.md) continua aplicável.
