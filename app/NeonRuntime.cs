using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CapriDesktop
{
    // The application owns these capabilities. The language model receives no
    // database handle, credential callback, approval token or Evolver instance.
    internal sealed class NeonRuntime : INeonRuntime, IDisposable
    {
        private readonly string productRoot;
        private readonly string stateRoot;
        private readonly Brain brain;
        private readonly Func<string, bool> confirm;
        private readonly Func<string> authenticate;
        private readonly object memoryGate = new object();
        private CapriStore store;
        private Chronicle chronicle;
        private MemoryCore memory;
        private Guardian guardian;
        private Evolver evolver;
        private EvolutionWorkspace workspace;
        private string startupError;
        private volatile bool disposed;

        internal NeonRuntime(string productRoot, string stateRoot, Brain brain,
            Func<string, bool> confirm, Func<string> authenticate, Func<string> identity = null)
        {
            this.productRoot = Path.GetFullPath(productRoot);
            this.stateRoot = Path.GetFullPath(stateRoot);
            this.brain = brain ?? throw new ArgumentNullException("brain");
            this.confirm = confirm;
            this.authenticate = authenticate;
            try
            {
                EvolutionWorkspace.CheckAncestors(this.stateRoot);
                Directory.CreateDirectory(this.stateRoot);
                store = new CapriStore(Path.Combine(this.stateRoot, "nexus.sqlite"));
                chronicle = new Chronicle(store);
                memory = new MemoryCore(store);
                guardian = new Guardian(store, chronicle, identity);
                workspace = new EvolutionWorkspace(this.productRoot, Path.Combine(this.stateRoot, "evolution"));
                evolver = new Evolver(workspace);
                evolver.Record = delegate(string kind, string details)
                { chronicle.Append(kind, "evolution", Chronicle.Digest(details), "recorded"); };
                chronicle.Append("runtime.started", "guardian", Chronicle.Digest("neon-runtime-v1"), "locked");
                brain.MayChat = delegate { return !disposed && guardian.IsOwner; };
                brain.Recall = Recall;
                brain.Capabilities = CapabilityContext;
                brain.TurnCompleted = RecordTurn;
                ReloadProfile();
            }
            catch (Exception error)
            {
                startupError = error.Message;
                if (store != null) store.Dispose();
                store = null;
            }
        }

        private void Ready()
        {
            if (disposed) throw new ObjectDisposedException("CAPRI NEON SOUL");
            if (startupError != null || store == null) throw new InvalidOperationException("NEXUS indisponível: " + startupError);
            if (!guardian.IsOwner) throw new UnauthorizedAccessException("A sessão Windows não pertence ao proprietário da CAPRI.");
        }
        private void Unlocked()
        {
            Ready();
            if (guardian.Locked) throw new UnauthorizedAccessException("Desbloqueie o Guardian com sua conta Windows antes desta operação.");
        }
        private T Run<T>(string scope, string operation, Func<T> action, Func<bool> active = null)
        {
            Unlocked();
            GuardianGrant grant = guardian.Request(scope, operation, confirm, active);
            T value = default(T);
            bool executed = false;
            string result = guardian.Execute(grant, scope, operation, delegate
            {
                value = action();
                executed = true;
                return "ok";
            }, active);
            if (!executed) throw new UnauthorizedAccessException(string.IsNullOrEmpty(guardian.LastError) ? result : guardian.LastError);
            if (result != "ok") throw new InvalidOperationException(result);
            return value;
        }
        private static Task<T> Work<T>(Func<T> action) { return Task.Run(action); }
        private static Task Work(Action action) { return Task.Run(action); }

        internal string ExecuteTool(string tool, ToolArguments args, Func<bool> active)
        {
            try
            {
                Ready();
                if (!Hands.Valid(tool, args)) return Guardian.Denied;
                string scope = ScopeFor(tool);
                string operation = ToolPermission.Describe(tool, args);
                ToolArguments snapshot = Hands.Read(operation.Substring(operation.IndexOf('\n') + 1));
                if (snapshot == null) return Guardian.Denied;
                GuardianGrant grant = guardian.Request(scope, operation, confirm, active);
                return guardian.Execute(grant, scope, operation, delegate { return Hands.Do(tool, snapshot); }, active);
            }
            catch (Exception) { return "Guardian: operação bloqueada. Abra Memória / Evolução para verificar proprietário, bloqueio e auditoria."; }
        }
        private static string ScopeFor(string tool)
        {
            switch (tool)
            {
                case "open_app": case "app_window": return "apps";
                case "open_website": return "network";
                case "media": case "play_music": return "media";
                case "look_at_screen": return "screen";
                case "use_computer": return "computer";
                default: throw new UnauthorizedAccessException("Ferramenta fora da política do Guardian.");
            }
        }

        internal string Recall(string query)
        {
            try
            {
                Ready();
                // Local memories are never silently transmitted to a cloud provider.
                if (guardian.Locked || !memory.Enabled || brain.Remote != null || !ScopeEnabled("memory")) return "";
                string context = memory.Context(query, 1800);
                if (context.Length > 0) chronicle.Append("memory.recalled", "memory", Chronicle.Digest(query), "selected");
                return context;
            }
            catch (Exception) { return ""; }
        }
        internal string CapabilityContext()
        {
            if (startupError != null || guardian == null) return "Os módulos locais estão indisponíveis; não invente memória, permissões ou evolução.";
            return "Estado real informado pelo software: memória SQLite " + (memory.Enabled ? "habilitada com fatos aprovados" : "desabilitada") +
                "; Guardian " + (guardian.Locked ? "bloqueado" : "autenticado temporariamente") +
                "; Chronicle registra metadados verificáveis; Evolver cria versões de persona e parâmetros com validação, aprovação e restauração. " +
                "Conversar não salva fatos automaticamente. O modelo não aplica mudanças nem controla permissões. Memórias não são enviadas à nuvem. " +
                "A arquitetura de modelos separados e a alteração automática de código não estão ativas.";
        }
        private void RecordTurn(string input, string output)
        {
            try { if (!disposed && chronicle != null) chronicle.Append("chat.completed", "chat", Chronicle.Digest(input), "reply-chars:" + (output ?? "").Length); }
            catch (Exception) { /* UI verification exposes audit failures; tools remain fail-closed. */ }
        }
        private bool ScopeEnabled(string scope)
        {
            bool enabled;
            return guardian.GetScopes().TryGetValue(scope, out enabled) && enabled;
        }
        private void ReloadProfile()
        {
            CapriProfile.Reload(productRoot);
            EvolutionSettings settings = EvolutionSettings.Load(productRoot);
            brain.ResponseTokens = settings.OutputTokens;
            brain.HistoryMessages = settings.HistoryMessages;
            brain.SamplingTemperature = settings.Temperature;
            brain.Forget();
        }

        public Task<NeonRuntimeStatus> GetStatusAsync()
        {
            return Work(delegate
            {
                if (startupError != null || store == null) return new NeonRuntimeStatus
                { GuardianLocked = true, ModelConnected = brain.Model != null, ModelName = brain.Model, StorageStatus = "Indisponível: " + startupError, Owner = "Não verificado" };
                Ready();
                ChronicleCheck audit = chronicle.Verify();
                return new NeonRuntimeStatus
                {
                    Owner = guardian.OwnerSid, GuardianLocked = guardian.Locked,
                    ModelConnected = brain.Model != null, ModelName = brain.Model,
                    MemoryEnabled = memory.Enabled,
                    StorageStatus = audit.Valid ? "SQLite ativo / cadeia verificada (" + audit.Count + " eventos)" : "AUDITORIA INVÁLIDA: " + audit.Error,
                    Scopes = guardian.GetScopes().Select(pair => new NeonScopeItem { Id = pair.Key, Name = pair.Key, Enabled = pair.Value }).ToList()
                };
            });
        }
        public Task SetGuardianLockedAsync(bool locked)
        {
            return Work(delegate
            {
                Ready();
                if (locked) { Lock(); return; }
                if (!guardian.Unlock(authenticate)) throw new UnauthorizedAccessException("O Windows não confirmou o proprietário. Guardian permanece bloqueado.");
            });
        }
        internal void Lock()
        {
            if (guardian != null) guardian.Lock();
            brain.Cancel();
            brain.Forget();
#if !CROSS
            Computer.Interrupt("Guardian bloqueado pelo proprietário.");
#endif
        }
        public Task SetScopeAsync(string scope, bool enabled)
        {
            return Work(delegate
            {
                Unlocked();
                if (enabled && (confirm == null || !confirm("Guardian: habilitar escopo " + scope + " para esta instalação?")))
                    throw new UnauthorizedAccessException("Alteração de escopo cancelada.");
                if (!guardian.SetScope(scope, enabled)) throw new UnauthorizedAccessException(guardian.LastError);
                if (!enabled) { brain.Cancel(); brain.Forget(); }
            });
        }

        public Task SetMemoryEnabledAsync(bool enabled)
        {
            return Work(delegate
            {
                if (!enabled)
                {
                    Unlocked(); memory.SetEnabled(false); brain.Cancel(); brain.Forget();
                    chronicle.Append("memory.disabled", "memory", Chronicle.Digest("opt-out"), "disabled");
                }
                else Run("memory", "memory.opt-in true", delegate { memory.SetEnabled(true); return true; });
            });
        }
        public Task<IList<NeonMemoryItem>> ListMemoryAsync()
        {
            return Work<IList<NeonMemoryItem>>(delegate
            { Ready(); return guardian.Locked || !ScopeEnabled("memory") ? new List<NeonMemoryItem>() : memory.List(500).Select(MemoryItem).ToList(); });
        }
        private static string Revision(MemoryRecord record)
        { return Chronicle.Digest(record.Id + "\n" + record.Title + "\n" + record.Content + "\n" + record.Source + "\n" + record.UpdatedUtc); }
        private static NeonMemoryItem MemoryItem(MemoryRecord item)
        {
            return new NeonMemoryItem { Id = item.Id, Revision = Revision(item), Kind = "fato aprovado", Key = item.Title,
                Value = item.Content, Source = item.Source, UpdatedAt = item.UpdatedUtc };
        }
        public Task<NeonMemoryItem> SaveMemoryAsync(NeonMemoryItem item)
        {
            return Work(delegate
            {
                if (item == null) throw new ArgumentNullException("item");
                string id = item.Id, revision = item.Revision, title = item.Key, content = item.Value, source = item.Source;
                string operation = "memory.save " + (id ?? "novo") + " sha256=" + Chronicle.Digest(title + "\n" + content + "\n" + source);
                return Run("memory", operation, delegate
                {
                    lock (memoryGate)
                    {
                        if (!string.IsNullOrEmpty(id))
                        {
                            MemoryRecord current = memory.Get(id);
                            if (current == null || Revision(current) != revision) throw new InvalidOperationException("A memória foi alterada. Recarregue antes de salvar.");
                        }
                        return MemoryItem(memory.Save(id, title, content, source));
                    }
                });
            });
        }
        public Task DeleteMemoryAsync(string id, string revision)
        {
            return Work(delegate
            {
                Run("memory", "memory.delete " + id + " revisão=" + revision, delegate
                {
                    lock (memoryGate)
                    {
                        MemoryRecord current = memory.Get(id);
                        if (current == null || Revision(current) != revision) throw new InvalidOperationException("A memória foi alterada. Recarregue antes de excluir.");
                        return memory.Delete(id);
                    }
                });
            });
        }
        public Task<string> ExportMemoryAsync()
        {
            return Work(delegate { return Run("memory", "memory.export fatos aprovados (conteúdo pessoal)", delegate { return Json(memory.List(10000)); }); });
        }

        public Task<string[]> ListEvolutionTargetsAsync()
        { return Task.FromResult(new[] { EvolutionWorkspace.PersonaPath, EvolutionWorkspace.SettingsPath }); }
        public Task<string> ReadEvolutionTargetAsync(string target)
        {
            return Work(delegate
            {
                Unlocked();
                if (!ScopeEnabled("evolution")) throw new UnauthorizedAccessException("Escopo evolution bloqueado.");
                byte[] bytes = workspace.ReadProduct(target);
                return bytes == null && target == EvolutionWorkspace.SettingsPath ? "{\"outputTokens\":384,\"historyMessages\":12,\"temperature\":0.8}" : bytes == null ? "" : new UTF8Encoding(false, true).GetString(bytes);
            });
        }
        public Task<IList<NeonProposalItem>> ListProposalsAsync()
        {
            return Work<IList<NeonProposalItem>>(delegate
            { Ready(); return guardian.Locked || !ScopeEnabled("evolution") ? new List<NeonProposalItem>() : evolver.List().Select(item => ProposalItem(item.Id)).ToList(); });
        }
        public Task<NeonProposalItem> GetProposalAsync(string id)
        { return Work(delegate { Unlocked(); return ProposalItem(id); }); }
        private NeonProposalItem ProposalItem(string id)
        {
            EvolutionPreview preview = evolver.Preview(id);
            EvolutionProposal proposal = preview.Proposal;
            return new NeonProposalItem { Id = proposal.Id, Target = proposal.Path, Title = proposal.Title, Status = proposal.Status,
                ContentHash = proposal.ProposalSha256, Before = preview.Before, After = preview.After, Diff = preview.Diff,
                Validation = proposal.ValidationMessage, CreatedAt = proposal.CreatedUtc,
                CanApply = proposal.Status == "Validated", CanRestore = proposal.Status == "Applied" };
        }
        public Task<NeonProposalItem> CreateProposalAsync(string target, string goal, CancellationToken cancellation)
        {
            return Work(delegate
            {
                Unlocked(); workspace.ProductPath(target);
                if (brain.Model == null) throw new InvalidOperationException("Conecte um modelo no Mainframe; versões manuais continuam disponíveis offline.");
                Brain drafting = new Brain { Model = brain.Model, Local = brain.Local, LocalPick = brain.LocalPick,
                    Remote = brain.Remote, SamplingTemperature = 0.2, MayChat = delegate { return guardian.IsOwner && !guardian.Locked; } };
                using (cancellation.Register(drafting.Cancel))
                {
                    Func<bool> active = delegate { return !cancellation.IsCancellationRequested && !disposed && !guardian.Locked; };
                    return Run("evolution", "evolution.generate " + target + " objetivo-sha256=" + Chronicle.Digest(goal), delegate
                    {
                        cancellation.ThrowIfCancellationRequested();
                        EvolutionProposal proposal = evolver.Draft("Altere somente " + target + ". Objetivo do proprietário: " + goal, delegate(string input)
                        {
                            string raw = drafting.Draft("Você propõe melhorias supervisionadas. Retorne apenas o objeto JSON solicitado, sem ferramentas, sem autorizações e sem alegar testes executados.", input, 4096);
                            cancellation.ThrowIfCancellationRequested();
                            if (!active()) throw new UnauthorizedAccessException("A sessão encerrou durante a geração.");
                            return raw;
                        });
                        if (proposal.Path != target) throw new InvalidDataException("O modelo propôs outro alvo; revise a proposta antes de validar.");
                        return ProposalItem(proposal.Id);
                    }, active);
                }
            });
        }
        public Task<NeonProposalItem> CreateManualProposalAsync(string target, string content, string goal)
        {
            return ImportProposalAsync("{\"title\":" + Voice.Quote("Versão manual: " + target) + ",\"reason\":" + Voice.Quote(goal ?? "Edição explícita do proprietário") +
                ",\"path\":" + Voice.Quote(target) + ",\"content\":" + Voice.Quote(content) + "}");
        }
        public Task<NeonProposalItem> ImportProposalAsync(string json)
        {
            return Work(delegate { return Run("evolution", "evolution.import sha256=" + Chronicle.Digest(json),
                delegate { return ProposalItem(evolver.ImportJson(json).Id); }); });
        }
        public Task<NeonProposalItem> ValidateProposalAsync(string id)
        {
            return Work(delegate { return Run("evolution", "evolution.validate " + id,
                delegate { evolver.Validate(id); return ProposalItem(id); }); });
        }
        public Task<NeonProposalItem> ApplyProposalAsync(string id, string expectedHash)
        {
            return Work(delegate
            {
                Ready();
                // Applying a version always requires fresh Windows authentication.
                if (!guardian.Unlock(authenticate)) throw new UnauthorizedAccessException("A aplicação exige autenticação atual do proprietário.");
                return Run("evolution", "evolution.apply " + id + " sha256=" + expectedHash, delegate
                {
                    string approval = evolver.Approve(id, expectedHash, delegate { return !guardian.Locked && guardian.IsOwner; });
                    if (approval == null) throw new UnauthorizedAccessException("Aprovação da versão ausente.");
                    evolver.Apply(id, approval); ReloadProfile(); return ProposalItem(id);
                });
            });
        }
        public Task<NeonProposalItem> RestoreProposalAsync(string id)
        {
            return Work(delegate
            {
                Ready();
                if (!guardian.Unlock(authenticate)) throw new UnauthorizedAccessException("A restauração exige autenticação atual do proprietário.");
                return Run("evolution", "evolution.rollback " + id, delegate
                {
                    if (!evolver.Rollback(id, delegate { return !guardian.Locked && guardian.IsOwner; })) throw new UnauthorizedAccessException("Restauração cancelada.");
                    ReloadProfile(); return ProposalItem(id);
                });
            });
        }
        public Task<string> ExportProposalAsync(string id)
        { return Work(delegate { return Run("evolution", "evolution.export " + id, delegate { return evolver.ExportJson(id); }); }); }

        public Task<IList<NeonAuditItem>> ListAuditAsync()
        {
            return Work<IList<NeonAuditItem>>(delegate
            {
                Ready();
                return guardian.Locked ? new List<NeonAuditItem>() : chronicle.Read(500).Select(item => new NeonAuditItem
                { Id = item.Sequence.ToString(CultureInfo.InvariantCulture), CreatedAt = item.AtUtc, Action = item.Kind,
                    Result = item.Outcome, Details = item.Scope + " / sha256=" + item.OperationHash }).ToList();
            });
        }
        public Task<string> VerifyAuditAsync()
        {
            return Work(delegate { Ready(); ChronicleCheck result = chronicle.Verify();
                return result.Valid ? "PASS: " + result.Count + " eventos, hashes e âncora íntegros." : "FALHA: " + result.Error; });
        }
        public Task<string> ExportAuditAsync()
        { return Work(delegate { return Run("memory", "chronicle.export metadados de auditoria", delegate { return Json(chronicle.Read(10000)); }); }); }
        private static string Json<T>(T value)
        {
            using (MemoryStream stream = new MemoryStream())
            { new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value); return Encoding.UTF8.GetString(stream.ToArray()); }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { if (guardian != null) guardian.Lock(); } catch (Exception) { }
            if (store != null) store.Dispose();
        }
    }
}
