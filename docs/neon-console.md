# Memória / Evolução

No Windows, abra o Mainframe e clique em **MEMÓRIA / EVOLUÇÃO**, perto do rodapé. A janela é redimensionável e tem as abas Memória, Evolução, Guardian e Auditoria. Em telas menores, o Mainframe oferece rolagem vertical para acessar o rodapé.

O cabeçalho mostra o modelo, o estado da memória, o bloqueio e a persistência local. **Modelo desconectado** desabilita a geração por IA; memória, revisão de versões, importação manual, Guardian e auditoria continuam disponíveis após desbloqueio.

Use **Buscar** no cabeçalho para filtrar localmente fatos, propostas e eventos de auditoria por texto. O contador ao lado mostra quantos registros correspondem ao filtro e quantos estão carregados; `0 / N itens` indica que nada corresponde. A busca ignora maiúsculas/minúsculas, preserva a seleção quando o item continua visível e não é enviada ao modelo nem persistida. **Ctrl+F** foca o campo; **Esc** limpa a busca quando ele está focado.

## Desbloquear

Na aba Guardian, use **Desbloquear proprietário** e autentique a conta Windows vinculada à instalação. O diálogo do Windows usa a senha dessa conta; PIN do Windows Hello não é aceito nesse fluxo. A CAPRI não pede a senha em uma caixa própria, nem a envia ao modelo.

A aplicação inicia bloqueada e a sessão autenticada dura até dez minutos. **Bloquear** revoga o acesso de operações futuras. A interface consulta o estado periodicamente e oculta os dados previamente exibidos quando identifica bloqueio ou sessão indisponível. Permitir um escopo libera aquela categoria para pedidos, sem substituir a confirmação da operação.

## Guardar fatos

Na aba Memória, ative **Permitir memória persistente e recuperação de fatos**. Use **Novo fato**, preencha chave, conteúdo e origem, e clique em **Salvar fato**. Para editar, selecione o registro e salve; versões conflitantes são recusadas. **Excluir selecionado** exige confirmação. **Exportar JSON** pede um destino escolhido pelo usuário.

Somente fatos escolhidos manualmente são armazenados. Não há gravação automática da conversa inteira. A recuperação envia ao chat local trechos relacionados ao pedido; não transmite essas memórias aos provedores de nuvem. Desativar a opção preserva os registros existentes e impede sua recuperação.

## Evoluir persona ou configuração

Na aba Evolução, abra **Criar / importar**:

1. Escolha um dos dois alvos permitidos e descreva o objetivo.
2. Com modelo conectado, **Gerar proposta com IA** prepara uma versão para revisão. A geração ocorre em segundo plano e pode ser cancelada.
3. Sem modelo, use **Carregar conteúdo atual**, edite o conteúdo completo e escolha **Preparar versão manual**. Também é possível abrir/colar um JSON exportado e clicar em **Importar JSON**.
4. Em **Propostas e histórico**, selecione a versão e examine **Diff completo**, **Antes** e **Depois**.
5. Clique em **Validar selecionada** e leia o relatório. Uma validação estrutural aprovada não comprova melhor qualidade de respostas nem menor consumo de recursos.
6. Só então use **Aprovar e aplicar versão exata**. Confira alvo e SHA-256. Aplicar exige nova autenticação e confirmação; uma versão divergente é recusada.
7. Para uma versão aplicada, **Restaurar versão anterior** recupera o backup, também com nova autenticação e confirmação. Mudanças externas conflitantes impedem a restauração automática.

Gerar, importar, visualizar ou validar não modifica o produto. Os únicos alvos aplicáveis são a persona UTF-8 compacta e a configuração tipada de geração ao lado do executável. O fluxo não aplica código, scripts, binários ou mudanças de permissões.

Para importar uma configuração manual, o documento usa quatro campos string: `title`, `reason`, `path`, `content`. Em `content`, o JSON da configuração é uma string:

```json
{
  "title": "Aumentar o limite de resposta",
  "reason": "Comparar respostas locais mais completas antes de adotar a mudança.",
  "path": "CAPRI/EVOLUTION_SETTINGS.json",
  "content": "{\"outputTokens\":512,\"historyMessages\":12,\"temperature\":0.8}"
}
```

A configuração aceita `outputTokens` de 64 a 2048, histórico par de 2 a 40 mensagens e temperatura de 0 a 1,5. Uma proposta que não altera nada é recusada. Para a persona, forneça o texto completo, com diretiva explícita de português brasileiro/pt-BR e até 16 KiB em UTF-8.

## Conferir a auditoria

A aba Auditoria lista operações, resultados e hashes. **Verificar integridade** confere cadeia e âncora; **Exportar JSON** exporta os metadados para o destino escolhido. Não há senhas nem transcrição completa nesses eventos. Uma falha de integridade precisa ser investigada; não equivale a uma validação bem-sucedida.

O estado padrão fica em `%LOCALAPPDATA%\Capri\neon-soul\`. Preserve esse diretório e seus arquivos de evolução/âncora ao fazer backup. A proteção não substitui isolamento contra outros processos com acesso aos mesmos arquivos. As limitações técnicas estão em [architecture.md](architecture.md).

## Renderização de teste

O executável aceita `--neon-smoke <diretório>`. Esse modo cria estado de teste no destino, mostra uma janela WinForms fora dos limites da tela e sem ativação, e renderiza quatro PNGs distintos por `DrawToBitmap`. Também verifica o filtro local com registros fictícios descartados após o check. Ele não apresenta uma janela interativa no desktop, pede credenciais, executa inferência ou realiza ações reais. A verificação falha se alguma aba não for distinta ou se a busca não filtrar as três listas. Os PNGs comprovam renderização da interface; não substituem testes do ciclo de aplicação, das credenciais ou de uma conversa com o modelo.
