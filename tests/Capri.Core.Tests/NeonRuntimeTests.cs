using System;
using System.IO;
using System.Linq;
using System.Text;
using CapriDesktop;

internal static class NeonRuntimeTests
{
    private static bool Refuses(Action action)
    {
        try { action(); return false; }
        catch (Exception) { return true; }
    }

    internal static void Run(Action<bool, string> check)
    {
        string folder = Path.Combine(Path.GetTempPath(), "capri-runtime-" + Guid.NewGuid().ToString("N"));
        string product = Path.Combine(folder, "product"), state = Path.Combine(folder, "state");
        Directory.CreateDirectory(Path.Combine(product, "CAPRI"));
        string personaPath = Path.Combine(product, "CAPRI", "PERSONA_DESKTOP.txt");
        string original = "Você é CAPRI. Converse em português brasileiro com clareza e respeito.";
        File.WriteAllText(personaPath, original, new UTF8Encoding(false));
        string owner = "runtime-owner", identity = owner;
        bool approve = true;
        int authentications = 0;
        Func<string> authenticate = delegate { authentications++; return owner; };
        Func<string, bool> confirm = delegate { return approve; };
        Brain brain = new Brain();
        string memoryId;
        try
        {
            using (NeonRuntime runtime = new NeonRuntime(product, state, brain, confirm, authenticate, delegate { return identity; }))
            {
                NeonRuntimeStatus status = runtime.GetStatusAsync().GetAwaiter().GetResult();
                check(status.GuardianLocked && !status.ModelConnected && status.StorageStatus.Contains("SQLite"), "runtime exposes real SQLite and locked Guardian while model is offline");
                check(runtime.ListMemoryAsync().GetAwaiter().GetResult().Count == 0, "locked runtime does not expose private memory");
                check(Refuses(delegate { runtime.SetMemoryEnabledAsync(true).GetAwaiter().GetResult(); }), "runtime cannot enable memory before owner authentication");
                runtime.SetGuardianLockedAsync(false).GetAwaiter().GetResult();
                check(authentications == 1 && !runtime.GetStatusAsync().GetAwaiter().GetResult().GuardianLocked, "runtime unlock delegates actual owner authentication");
                check(Refuses(delegate { runtime.SaveMemoryAsync(new NeonMemoryItem { Key = "Projeto", Value = "CAPRI" }).GetAwaiter().GetResult(); }), "runtime refuses storing memory until explicit opt-in");
                runtime.SetMemoryEnabledAsync(true).GetAwaiter().GetResult();
                NeonMemoryItem item = runtime.SaveMemoryAsync(new NeonMemoryItem { Key = "Codinome", Value = "Meu codinome é Aurora", Source = "pedido explícito" }).GetAwaiter().GetResult();
                memoryId = item.Id;
                check(!string.IsNullOrEmpty(item.Revision) && runtime.Recall("Qual é meu codinome?").Contains("Aurora"), "approved persisted memory reaches the actual Brain recall callback");
                runtime.SetScopeAsync("memory", false).GetAwaiter().GetResult();
                check(runtime.Recall("codinome") == "" && runtime.ListMemoryAsync().GetAwaiter().GetResult().Count == 0, "revoking memory scope blocks recall and memory UI data");
                runtime.SetScopeAsync("memory", true).GetAwaiter().GetResult();
                brain.Remote = new Cloud("sk-test-only", "test-model");
                check(runtime.Recall("codinome") == "", "local memories are not silently forwarded to a cloud provider");
                brain.Remote = null;
                approve = false;
                check(Refuses(delegate { runtime.SaveMemoryAsync(new NeonMemoryItem { Key = "Recusado", Value = "não deve salvar" }).GetAwaiter().GetResult(); }), "runtime cancellation prevents actual memory mutation");
                approve = true;
                check(runtime.ListMemoryAsync().GetAwaiter().GetResult().Count == 1, "cancelled mutation creates no phantom memory");
                identity = "other-owner";
                check(Refuses(delegate { runtime.ListMemoryAsync().GetAwaiter().GetResult(); }) && !brain.MayChat(), "runtime rejects a different Windows identity in UI and chat");
                identity = owner;
            }
            using (NeonRuntime runtime = new NeonRuntime(product, state, brain, confirm, authenticate, delegate { return identity; }))
            {
                check(runtime.GetStatusAsync().GetAwaiter().GetResult().GuardianLocked, "restarting runtime revokes old Guardian authentication");
                runtime.SetGuardianLockedAsync(false).GetAwaiter().GetResult();
                NeonMemoryItem item = runtime.ListMemoryAsync().GetAwaiter().GetResult().Single();
                check(item.Id == memoryId && item.Value.Contains("Aurora"), "SQLite memory survives a complete runtime restart");
                string oldRevision = item.Revision;
                item.Value = "Meu codinome é Boreal";
                NeonMemoryItem updated = runtime.SaveMemoryAsync(item).GetAwaiter().GetResult();
                check(updated.Revision != oldRevision, "editing a memory creates a new concurrency revision");
                check(Refuses(delegate { runtime.DeleteMemoryAsync(item.Id, oldRevision).GetAwaiter().GetResult(); }), "stale memory delete is refused without losing the new value");
                check(runtime.ExportMemoryAsync().GetAwaiter().GetResult().Contains("Boreal"), "owner can explicitly export stored memory");

                string revisedPersona = "Você é CAPRI. Fale português brasileiro com clareza. Esta é uma versão revisada e aprovada.";
                NeonProposalItem proposal = runtime.CreateManualProposalAsync("CAPRI/PERSONA_DESKTOP.txt", revisedPersona, "Teste controlado de evolução").GetAwaiter().GetResult();
                check(proposal.Status == "Proposed" && File.ReadAllText(personaPath) == original, "creating an evolution proposal never changes the running product");
                proposal = runtime.ValidateProposalAsync(proposal.Id).GetAwaiter().GetResult();
                check(proposal.CanApply && proposal.Validation.Contains("quality was not measured"), "runtime surfaces actual structural validation without inventing model quality");
                check(Refuses(delegate { runtime.ApplyProposalAsync(proposal.Id, "wrong-hash").GetAwaiter().GetResult(); }) && File.ReadAllText(personaPath) == original, "runtime refuses applying a different proposal hash");
                int beforeAuth = authentications;
                proposal = runtime.ApplyProposalAsync(proposal.Id, proposal.ContentHash).GetAwaiter().GetResult();
                check(proposal.Status == "Applied" && proposal.CanRestore && authentications > beforeAuth, "version application requires fresh authentication and persists applied state");
                check(File.ReadAllText(personaPath) == revisedPersona && CapriProfile.Persona == revisedPersona, "applied evolution changes and reloads the actual runtime persona");
                check(runtime.ExportProposalAsync(proposal.Id).GetAwaiter().GetResult().Contains("content"), "version export uses the real Evolver interchange schema");
                proposal = runtime.RestoreProposalAsync(proposal.Id).GetAwaiter().GetResult();
                check(File.ReadAllText(personaPath) == original && CapriProfile.Persona == original, "runtime rollback restores original bytes and live personality");

                string settings = "{\"outputTokens\":512,\"historyMessages\":16,\"temperature\":0.4}";
                NeonProposalItem configuration = runtime.CreateManualProposalAsync("CAPRI/EVOLUTION_SETTINGS.json", settings, "Teste dos parâmetros reais").GetAwaiter().GetResult();
                configuration = runtime.ValidateProposalAsync(configuration.Id).GetAwaiter().GetResult();
                runtime.ApplyProposalAsync(configuration.Id, configuration.ContentHash).GetAwaiter().GetResult();
                check(brain.ResponseTokens == 512 && brain.HistoryMessages == 16 && brain.SamplingTemperature == 0.4, "validated configuration evolution controls actual Brain generation settings");
                runtime.RestoreProposalAsync(configuration.Id).GetAwaiter().GetResult();
                check(brain.ResponseTokens == 384 && brain.HistoryMessages == 12, "configuration rollback restores prior effective settings");
                check(runtime.VerifyAuditAsync().GetAwaiter().GetResult().StartsWith("PASS"), "complete memory and evolution UI flow leaves a verifiable audit chain");
                string audit = runtime.ExportAuditAsync().GetAwaiter().GetResult();
                check(audit.Contains("evolution") && !audit.Contains("Aurora") && !audit.Contains("Boreal"), "Chronicle export records operations without storing private memory contents");
                runtime.SetMemoryEnabledAsync(false).GetAwaiter().GetResult();
                check(runtime.Recall("codinome") == "", "opting out stops retrieval without silently deleting approved facts");
                runtime.DeleteMemoryAsync(updated.Id, updated.Revision).GetAwaiter().GetResult();
                check(runtime.ListMemoryAsync().GetAwaiter().GetResult().Count == 0, "owner can explicitly remove the exact current memory revision");
                runtime.Lock();
                check(runtime.Recall("codinome") == "" && runtime.ListProposalsAsync().GetAwaiter().GetResult().Count == 0, "locking the runtime removes memory and proposal data from active UI access");
            }
        }
        finally
        {
            CapriProfile.Reload(AppDomain.CurrentDomain.BaseDirectory);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }
}
