using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed class NeonConsole : Form
    {
        private static readonly Color Surface = Color.FromArgb(19, 24, 33);
        private static readonly Color Ink = Color.FromArgb(235, 241, 248);
        private static readonly Color Accent = Color.FromArgb(85, 227, 224);
        private readonly INeonRuntime runtime;
        private readonly TabControl tabs = new TabControl();
        private readonly Label summary = new Label();
        private readonly TextBox search = Editor(false);
        private readonly Label searchCount = new Label();
        private readonly Label message = new Label();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly Button cancel = new Button();
        private readonly System.Windows.Forms.Timer heartbeat = new System.Windows.Forms.Timer();
        private readonly CheckBox memoryEnabled = new CheckBox();
        private readonly DataGridView memoryGrid;
        private readonly TextBox memoryKind = Editor(false);
        private readonly TextBox memoryKey = Editor(false);
        private readonly TextBox memoryValue = Editor(true);
        private readonly TextBox memorySource = Editor(false);
        private readonly DataGridView proposalGrid;
        private readonly ComboBox target = new ComboBox();
        private readonly TextBox goal = Editor(true);
        private readonly TextBox manualContent = Editor(true);
        private readonly TextBox importJson = Editor(true);
        private readonly TextBox diff = Editor(true, true);
        private readonly TextBox before = Editor(true, true);
        private readonly TextBox after = Editor(true, true);
        private readonly TextBox validation = Editor(true, true);
        private readonly Label proposalInfo = new Label();
        private readonly Button generate;
        private readonly Button apply;
        private readonly Button restore;
        private readonly Label guardianInfo = new Label();
        private readonly DataGridView scopeGrid;
        private readonly DataGridView auditGrid;
        private readonly TextBox auditDetails = Editor(true, true);
        private NeonMemoryItem editingMemory;
        private NeonProposalItem selectedProposal;
        private NeonRuntimeStatus lastStatus;
        private List<NeonMemoryItem> memoryItems = new List<NeonMemoryItem>();
        private List<NeonProposalItem> proposalItems = new List<NeonProposalItem>();
        private List<NeonAuditItem> auditItems = new List<NeonAuditItem>();
        private CancellationTokenSource generation;
        private bool busy;
        private bool syncing;
        private bool loadingRows;
        private bool checkingStatus;
        private bool smokeRendering;

        internal NeonConsole(INeonRuntime runtime)
        {
            if (runtime == null) throw new ArgumentNullException("runtime");
            this.runtime = runtime;
            Text = "CAPRI — Memória / Evolução";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9.5f);
            KeyPreview = true;
            BackColor = Surface;
            ForeColor = Ink;
            MinimumSize = new Size(640, 480);
            Rectangle available = Screen.FromControl(this).WorkingArea;
            Size = new Size(Math.Min(1000, available.Width - 40), Math.Min(760, available.Height - 48));

            TableLayoutPanel shell = PanelLayout(1, 3);
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            summary.Dock = DockStyle.Fill;
            summary.Padding = new Padding(12, 8, 12, 4);
            summary.ForeColor = Accent;
            summary.Text = "Conectando módulos locais…";
            TableLayoutPanel header = PanelLayout(5, 1);
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            header.Controls.Add(summary, 0, 0);
            Label searchLabel = new Label { Text = "Buscar", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 8, 0), ForeColor = Ink, AccessibleName = "Busca local" };
            header.Controls.Add(searchLabel, 1, 0);
            search.AccessibleName = "Buscar em memória, propostas e auditoria";
            search.AccessibleDescription = "Filtra registros localmente. O texto não é enviado ao modelo nem salvo.";
            search.MaxLength = 128;
            search.Margin = new Padding(2, 17, 8, 17);
            search.TextChanged += delegate { ApplySearch(); };
            header.Controls.Add(search, 2, 0);
            searchCount.Dock = DockStyle.Fill;
            searchCount.TextAlign = ContentAlignment.MiddleCenter;
            searchCount.ForeColor = Accent;
            searchCount.AccessibleName = "Contagem de resultados da busca";
            searchCount.AccessibleDescription = "Quantidade de registros visíveis em memória, propostas e auditoria.";
            searchCount.Margin = new Padding(0);
            header.Controls.Add(searchCount, 3, 0);
            Button reload = new Button { Text = "Atualizar", Dock = DockStyle.Fill, Margin = new Padding(5, 13, 10, 13) };
            reload.Click += async delegate { await RunAsync(RefreshAllAsync, "Módulos locais atualizados."); };
            header.Controls.Add(reload, 4, 0);
            shell.Controls.Add(header, 0, 0);
            tabs.Dock = DockStyle.Fill;
            tabs.Padding = new Point(14, 6);
            shell.Controls.Add(tabs, 0, 1);
            TableLayoutPanel bottom = PanelLayout(3, 1);
            bottom.Padding = new Padding(8);
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            message.Dock = DockStyle.Fill;
            message.TextAlign = ContentAlignment.MiddleLeft;
            message.AutoEllipsis = true;
            progress.Dock = DockStyle.Fill;
            progress.Style = ProgressBarStyle.Marquee;
            progress.Visible = false;
            cancel.Text = "Cancelar geração";
            cancel.Dock = DockStyle.Fill;
            cancel.Enabled = false;
            cancel.Click += delegate { CancelGeneration(); };
            bottom.Controls.Add(message, 0, 0);
            bottom.Controls.Add(progress, 1, 0);
            bottom.Controls.Add(cancel, 2, 0);
            shell.Controls.Add(bottom, 0, 2);
            Controls.Add(shell);

            memoryGrid = Grid("Tipo", "Kind", "Chave", "Key", "Conteúdo", "Value", "Atualizado", "UpdatedAt");
            proposalGrid = Grid("Criada", "CreatedAt", "Alvo", "Target", "Título", "Title", "Estado", "Status");
            scopeGrid = Grid("Escopo", "Name", "Identificador", "Id", "Permitido", "Enabled");
            auditGrid = Grid("Quando", "CreatedAt", "Operação", "Action", "Resultado", "Result");

            tabs.TabPages.Add(MemoryPage());
            Button generatedButton;
            Button applyButton;
            Button restoreButton;
            tabs.TabPages.Add(EvolutionPage(out generatedButton, out applyButton, out restoreButton));
            generate = generatedButton;
            apply = applyButton;
            restore = restoreButton;
            tabs.TabPages.Add(GuardianPage());
            tabs.TabPages.Add(AuditPage());
            heartbeat.Interval = 5000;
            heartbeat.Tick += async delegate
            {
                if (busy || checkingStatus || IsDisposed) return;
                checkingStatus = true;
                try
                {
                    NeonRuntimeStatus state = await runtime.GetStatusAsync();
                    if (!IsDisposed && !busy) ApplyStatus(state);
                }
                catch (Exception)
                {
                    if (!IsDisposed && !busy)
                    {
                        lastStatus = null;
                        ClearSensitiveViews();
                        summary.Text = "Sessão indisponível — dados locais ocultados. Atualize para verificar o Guardian.";
                        UpdateActions();
                    }
                }
                finally { checkingStatus = false; }
            };
            Shown += async delegate
            {
                if (smokeRendering) return;
                await RunAsync(RefreshAllAsync, "Módulos locais atualizados.");
                if (!IsDisposed) heartbeat.Start();
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!busy) return;
                e.Cancel = true;
                if (generation != null) { CancelGeneration(); SetMessage("Cancelando geração; aguarde a conclusão."); }
                else SetMessage("Aguarde a operação em andamento antes de fechar.");
            };
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.F)
                {
                    search.Focus();
                    search.SelectAll();
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Escape && search.Focused && search.Text.Length > 0)
                {
                    search.Clear();
                    e.SuppressKeyPress = true;
                }
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                heartbeat.Stop();
                heartbeat.Dispose();
                CancelGeneration();
            }
            base.Dispose(disposing);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        private TabPage MemoryPage()
        {
            TabPage page = Page("Memória");
            TableLayoutPanel body = PanelLayout(1, 5);
            body.Padding = new Padding(10);
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 56));
            memoryEnabled.Text = "Permitir memória persistente e recuperação de fatos";
            memoryEnabled.AutoSize = true;
            memoryEnabled.ForeColor = Accent;
            memoryEnabled.CheckedChanged += async delegate
            {
                if (syncing || busy) return;
                bool enabled = memoryEnabled.Checked;
                await RunAsync(async delegate
                {
                    try { await runtime.SetMemoryEnabledAsync(enabled); }
                    finally { await RefreshStatusAsync(); }
                }, enabled ? "Memória ativada por escolha do proprietário." : "Memória desativada; registros existentes foram preservados.");
            };
            body.Controls.Add(memoryEnabled, 0, 0);
            body.Controls.Add(Info("Salve somente fatos escolhidos. Esta tela não grava conversas inteiras automaticamente. Desativar impede o uso da memória sem apagar os registros."), 0, 1);
            FlowLayoutPanel actions = Toolbar();
            AddButton(actions, "Atualizar", async delegate { await RunAsync(RefreshMemoryAsync, "Memória atualizada."); });
            AddButton(actions, "Novo fato", delegate { ClearMemoryEditor(); });
            AddButton(actions, "Excluir selecionado", async delegate
            {
                NeonMemoryItem item = Selected<NeonMemoryItem>(memoryGrid);
                if (item == null) { SetMessage("Selecione um fato para excluir."); return; }
                if (!Confirm("Excluir o fato selecionado?\n\n" + item.Key + "\n\nEssa remoção afeta a memória persistente.")) return;
                await RunAsync(async delegate
                {
                    await runtime.DeleteMemoryAsync(item.Id, item.Revision);
                    ClearMemoryEditor();
                    await RefreshMemoryAsync();
                }, "Fato excluído da memória.");
            });
            AddButton(actions, "Exportar JSON", async delegate { await ExportAsync("capri-memoria.json", runtime.ExportMemoryAsync); });
            body.Controls.Add(actions, 0, 2);
            body.Controls.Add(memoryGrid, 0, 3);
            memoryGrid.SelectionChanged += delegate
            {
                if (loadingRows || busy) return;
                NeonMemoryItem item = Selected<NeonMemoryItem>(memoryGrid);
                if (item == null) return;
                editingMemory = CopyMemory(item);
                memoryKind.Text = item.Kind ?? "fato";
                memoryKey.Text = item.Key ?? "";
                memoryValue.Text = item.Value ?? "";
                memorySource.Text = item.Source ?? "";
            };
            TableLayoutPanel edit = PanelLayout(2, 5);
            edit.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            edit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            edit.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            edit.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            edit.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            edit.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            edit.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            Field(edit, 0, "Tipo", memoryKind);
            Field(edit, 1, "Chave", memoryKey);
            Field(edit, 2, "Conteúdo", memoryValue);
            Field(edit, 3, "Origem", memorySource);
            FlowLayoutPanel saveRow = Toolbar();
            AddButton(saveRow, "Salvar fato", async delegate
            {
                if (string.IsNullOrWhiteSpace(memoryKey.Text) || string.IsNullOrWhiteSpace(memoryValue.Text))
                { SetMessage("Preencha a chave e o conteúdo do fato.", true); return; }
                NeonMemoryItem item = editingMemory == null ? new NeonMemoryItem() : CopyMemory(editingMemory);
                item.Kind = memoryKind.Text.Trim();
                item.Key = memoryKey.Text.Trim();
                item.Value = memoryValue.Text;
                item.Source = memorySource.Text.Trim();
                await RunAsync(async delegate
                {
                    await runtime.SaveMemoryAsync(item);
                    ClearMemoryEditor();
                    await RefreshMemoryAsync();
                    await RefreshStatusAsync();
                }, "Fato salvo na memória persistente.");
            });
            edit.Controls.Add(saveRow, 1, 4);
            body.Controls.Add(edit, 0, 4);
            page.Controls.Add(body);
            ClearMemoryEditor();
            return page;
        }

        private TabPage EvolutionPage(out Button generatedButton, out Button applyButton, out Button restoreButton)
        {
            TabPage page = Page("Evolução");
            TabControl views = new TabControl { Dock = DockStyle.Fill };
            TabPage history = Page("Propostas e histórico");
            TableLayoutPanel review = PanelLayout(1, 5);
            review.Padding = new Padding(10);
            review.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            review.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
            review.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            review.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            review.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            FlowLayoutPanel refreshRow = Toolbar();
            AddButton(refreshRow, "Atualizar histórico", async delegate { await RunAsync(async delegate { await RefreshProposalsAsync(null); }, "Histórico atualizado."); });
            AddButton(refreshRow, "Exportar proposta", async delegate
            {
                if (selectedProposal == null) { SetMessage("Selecione uma proposta para exportar."); return; }
                string id = selectedProposal.Id;
                await ExportAsync("capri-proposta.json", delegate { return runtime.ExportProposalAsync(id); });
            });
            review.Controls.Add(refreshRow, 0, 0);
            review.Controls.Add(proposalGrid, 0, 1);
            proposalGrid.SelectionChanged += async delegate
            {
                if (loadingRows || busy) return;
                NeonProposalItem row = Selected<NeonProposalItem>(proposalGrid);
                if (row == null) return;
                string id = row.Id;
                await RunAsync(async delegate { SetProposal(await runtime.GetProposalAsync(id)); }, "Proposta carregada.");
            };
            proposalInfo.Dock = DockStyle.Fill;
            proposalInfo.AutoEllipsis = true;
            proposalInfo.Padding = new Padding(3, 4, 3, 2);
            review.Controls.Add(proposalInfo, 0, 2);
            TabControl preview = new TabControl { Dock = DockStyle.Fill };
            AddEditorPage(preview, "Diff completo", diff);
            AddEditorPage(preview, "Antes", before);
            AddEditorPage(preview, "Depois", after);
            AddEditorPage(preview, "Validação", validation);
            review.Controls.Add(preview, 0, 3);
            FlowLayoutPanel decisions = Toolbar();
            AddButton(decisions, "Validar selecionada", async delegate
            {
                if (selectedProposal == null) { SetMessage("Selecione uma proposta para validar."); return; }
                string id = selectedProposal.Id;
                await RunAsync(async delegate
                {
                    SetProposal(await runtime.ValidateProposalAsync(id));
                    await RefreshProposalsAsync(id);
                }, "Validação concluída; consulte o relatório e o estado da proposta.");
            });
            applyButton = AddButton(decisions, "Aprovar e aplicar versão exata", async delegate
            {
                NeonProposalItem item = selectedProposal;
                if (item == null || !item.CanApply || string.IsNullOrEmpty(item.ContentHash))
                { SetMessage("A proposta precisa estar validada e possuir hash verificável.", true); return; }
                if (!Confirm("Aplicar exatamente a proposta exibida?\n\nAlvo: " + item.Target + "\nSHA-256: " + item.ContentHash + "\n\nConfira o diff antes de aprovar. Uma versão divergente será recusada.")) return;
                await RunAsync(async delegate
                {
                    SetProposal(await runtime.ApplyProposalAsync(item.Id, item.ContentHash));
                    await RefreshProposalsAsync(item.Id);
                    await RefreshStatusAsync();
                }, "Versão aprovada aplicada. Consulte o histórico e a auditoria.");
            });
            restoreButton = AddButton(decisions, "Restaurar versão anterior", async delegate
            {
                NeonProposalItem item = selectedProposal;
                if (item == null || !item.CanRestore) { SetMessage("Selecione uma versão aplicada com backup restaurável."); return; }
                if (!Confirm("Restaurar o backup da proposta selecionada?\n\nAlvo: " + item.Target + "\nProposta: " + item.Id + "\n\nA restauração será recusada se a versão atual divergir do histórico.")) return;
                await RunAsync(async delegate
                {
                    SetProposal(await runtime.RestoreProposalAsync(item.Id));
                    await RefreshProposalsAsync(item.Id);
                    await RefreshStatusAsync();
                }, "Versão anterior restaurada.");
            });
            review.Controls.Add(decisions, 0, 4);
            history.Controls.Add(review);

            TabPage create = Page("Criar / importar");
            TableLayoutPanel composer = PanelLayout(1, 5);
            composer.Padding = new Padding(10);
            composer.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            composer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            composer.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            composer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            composer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            composer.Controls.Add(Info("Propostas alteram persona ou configuração validada. Código livre fica como plano exportável; não é aplicado automaticamente. Nada é aplicado ao gerar ou importar."), 0, 0);
            FlowLayoutPanel targetRow = Toolbar();
            targetRow.Controls.Add(new Label { Text = "Alvo", AutoSize = true, Padding = new Padding(0, 8, 6, 0), ForeColor = Ink });
            target.DropDownStyle = ComboBoxStyle.DropDownList;
            target.Width = 170;
            target.AccessibleName = "Alvo da proposta";
            targetRow.Controls.Add(target);
            AddButton(targetRow, "Carregar conteúdo atual", async delegate
            {
                string name = target.SelectedItem as string;
                if (name == null) { SetMessage("Escolha um alvo."); return; }
                if (manualContent.Modified && !Confirm("Substituir o conteúdo em edição pela versão atual do alvo?")) return;
                await RunAsync(async delegate
                {
                    manualContent.Text = await runtime.ReadEvolutionTargetAsync(name);
                    manualContent.Modified = false;
                }, "Conteúdo atual carregado; edite e prepare uma proposta manual.");
            });
            composer.Controls.Add(targetRow, 0, 1);
            goal.AccessibleName = "Objetivo da evolução";
            goal.MaxLength = 1800;
            Panel goalPanel = new Panel { Dock = DockStyle.Fill };
            Label goalLabel = new Label { Text = "Objetivo da mudança", Dock = DockStyle.Top, Height = 22, ForeColor = Ink };
            goalPanel.Controls.Add(goal);
            goalPanel.Controls.Add(goalLabel);
            goal.BringToFront();
            goalLabel.BringToFront();
            composer.Controls.Add(goalPanel, 0, 2);
            TabControl contentViews = new TabControl { Dock = DockStyle.Fill };
            AddEditorPage(contentViews, "Versão manual — conteúdo completo", manualContent);
            AddEditorPage(contentViews, "JSON de proposta", importJson);
            composer.Controls.Add(contentViews, 0, 3);
            FlowLayoutPanel createButtons = Toolbar();
            generatedButton = AddButton(createButtons, "Gerar proposta com IA", async delegate
            {
                string name = target.SelectedItem as string;
                string request = goal.Text.Trim();
                if (lastStatus == null || !lastStatus.ModelConnected) { SetMessage("Modelo desconectado. Use a versão manual ou importe JSON.", true); return; }
                if (name == null || request.Length == 0) { SetMessage("Escolha o alvo e descreva o objetivo.", true); return; }
                await RunAsync(async delegate
                {
                    CancellationTokenSource pending = new CancellationTokenSource();
                    generation = pending;
                    cancel.Enabled = true;
                    try
                    {
                        NeonProposalItem created = await Task.Run(() => runtime.CreateProposalAsync(name, request, pending.Token));
                        pending.Token.ThrowIfCancellationRequested();
                        await RefreshProposalsAsync(created.Id);
                        views.SelectedTab = history;
                    }
                    finally { generation = null; pending.Dispose(); cancel.Enabled = false; }
                }, "Proposta gerada. Revise e valide antes de aplicar.");
            });
            AddButton(createButtons, "Preparar versão manual", async delegate
            {
                string name = target.SelectedItem as string;
                string content = manualContent.Text;
                string request = goal.Text.Trim();
                if (name == null || string.IsNullOrWhiteSpace(content)) { SetMessage("Escolha o alvo e preencha o conteúdo completo.", true); return; }
                await RunAsync(async delegate
                {
                    NeonProposalItem created = await runtime.CreateManualProposalAsync(name, content, request);
                    await RefreshProposalsAsync(created.Id);
                    views.SelectedTab = history;
                }, "Proposta manual registrada; confira o diff e valide.");
            });
            AddButton(createButtons, "Abrir arquivo JSON", async delegate
            {
                using (OpenFileDialog dialog = new OpenFileDialog { Filter = "Proposta JSON (*.json)|*.json", CheckFileExists = true })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    string filename = dialog.FileName;
                    await RunAsync(async delegate
                    {
                        if (new FileInfo(filename).Length > 1024 * 1024) throw new InvalidOperationException("O JSON excede o limite de 1 MiB da importação.");
                        importJson.Text = await Task.Run(() => File.ReadAllText(filename, new UTF8Encoding(false, true)));
                        contentViews.SelectedIndex = 1;
                    }, "JSON carregado para revisão; clique em Importar JSON para registrar a proposta.");
                }
            });
            AddButton(createButtons, "Importar JSON", async delegate
            {
                string json = importJson.Text;
                if (string.IsNullOrWhiteSpace(json)) { SetMessage("Cole ou abra um JSON de proposta antes de importar.", true); return; }
                await RunAsync(async delegate
                {
                    NeonProposalItem created = await runtime.ImportProposalAsync(json);
                    await RefreshProposalsAsync(created.Id);
                    views.SelectedTab = history;
                }, "Proposta importada; valide antes de aprovar qualquer aplicação.");
            });
            composer.Controls.Add(createButtons, 0, 4);
            create.Controls.Add(composer);
            views.TabPages.Add(history);
            views.TabPages.Add(create);
            page.Controls.Add(views);
            return page;
        }

        private TabPage GuardianPage()
        {
            TabPage page = Page("Guardian");
            TableLayoutPanel body = PanelLayout(1, 4);
            body.Padding = new Padding(10);
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            guardianInfo.Dock = DockStyle.Fill;
            guardianInfo.ForeColor = Accent;
            guardianInfo.Padding = new Padding(4);
            body.Controls.Add(guardianInfo, 0, 0);
            FlowLayoutPanel controls = Toolbar();
            AddButton(controls, "Atualizar estado", async delegate { await RunAsync(RefreshStatusAsync, "Guardian atualizado."); });
            AddButton(controls, "Bloquear", async delegate
            {
                await RunAsync(async delegate { await runtime.SetGuardianLockedAsync(true); await RefreshAllAsync(); }, "Guardian bloqueado.");
            });
            AddButton(controls, "Desbloquear proprietário", async delegate
            {
                await RunAsync(async delegate { await runtime.SetGuardianLockedAsync(false); await RefreshAllAsync(); }, "Solicitação de desbloqueio concluída; confira o estado do proprietário.");
            });
            AddButton(controls, "Permitir escopo", async delegate { await ChangeScopeAsync(true); });
            AddButton(controls, "Negar escopo", async delegate { await ChangeScopeAsync(false); });
            body.Controls.Add(controls, 0, 1);
            body.Controls.Add(scopeGrid, 0, 2);
            body.Controls.Add(Info("Permitir um escopo não autoriza uma ação futura. Use Bloquear para impedir operações até novo desbloqueio do proprietário. Fechar esta janela não concede permissões."), 0, 3);
            page.Controls.Add(body);
            return page;
        }

        private TabPage AuditPage()
        {
            TabPage page = Page("Auditoria");
            TableLayoutPanel body = PanelLayout(1, 4);
            body.Padding = new Padding(10);
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
            body.Controls.Add(Info("Chronicle registra eventos e resultados, com credenciais omitidas. Verifique a integridade antes de exportar o histórico."), 0, 0);
            FlowLayoutPanel controls = Toolbar();
            AddButton(controls, "Atualizar eventos", async delegate { await RunAsync(RefreshAuditAsync, "Auditoria atualizada."); });
            AddButton(controls, "Verificar integridade", async delegate
            {
                await RunAsync(async delegate
                {
                    string report = await runtime.VerifyAuditAsync();
                    auditDetails.Text = report;
                    MessageBox.Show(this, report, "Chronicle — integridade", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }, "Verificação concluída; consulte o relatório.");
            });
            AddButton(controls, "Exportar JSON", async delegate { await ExportAsync("capri-auditoria.json", runtime.ExportAuditAsync); });
            body.Controls.Add(controls, 0, 1);
            body.Controls.Add(auditGrid, 0, 2);
            body.Controls.Add(auditDetails, 0, 3);
            auditGrid.SelectionChanged += delegate
            {
                if (loadingRows) return;
                NeonAuditItem item = Selected<NeonAuditItem>(auditGrid);
                if (item != null) auditDetails.Text = item.Details ?? "";
            };
            page.Controls.Add(body);
            return page;
        }

        private async Task ChangeScopeAsync(bool enabled)
        {
            NeonScopeItem scope = Selected<NeonScopeItem>(scopeGrid);
            if (scope == null) { SetMessage("Selecione um escopo."); return; }
            if (enabled && !Confirm("Permitir o escopo " + scope.Name + "?\n\nIsso não substitui a autorização de cada operação.")) return;
            await RunAsync(async delegate { await runtime.SetScopeAsync(scope.Id, enabled); await RefreshStatusAsync(); }, "Política do escopo atualizada.");
        }

        private async Task RefreshAllAsync()
        {
            string selectedTarget = target.SelectedItem as string;
            await RefreshStatusAsync();
            await RefreshMemoryAsync();
            string[] names = await runtime.ListEvolutionTargetsAsync();
            target.Items.Clear();
            foreach (string name in names ?? new string[0]) target.Items.Add(name);
            if (selectedTarget != null && target.Items.Contains(selectedTarget))
                target.SelectedItem = selectedTarget;
            else if (target.Items.Count > 0)
                target.SelectedIndex = 0;
            await RefreshProposalsAsync(null);
            await RefreshAuditAsync();
        }

        private void CancelGeneration()
        {
            CancellationTokenSource pending = generation;
            if (pending == null) return;
            try { pending.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        private async Task RefreshStatusAsync()
        {
            NeonRuntimeStatus state = await runtime.GetStatusAsync();
            ApplyStatus(state);
        }

        private void ApplyStatus(NeonRuntimeStatus state)
        {
            if (state == null) throw new InvalidOperationException("O runtime não retornou o estado dos módulos.");
            bool clear = state.GuardianLocked && (lastStatus == null || !lastStatus.GuardianLocked);
            lastStatus = state;
            summary.Text = (state.ModelConnected ? "Modelo: " + state.ModelName : "Modelo desconectado — memória, Guardian e auditoria continuam locais.") +
                "\nMemória: " + (state.MemoryEnabled ? "ativada" : "desativada") + "  |  Guardian: " + (state.GuardianLocked ? "bloqueado" : "desbloqueado") + "  |  " + (state.StorageStatus ?? "");
            guardianInfo.Text = "Proprietário ativo: " + (state.Owner ?? "não identificado") + "\nEstado: " + (state.GuardianLocked ? "BLOQUEADO" : "DESBLOQUEADO") + "\n" + (state.StorageStatus ?? "");
            syncing = true;
            try { memoryEnabled.Checked = state.MemoryEnabled; }
            finally { syncing = false; }
            if (clear) ClearSensitiveViews();
            NeonScopeItem selectedScope = Selected<NeonScopeItem>(scopeGrid);
            scopeGrid.DataSource = new List<NeonScopeItem>(state.Scopes ?? new List<NeonScopeItem>());
            scopeGrid.ClearSelection();
            if (selectedScope != null)
                foreach (DataGridViewRow row in scopeGrid.Rows)
                {
                    NeonScopeItem scope = row.DataBoundItem as NeonScopeItem;
                    if (scope != null && scope.Id == selectedScope.Id) { row.Selected = true; break; }
                }
            UpdateActions();
        }

        // Rendering-only entry point: an off-screen, non-activating window creates
        // real WinForms handles so DrawToBitmap can render the selected tab.
        // Read-only list methods are empty while Guardian is locked.
        internal void SmokeRender(string directory)
        {
            Directory.CreateDirectory(directory);
            ApplyStatus(runtime.GetStatusAsync().GetAwaiter().GetResult());
            loadingRows = true;
            try
            {
                memoryItems = new List<NeonMemoryItem>(runtime.ListMemoryAsync().GetAwaiter().GetResult() ?? new List<NeonMemoryItem>());
                proposalItems = new List<NeonProposalItem>(runtime.ListProposalsAsync().GetAwaiter().GetResult() ?? new List<NeonProposalItem>());
                auditItems = new List<NeonAuditItem>(runtime.ListAuditAsync().GetAwaiter().GetResult() ?? new List<NeonAuditItem>());
                ApplySearch();
                VerifyLocalSearch();
                memoryGrid.ClearSelection();
                proposalGrid.ClearSelection();
                auditGrid.ClearSelection();
            }
            finally { loadingRows = false; }
            SetProposal(null);
            string[] names = runtime.ListEvolutionTargetsAsync().GetAwaiter().GetResult();
            target.Items.Clear();
            foreach (string name in names ?? new string[0]) target.Items.Add(name);
            if (target.Items.Count > 0) target.SelectedIndex = 0;
            SetMessage("Renderização local; nenhuma proposta ou ação executada.");
            CreateControl();
            Rectangle virtualScreen = SystemInformation.VirtualScreen;
            Location = new Point(virtualScreen.Left - Width - 64, virtualScreen.Top - Height - 64);
            ShowInTaskbar = false;
            string[] filenames = { "neon-memoria.png", "neon-evolucao.png", "neon-guardian.png", "neon-auditoria.png" };
            HashSet<string> renderedHashes = new HashSet<string>(StringComparer.Ordinal);
            smokeRendering = true;
            try
            {
                Show();
                Application.DoEvents();
                for (int i = 0; i < tabs.TabPages.Count; i++)
                {
                    tabs.SelectedIndex = i;
                    LayoutTree(this);
                    Application.DoEvents();
                    using (Bitmap bitmap = new Bitmap(Width, Height))
                    {
                        DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                        string path = Path.Combine(directory, filenames[i]);
                        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                        using (SHA256 sha = SHA256.Create())
                        {
                            byte[] hash = sha.ComputeHash(File.ReadAllBytes(path));
                            renderedHashes.Add(BitConverter.ToString(hash));
                        }
                    }
                }
            }
            finally
            {
                Hide();
                smokeRendering = false;
            }
            if (tabs.TabPages.Count != filenames.Length || renderedHashes.Count != filenames.Length)
                throw new InvalidOperationException("Nem todas as abas NEON produziram uma renderização visual distinta.");
            File.WriteAllText(Path.Combine(directory, "neon-ui-smoke.txt"),
                "PASS four distinct local module tabs rendered off-screen without activation\nPASS local search filters memory, proposals and audit case-insensitively\nPASS local search result counter includes zero-result feedback\nLIMIT no real model, credentials or mutation invoked\n", new UTF8Encoding(false));
        }

        private void VerifyLocalSearch()
        {
            List<NeonMemoryItem> memories = memoryItems;
            List<NeonProposalItem> proposals = proposalItems;
            List<NeonAuditItem> audit = auditItems;
            try
            {
                memoryItems = new List<NeonMemoryItem>
                {
                    new NeonMemoryItem { Key = "capri-smoke-search", Value = "Fixture local" },
                    new NeonMemoryItem { Key = "sem correspondência", Value = "Fixture local" }
                };
                proposalItems = new List<NeonProposalItem>
                {
                    new NeonProposalItem { Id = "capri-smoke-search", Title = "Fixture" },
                    new NeonProposalItem { Id = "other", Title = "Sem correspondência" }
                };
                auditItems = new List<NeonAuditItem>
                {
                    new NeonAuditItem { Action = "capri-smoke-search", Result = "Fixture" },
                    new NeonAuditItem { Action = "other", Result = "Sem correspondência" }
                };
                search.Text = "CAPRI-SMOKE-SEARCH";
                if (memoryGrid.Rows.Count != 1 || proposalGrid.Rows.Count != 1 || auditGrid.Rows.Count != 1)
                    throw new InvalidOperationException("A busca local não filtrou corretamente Memória, Evolução e Auditoria.");
                if (searchCount.Text != "3 / 6 itens")
                    throw new InvalidOperationException("A contagem da busca local não corresponde aos registros filtrados.");
                search.Text = "sem resultado";
                if (memoryGrid.Rows.Count != 0 || proposalGrid.Rows.Count != 0 || auditGrid.Rows.Count != 0 ||
                    searchCount.Text != "0 / 6 itens")
                    throw new InvalidOperationException("A busca local não informou corretamente quando não há resultados.");
            }
            finally
            {
                memoryItems = memories;
                proposalItems = proposals;
                auditItems = audit;
                search.Clear();
                ApplySearch();
            }
        }

        private static void LayoutTree(Control control)
        {
            control.CreateControl();
            control.PerformLayout();
            foreach (Control child in control.Controls) LayoutTree(child);
        }

        private async Task RefreshMemoryAsync()
        {
            IList<NeonMemoryItem> items = await runtime.ListMemoryAsync();
            memoryItems = items == null ? new List<NeonMemoryItem>() : items.ToList();
            ApplySearch();
        }

        private void ApplySearch()
        {
            string query = search.Text.Trim();
            NeonMemoryItem selectedMemory = Selected<NeonMemoryItem>(memoryGrid);
            NeonProposalItem selectedRow = Selected<NeonProposalItem>(proposalGrid);
            NeonAuditItem selectedAudit = Selected<NeonAuditItem>(auditGrid);
            List<NeonMemoryItem> visibleMemories = memoryItems
                .Where(item => Matches(query, item.Kind, item.Key, item.Value, item.Source, item.UpdatedAt)).ToList();
            List<NeonProposalItem> visibleProposals = proposalItems
                .Where(item => Matches(query, item.Target, item.Title, item.Status, item.Id, item.Validation)).ToList();
            List<NeonAuditItem> visibleAudit = auditItems
                .Where(item => Matches(query, item.CreatedAt, item.Action, item.Result, item.Details)).ToList();
            loadingRows = true;
            try
            {
                memoryGrid.DataSource = visibleMemories;
                proposalGrid.DataSource = visibleProposals;
                auditGrid.DataSource = visibleAudit;
                int visibleCount = visibleMemories.Count + visibleProposals.Count + visibleAudit.Count;
                int totalCount = memoryItems.Count + proposalItems.Count + auditItems.Count;
                searchCount.Text = query.Length == 0
                    ? totalCount + " itens"
                    : visibleCount + " / " + totalCount + " itens";
                memoryGrid.ClearSelection();
                proposalGrid.ClearSelection();
                auditGrid.ClearSelection();
                RestoreSelection<NeonMemoryItem>(memoryGrid, selectedMemory == null ? null : selectedMemory.Id, item => item.Id);
                RestoreSelection<NeonProposalItem>(proposalGrid, selectedRow == null ? null : selectedRow.Id, item => item.Id);
                RestoreSelection<NeonAuditItem>(auditGrid, selectedAudit == null ? null : selectedAudit.Id, item => item.Id);
            }
            finally { loadingRows = false; }
            if (selectedMemory != null && !Matches(query, selectedMemory.Kind, selectedMemory.Key, selectedMemory.Value, selectedMemory.Source, selectedMemory.UpdatedAt))
                ClearMemoryEditor();
            if (selectedProposal != null && !proposalItems.Any(item => item.Id == selectedProposal.Id &&
                Matches(query, item.Target, item.Title, item.Status, item.Id, item.Validation)))
                SetProposal(null);
            if (selectedAudit != null && !Matches(query, selectedAudit.CreatedAt, selectedAudit.Action, selectedAudit.Result, selectedAudit.Details))
            {
                auditDetails.Clear();
            }
        }

        private static bool Matches(string query, params string[] values)
        {
            if (query.Length == 0) return true;
            return values.Any(value => value != null && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void RestoreSelection<T>(DataGridView grid, string id, Func<T, string> getId) where T : class
        {
            if (string.IsNullOrEmpty(id)) return;
            foreach (DataGridViewRow row in grid.Rows)
            {
                T item = row.DataBoundItem as T;
                if (item != null && getId(item) == id) { row.Selected = true; return; }
            }
        }

        private async Task RefreshProposalsAsync(string selectedId)
        {
            IList<NeonProposalItem> items = await runtime.ListProposalsAsync();
            proposalItems = items == null ? new List<NeonProposalItem>() : items.ToList();
            bool selectedVisible = false;
            loadingRows = true;
            try
            {
                string query = search.Text.Trim();
                proposalGrid.DataSource = proposalItems.Where(item => Matches(query, item.Target, item.Title, item.Status, item.Id, item.Validation)).ToList();
                proposalGrid.ClearSelection();
                if (selectedId != null)
                    foreach (DataGridViewRow row in proposalGrid.Rows)
                    {
                        NeonProposalItem item = row.DataBoundItem as NeonProposalItem;
                        if (item != null && item.Id == selectedId) { row.Selected = true; selectedVisible = true; break; }
                    }
            }
            finally { loadingRows = false; }
            if (selectedId != null && selectedVisible) SetProposal(await runtime.GetProposalAsync(selectedId));
            else SetProposal(null);
        }

        private async Task RefreshAuditAsync()
        {
            IList<NeonAuditItem> items = await runtime.ListAuditAsync();
            auditItems = items == null ? new List<NeonAuditItem>() : items.ToList();
            ApplySearch();
        }

        private void SetProposal(NeonProposalItem item)
        {
            selectedProposal = item;
            proposalInfo.Text = item == null ? "Selecione uma proposta para visualizar o diff completo." :
                "Alvo: " + item.Target + "  |  Estado: " + item.Status + "  |  ID: " + item.Id + "\nSHA-256: " + item.ContentHash;
            diff.Text = item == null ? "" : item.Diff ?? "";
            before.Text = item == null ? "" : item.Before ?? "";
            after.Text = item == null ? "" : item.After ?? "";
            validation.Text = item == null ? "" : item.Validation ?? "Ainda não validada.";
            UpdateActions();
        }

        private void UpdateActions()
        {
            if (generate != null) generate.Enabled = lastStatus != null && lastStatus.ModelConnected && !lastStatus.GuardianLocked;
            if (apply != null) apply.Enabled = selectedProposal != null && selectedProposal.CanApply;
            if (restore != null) restore.Enabled = selectedProposal != null && selectedProposal.CanRestore;
        }

        private async Task RunAsync(Func<Task> work, string completed)
        {
            if (busy || IsDisposed) return;
            busy = true;
            tabs.Enabled = false;
            progress.Visible = true;
            SetMessage("Operação em andamento…");
            try { await work(); if (!IsDisposed) SetMessage(completed); }
            catch (OperationCanceledException) { if (!IsDisposed) SetMessage("Geração cancelada. Nenhuma proposta foi aplicada."); }
            catch (Exception error)
            {
                if (!IsDisposed)
                {
                    SetMessage("Falha: " + error.Message, true);
                    MessageBox.Show(this, error.Message, "CAPRI — operação não concluída", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                busy = false;
                if (!IsDisposed) { tabs.Enabled = true; progress.Visible = false; cancel.Enabled = generation != null; UpdateActions(); }
            }
        }

        private async Task ExportAsync(string filename, Func<Task<string>> load)
        {
            using (SaveFileDialog dialog = new SaveFileDialog { FileName = filename, DefaultExt = "json", Filter = "JSON (*.json)|*.json", OverwritePrompt = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string destination = dialog.FileName;
                await RunAsync(async delegate
                {
                    string json = await load();
                    await Task.Run(() => File.WriteAllText(destination, json, new UTF8Encoding(false)));
                }, "Arquivo exportado para o destino escolhido.");
            }
        }

        private bool Confirm(string text)
        {
            return MessageBox.Show(this, text, "CAPRI — confirmar operação", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private void SetMessage(string text, bool error = false)
        {
            message.Text = text;
            message.ForeColor = error ? Color.FromArgb(255, 173, 151) : Ink;
        }

        private void ClearMemoryEditor()
        {
            editingMemory = null;
            memoryKind.Text = "fato";
            memoryKey.Clear();
            memoryValue.Clear();
            memorySource.Text = "registrado pelo proprietário";
            memoryGrid.ClearSelection();
        }

        private void ClearSensitiveViews()
        {
            memoryItems.Clear();
            proposalItems.Clear();
            auditItems.Clear();
            search.Clear();
            loadingRows = true;
            try
            {
                memoryGrid.DataSource = new List<NeonMemoryItem>();
                proposalGrid.DataSource = new List<NeonProposalItem>();
                auditGrid.DataSource = new List<NeonAuditItem>();
                ClearMemoryEditor();
                SetProposal(null);
                manualContent.Clear();
                importJson.Clear();
                goal.Clear();
                auditDetails.Clear();
            }
            finally { loadingRows = false; }
        }

        private static NeonMemoryItem CopyMemory(NeonMemoryItem item)
        {
            return new NeonMemoryItem { Id = item.Id, Revision = item.Revision, Kind = item.Kind, Key = item.Key,
                Value = item.Value, Source = item.Source, UpdatedAt = item.UpdatedAt };
        }

        private static T Selected<T>(DataGridView grid) where T : class
        {
            return grid.SelectedRows.Count == 0 ? null : grid.SelectedRows[0].DataBoundItem as T;
        }

        private static TableLayoutPanel PanelLayout(int columns, int rows)
        {
            return new TableLayoutPanel { ColumnCount = columns, RowCount = rows, Dock = DockStyle.Fill,
                BackColor = Surface, ForeColor = Ink, Margin = Padding.Empty, GrowStyle = TableLayoutPanelGrowStyle.FixedSize };
        }

        private static TabPage Page(string name) { return new TabPage(name) { BackColor = Surface, ForeColor = Ink }; }

        private static Label Info(string text)
        {
            return new Label { Text = text, Dock = DockStyle.Fill, Padding = new Padding(3), ForeColor = Ink, AutoEllipsis = true };
        }

        private static TextBox Editor(bool multiline, bool readOnly = false)
        {
            return new TextBox { Multiline = multiline, ReadOnly = readOnly, Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(12, 17, 24), ForeColor = Ink, BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = multiline ? ScrollBars.Both : ScrollBars.None, WordWrap = false,
                AcceptsReturn = multiline, AcceptsTab = multiline, MaxLength = multiline ? 1024 * 1024 : 4096 };
        }

        private static FlowLayoutPanel Toolbar()
        {
            return new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true, Padding = new Padding(0, 3, 0, 3), Margin = Padding.Empty, BackColor = Surface };
        }

        private static Button AddButton(FlowLayoutPanel panel, string text, EventHandler click)
        {
            Button button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(88, 29), Padding = new Padding(7, 2, 7, 2), FlatStyle = FlatStyle.Flat,
                ForeColor = Ink, BackColor = Color.FromArgb(32, 43, 55), Margin = new Padding(0, 0, 6, 3) };
            button.FlatAppearance.BorderColor = Color.FromArgb(83, 109, 127);
            button.Click += click;
            panel.Controls.Add(button);
            return button;
        }

        private static DataGridView Grid(params string[] columns)
        {
            DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false,
                ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false,
                BackgroundColor = Surface, BorderStyle = BorderStyle.FixedSingle, EnableHeadersVisualStyles = false };
            grid.DefaultCellStyle.BackColor = Color.FromArgb(24, 31, 42);
            grid.DefaultCellStyle.ForeColor = Ink;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(35, 87, 102);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 47, 61);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            for (int i = 0; i < columns.Length; i += 2)
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = columns[i], DataPropertyName = columns[i + 1], SortMode = DataGridViewColumnSortMode.NotSortable });
            return grid;
        }

        private static void Field(TableLayoutPanel table, int row, string caption, Control editor)
        {
            table.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Ink }, 0, row);
            table.Controls.Add(editor, 1, row);
        }

        private static void AddEditorPage(TabControl tabs, string name, TextBox editor)
        {
            TabPage page = Page(name);
            page.Padding = new Padding(4);
            page.Controls.Add(editor);
            tabs.TabPages.Add(page);
        }
    }
}
