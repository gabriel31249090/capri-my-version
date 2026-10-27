using System;
using System.IO;
using System.Text;
using System.Text.Json;
using CapriDesktop;

internal static class EvolutionTests
{
    private const string Original = "Você é CAPRI. Responda em português brasileiro com precisão e curiosidade.";
    private const string Improved = "Você é CAPRI NEON SOUL. Responda em português brasileiro com clareza, precisão e humor cuidadoso. Ações exigem autorização do proprietário.";
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

    internal static void Run(Action<bool, string> check)
    {
        string temporary = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "capri-evolution-tests-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(temporary);
        try
        {
            Lifecycle(temporary, check);
            Settings(temporary, check);
            Integrity(temporary, check);
            Recovery(temporary, check);
            Scope(temporary, check);
            Model(temporary, check);
        }
        finally
        {
            // Delete only this unique test directory, never a path supplied by a model.
            string testRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (temporary.StartsWith(testRoot, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(temporary).StartsWith("capri-evolution-tests-", StringComparison.Ordinal))
                Directory.Delete(temporary, true);
        }
    }

    private static EvolutionWorkspace Workspace(string temporary, string name)
    {
        string product = Path.Combine(temporary, name, "product");
        Directory.CreateDirectory(Path.Combine(product, "CAPRI"));
        File.WriteAllText(Path.Combine(product, "CAPRI", "PERSONA_DESKTOP.txt"), Original, Utf8);
        return new EvolutionWorkspace(product, Path.Combine(temporary, name, "state"));
    }

    private static string Proposal(string path, string content)
    { return JsonSerializer.Serialize(new { title = "Melhoria revisável", reason = "Objetivo explícito do proprietário", path, content }); }

    private static bool Refuses(Action action)
    {
        try { action(); return false; }
        catch (IOException) { return true; }
        catch (InvalidDataException) { return true; }
        catch (InvalidOperationException) { return true; }
        catch (ArgumentException) { return true; }
        catch (FormatException) { return true; }
    }

    private static string Persona(EvolutionWorkspace workspace)
    { return File.ReadAllText(workspace.ProductPath(EvolutionWorkspace.PersonaPath), Utf8); }

    private static void Lifecycle(string temporary, Action<bool, string> check)
    {
        EvolutionWorkspace workspace = Workspace(temporary, "lifecycle");
        Evolver evolver = new Evolver(workspace);
        EvolutionProposal proposal = evolver.ImportJson(Proposal(EvolutionWorkspace.PersonaPath, Improved));
        check(proposal.Version == 1 && proposal.Status == "Proposed" && Persona(workspace) == Original, "evolution proposal is versioned and leaves product unchanged");
        EvolutionPreview preview = evolver.Preview(proposal.Id);
        check(preview.Before == Original && preview.After == Improved && preview.Diff.Contains("-" + Original) && preview.Diff.Contains("+" + Improved), "evolution preview contains the exact before/after diff including UTF-8 accents");
        using (JsonDocument exported = JsonDocument.Parse(evolver.ExportJson(proposal.Id)))
            check(exported.RootElement.GetProperty("content").GetString() == Improved && exported.RootElement.GetProperty("path").GetString() == EvolutionWorkspace.PersonaPath, "proposal JSON export preserves candidate content in the strict import schema");
        check(Refuses(() => evolver.Approve(proposal.Id, proposal.ProposalSha256, _ => true)), "unvalidated evolution cannot be approved");
        check(evolver.Validate(proposal.Id).Passed && Persona(workspace) == Original, "evolution validates only an isolated copy before approval");
        EvolutionValidationReport structural = evolver.Get(proposal.Id).ValidationReport;
        check(structural.BeforeBytes == Utf8.GetByteCount(Original) && structural.AfterBytes == Utf8.GetByteCount(Improved) && structural.PortugueseDirective && structural.Limitation.Contains("quality"), "persisted evolution comparison measures byte sizes and invariants without claiming better model quality");
        check(Directory.GetDirectories(workspace.StatePath(proposal.Id), "validation-*").Length == 1, "evolution validation preserves its isolated test artifact");
        check(Refuses(() => evolver.Approve(proposal.Id, new string('0', 64), _ => true)), "approval rejects a different proposal SHA256");
        check(evolver.Approve(proposal.Id, proposal.ProposalSha256, _ => false) == null && evolver.Get(proposal.Id).Status == "Validated", "declined evolution changes no product or approval state");
        string reviewed = null;
        string token = evolver.Approve(proposal.Id, proposal.ProposalSha256, text => { reviewed = text; return true; });
        check(token != null && reviewed.Contains(proposal.ProposalSha256) && reviewed.Contains(Improved), "human approval receives the exact digest and proposed content");
        check(Refuses(() => evolver.Apply(proposal.Id, "wrong-token")) && Persona(workspace) == Original, "wrong evolution authorization token cannot apply");
        evolver.Apply(proposal.Id, token);
        check(Persona(workspace) == Improved && evolver.Get(proposal.Id).Status == "Applied", "approved declarative evolution really updates the product");
        check(Refuses(() => evolver.Apply(proposal.Id, token)), "consumed evolution approval cannot be replayed");
        check(File.ReadAllText(workspace.StatePath(proposal.Id + "/before.bin"), Utf8) == Original, "evolution preserves the original backup after application");
        Evolver restarted = new Evolver(new EvolutionWorkspace(workspace.ProductRoot, workspace.StateRoot));
        check(restarted.Get(proposal.Id).Status == "Applied" && restarted.List().Length == 1, "applied evolution and its version history survive restart");
        check(!restarted.Rollback(proposal.Id, _ => false) && Persona(workspace) == Improved, "rollback also requires explicit human consent");
        check(restarted.Rollback(proposal.Id, _ => true) && Persona(workspace) == Original, "approved rollback restores the exact original UTF-8 bytes");
        check(Refuses(() => restarted.Rollback(proposal.Id, _ => true)), "completed rollback cannot be replayed");
        EvolutionProposal second = restarted.ImportJson(Proposal(EvolutionWorkspace.PersonaPath, Improved + " Use exemplos concretos."));
        restarted.Validate(second.Id);
        string obsoleteToken = restarted.Approve(second.Id, second.ProposalSha256, _ => true);
        Evolver nextSession = new Evolver(new EvolutionWorkspace(workspace.ProductRoot, workspace.StateRoot));
        check(second.Version == 2 && nextSession.Get(second.Id).Status == "Validated" && Refuses(() => nextSession.Apply(second.Id, obsoleteToken)), "restart preserves proposals but invalidates old session authorizations");
    }

    private static void Settings(string temporary, Action<bool, string> check)
    {
        EvolutionWorkspace workspace = Workspace(temporary, "settings");
        Evolver evolver = new Evolver(workspace);
        string json = "{\"outputTokens\":512,\"historyMessages\":16,\"temperature\":0.6}";
        EvolutionProposal proposal = evolver.ImportJson(Proposal(EvolutionWorkspace.SettingsPath, json));
        EvolutionValidation validation = evolver.Validate(proposal.Id);
        check(validation.Passed && validation.Report.BeforeOutputTokens == 384 && validation.Report.AfterOutputTokens == 512, "typed generation settings validate in a copy and compare baseline/candidate bounds");
        string token = evolver.Approve(proposal.Id, proposal.ProposalSha256, _ => true);
        evolver.Apply(proposal.Id, token);
        EvolutionSettings loaded = EvolutionSettings.Load(workspace.ProductRoot);
        check(loaded.OutputTokens == 512 && loaded.HistoryMessages == 16 && loaded.Temperature == 0.6, "applied generation settings load as bounded typed runtime values");
        evolver.Rollback(proposal.Id, _ => true);
        check(!File.Exists(workspace.ProductPath(EvolutionWorkspace.SettingsPath)), "rollback deletes a settings file that did not exist before evolution");
        check(EvolutionSettings.Load(workspace.ProductRoot).OutputTokens == 384, "missing settings load documented safe defaults");
        string[] invalid = {
            "{\"outputTokens\":999999,\"historyMessages\":12,\"temperature\":0.8}",
            "{\"outputTokens\":384,\"historyMessages\":3,\"temperature\":0.8}",
            "{\"outputTokens\":384,\"historyMessages\":12,\"temperature\":-0.1}",
            "{\"outputTokens\":384,\"historyMessages\":12,\"temperature\":0.8,\"execute\":1}",
            "{\"outputTokens\":384,\"outputTokens\":512,\"historyMessages\":12,\"temperature\":0.8}",
            "{\"outputTokens\":384,\"historyMessages\":12,\"temperature\":NaN}"
        };
        foreach (string candidate in invalid)
        {
            EvolutionProposal bad = evolver.ImportJson(Proposal(EvolutionWorkspace.SettingsPath, candidate));
            check(!evolver.Validate(bad.Id).Passed && Refuses(() => evolver.Approve(bad.Id, bad.ProposalSha256, _ => true)), "invalid or extended typed settings cannot reach approval");
        }
        File.WriteAllBytes(workspace.ProductPath(EvolutionWorkspace.SettingsPath), new byte[] { 0xC3, 0x28 });
        check(EvolutionSettings.Load(workspace.ProductRoot).HistoryMessages == 12, "invalid UTF-8 settings fall back without crashing runtime");
    }

    private static void Integrity(string temporary, Action<bool, string> check)
    {
        EvolutionWorkspace concurrent = Workspace(temporary, "concurrent");
        Evolver evolver = new Evolver(concurrent);
        EvolutionProposal proposal = evolver.ImportJson(Proposal(EvolutionWorkspace.PersonaPath, Improved));
        File.WriteAllText(concurrent.ProductPath(EvolutionWorkspace.PersonaPath), Original + " Edição externa.", Utf8);
        check(!evolver.Validate(proposal.Id).Passed && Persona(concurrent).EndsWith("Edição externa."), "concurrent product edits invalidate validation without being overwritten");

        EvolutionWorkspace snapshot = Workspace(temporary, "tamper-snapshot");
        Evolver protectedEvolver = new Evolver(snapshot);
        EvolutionProposal tampered = protectedEvolver.ImportJson(Proposal(EvolutionWorkspace.PersonaPath, Improved));
        protectedEvolver.Validate(tampered.Id);
        string token = protectedEvolver.Approve(tampered.Id, tampered.ProposalSha256, _ => true);
        File.AppendAllText(snapshot.StatePath(tampered.Id + "/after.bin"), " alteração", Utf8);
        check(Refuses(() => protectedEvolver.Apply(tampered.Id, token)) && Persona(snapshot) == Original, "candidate tampering after approval fails SHA256 verification");

        EvolutionWorkspace state = Workspace(temporary, "tamper-state");
        Evolver signed = new Evolver(state);
        EvolutionProposal record = signed.ImportJson(Proposal(EvolutionWorkspace.PersonaPath, Improved));
        string statePath = state.StatePath(record.Id + "/state.json");
        using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(statePath)))
        {
            string payload = document.RootElement.GetProperty("Payload").GetString();
            File.WriteAllText(statePath, JsonSerializer.Serialize(new { Payload = payload, Mac = Convert.ToBase64String(new byte[32]) }), Utf8);
        }
        check(Refuses(() => signed.Get(record.Id)) && Persona(state) == Original, "modified approval/state metadata fails its HMAC integrity check");

        EvolutionWorkspace audit = Workspace(temporary, "audit-failure");
        Evolver audited = new Evolver(audit);
        EvolutionProposal waiting = audited.ImportJson(Proposal(EvolutionWorkspace.PersonaPath, Improved));
        audited.Validate(waiting.Id);
        string approved = audited.Approve(waiting.Id, waiting.ProposalSha256, _ => true);
        audited.Record = delegate { throw new IOException("audit unavailable"); };
        check(Refuses(() => audited.Apply(waiting.Id, approved)) && Persona(audit) == Original, "audit persistence failure prevents evolution application");

        EvolutionWorkspace duringReview = Workspace(temporary, "review-conflict");
        Evolver reviewing = new Evolver(duringReview);
        EvolutionProposal reviewed = reviewing.ImportJson(Proposal(EvolutionWorkspace.PersonaPath, Improved));
        reviewing.Validate(reviewed.Id);
        check(Refuses(() => reviewing.Approve(reviewed.Id, reviewed.ProposalSha256, _ => {
            File.WriteAllText(duringReview.ProductPath(EvolutionWorkspace.PersonaPath), Original + " Concorrência.", Utf8);
            return true;
        })), "edits during the human approval dialog invalidate that approval");

        EvolutionWorkspace replay = Workspace(temporary, "metadata-replay");
        Evolver once = new Evolver(replay);
        EvolutionProposal one = once.ImportJson(Proposal(EvolutionWorkspace.PersonaPath, Improved));
        once.Validate(one.Id);
        string oneToken = once.Approve(one.Id, one.ProposalSha256, _ => true);
        string signedStatePath = replay.StatePath(one.Id + "/state.json");
        byte[] oldSignedState = File.ReadAllBytes(signedStatePath);
        once.Apply(one.Id, oneToken);
        File.WriteAllBytes(signedStatePath, oldSignedState);
        File.WriteAllText(replay.ProductPath(EvolutionWorkspace.PersonaPath), Original, Utf8);
        check(Refuses(() => once.Apply(one.Id, oneToken)) && Persona(replay) == Original, "durable consumption tombstone rejects replay of previously signed approval metadata");

        EvolutionWorkspace rollback = Workspace(temporary, "rollback-conflict");
        Evolver reversing = new Evolver(rollback);
        EvolutionProposal applied = reversing.ImportJson(Proposal(EvolutionWorkspace.PersonaPath, Improved));
        reversing.Validate(applied.Id);
        reversing.Apply(applied.Id, reversing.Approve(applied.Id, applied.ProposalSha256, _ => true));
        File.WriteAllText(rollback.ProductPath(EvolutionWorkspace.PersonaPath), Improved + " Mudança posterior.", Utf8);
        check(Refuses(() => reversing.Rollback(applied.Id, _ => true)) && Persona(rollback).EndsWith("Mudança posterior."), "rollback never overwrites changes made after application");
        File.WriteAllText(rollback.ProductPath(EvolutionWorkspace.PersonaPath), Improved, Utf8);
        File.AppendAllText(rollback.StatePath(applied.Id + "/before.bin"), " backup alterado", Utf8);
        check(Refuses(() => reversing.Rollback(applied.Id, _ => true)) && Persona(rollback) == Improved, "modified preserved backup cannot be restored by rollback");

        EvolutionWorkspace bom = Workspace(temporary, "bom-rollback");
        File.WriteAllText(bom.ProductPath(EvolutionWorkspace.PersonaPath), Original, new UTF8Encoding(true));
        byte[] originalBytes = File.ReadAllBytes(bom.ProductPath(EvolutionWorkspace.PersonaPath));
        Evolver preserving = new Evolver(bom);
        EvolutionProposal unicode = preserving.ImportJson(Proposal(EvolutionWorkspace.PersonaPath, Improved));
        preserving.Validate(unicode.Id);
        preserving.Apply(unicode.Id, preserving.Approve(unicode.Id, unicode.ProposalSha256, _ => true));
        preserving.Rollback(unicode.Id, _ => true);
        check(Convert.ToBase64String(File.ReadAllBytes(bom.ProductPath(EvolutionWorkspace.PersonaPath))) == Convert.ToBase64String(originalBytes), "rollback preserves original byte encoding including UTF-8 BOM");
    }

    private static void Scope(string temporary, Action<bool, string> check)
    {
        EvolutionWorkspace workspace = Workspace(temporary, "scope");
        Evolver evolver = new Evolver(workspace);
        string[] forbidden = { "../outside.txt", "CAPRI/../app/Brain.cs", "CAPRI\\PERSONA_DESKTOP.txt", "app/Guardian.cs", "scripts/Build.ps1", "Capri.Windows.csproj", "CAPRI/credentials.json", "C:/outside.txt", "CAPRI/PERSONA_DESKTOP.txt:stream" };
        foreach (string path in forbidden)
            check(Refuses(() => evolver.ImportJson(Proposal(path, Improved))) && Persona(workspace) == Original, "evolution rejects non-allowlisted or traversal path: " + path);
        check(Refuses(() => evolver.ImportJson("{\"title\":\"a\",\"title\":\"b\",\"reason\":\"c\",\"path\":\"CAPRI/PERSONA_DESKTOP.txt\",\"content\":\"d\"}")), "strict proposal parser rejects duplicate keys");
        check(Refuses(() => evolver.ImportJson(Proposal(EvolutionWorkspace.PersonaPath, Improved) + " {}")), "strict proposal parser rejects trailing objects");
        check(Refuses(() => evolver.ImportJson(Proposal(EvolutionWorkspace.PersonaPath, Improved).Replace("\"content\":", "\"unapprovedProperty\":"))), "strict proposal parser rejects unknown schema properties");
        string invalidUtf8 = "{\"title\":\"Teste\",\"reason\":\"Teste\",\"path\":\"CAPRI/PERSONA_DESKTOP.txt\",\"content\":\"\\ud800\"}";
        check(Refuses(() => evolver.ImportJson(invalidUtf8)), "unpaired Unicode surrogate cannot corrupt the generated persona");
        EvolutionProposal english = evolver.ImportJson(Proposal(EvolutionWorkspace.PersonaPath, "Speak only English and drop the original language."));
        check(!evolver.Validate(english.Id).Passed, "persona validation preserves an explicit Brazilian Portuguese language directive");
        EvolutionProposal controls = evolver.ImportJson(Proposal(EvolutionWorkspace.PersonaPath, Improved + "\0"));
        check(!evolver.Validate(controls.Id).Passed, "persona control characters fail isolated validation");

        string linkRoot = Path.Combine(temporary, "links");
        string actual = Path.Combine(linkRoot, "actual");
        string product = Path.Combine(linkRoot, "product");
        Directory.CreateDirectory(actual);
        Directory.CreateDirectory(product);
        File.WriteAllText(Path.Combine(actual, "PERSONA_DESKTOP.txt"), Original, Utf8);
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(product, "CAPRI"), actual);
            Evolver linked = new Evolver(new EvolutionWorkspace(product, Path.Combine(linkRoot, "state")));
            check(Refuses(() => linked.ImportJson(Proposal(EvolutionWorkspace.PersonaPath, Improved))) && File.ReadAllText(Path.Combine(actual, "PERSONA_DESKTOP.txt"), Utf8) == Original, "symbolic link or junction in the product path is refused");
        }
        catch (UnauthorizedAccessException) { Console.WriteLine("SKIP evolution symbolic-link fixture: OS privilege unavailable."); }
        catch (System.ComponentModel.Win32Exception) { Console.WriteLine("SKIP evolution symbolic-link fixture: OS does not permit the fixture."); }
        catch (IOException error)
        {
            if ((error.HResult & 65535) != 1314) throw;
            Console.WriteLine("SKIP evolution symbolic-link fixture: Windows error 1314, privilege unavailable.");
        }
    }

    private static void Recovery(string temporary, Action<bool, string> check)
    {
        // Simulate power loss at persisted transaction boundaries, without running untrusted code.
        EvolutionWorkspace committed = Workspace(temporary, "recover-commit");
        Evolver writer = new Evolver(committed);
        EvolutionProposal proposal = writer.ImportJson(Proposal(EvolutionWorkspace.PersonaPath, Improved));
        writer.Validate(proposal.Id);
        writer.Approve(proposal.Id, proposal.ProposalSha256, _ => true);
        EvolutionProposal pending = committed.Load<EvolutionProposal>(proposal.Id + "/state.json");
        pending.Status = "Applying";
        pending.ApprovalHash = null;
        committed.Save(proposal.Id + "/state.json", pending);
        File.WriteAllText(committed.ProductPath(EvolutionWorkspace.PersonaPath), Improved, Utf8);
        Evolver rebooted = new Evolver(new EvolutionWorkspace(committed.ProductRoot, committed.StateRoot));
        check(rebooted.Get(proposal.Id).Status == "Applied", "restart recovers an application committed before final state persistence");
        EvolutionProposal reversing = committed.Load<EvolutionProposal>(proposal.Id + "/state.json");
        reversing.Status = "RollingBack";
        committed.Save(proposal.Id + "/state.json", reversing);
        File.WriteAllText(committed.ProductPath(EvolutionWorkspace.PersonaPath), Original, Utf8);
        check(new Evolver(new EvolutionWorkspace(committed.ProductRoot, committed.StateRoot)).Get(proposal.Id).Status == "RolledBack", "restart recovers rollback committed before its final status persistence");

        EvolutionWorkspace interrupted = Workspace(temporary, "recover-interrupted");
        Evolver beforeCommit = new Evolver(interrupted);
        EvolutionProposal notWritten = beforeCommit.ImportJson(Proposal(EvolutionWorkspace.PersonaPath, Improved));
        EvolutionProposal state = interrupted.Load<EvolutionProposal>(notWritten.Id + "/state.json");
        state.Status = "Applying";
        interrupted.Save(notWritten.Id + "/state.json", state);
        Evolver resumed = new Evolver(new EvolutionWorkspace(interrupted.ProductRoot, interrupted.StateRoot));
        check(resumed.Get(notWritten.Id).Status == "Interrupted" && Persona(interrupted) == Original, "restart marks uncommitted application interrupted and does not silently apply it");
        state.Status = "Applying";
        interrupted.Save(notWritten.Id + "/state.json", state);
        File.WriteAllText(interrupted.ProductPath(EvolutionWorkspace.PersonaPath), Original + " Outra alteração.", Utf8);
        check(new Evolver(new EvolutionWorkspace(interrupted.ProductRoot, interrupted.StateRoot)).Get(notWritten.Id).Status == "Conflict" && Persona(interrupted).EndsWith("Outra alteração."), "restart preserves unknown external changes as a conflict instead of repairing them destructively");
    }

    private static void Model(string temporary, Action<bool, string> check)
    {
        EvolutionWorkspace workspace = Workspace(temporary, "model");
        Evolver evolver = new Evolver(workspace);
        string prompt = null;
        EvolutionProposal proposal = evolver.Draft("Melhore a clareza", text => { prompt = text; return Proposal(EvolutionWorkspace.PersonaPath, Improved); });
        check(prompt.Contains(Original) && prompt.Contains("Melhore a clareza") && proposal.Status == "Proposed" && Persona(workspace) == Original, "AI drafting uses the goal/current data but only creates a reviewable proposal");
        check(Refuses(() => evolver.Draft("Teste", _ => "```json\n{}\n```")), "Markdown or free-form model output is not executable proposal input");
        check(Refuses(() => evolver.Draft("Teste", _ => Proposal("app/Guardian.cs", "class Bypass {}"))), "model output cannot expand its own file permissions or propose executable C#");
        check(Persona(workspace) == Original && evolver.List().Length == 1, "failed AI proposals do not mutate the source or become valid history");
        check(Refuses(() => evolver.Draft("Teste de concorrência", _ => {
            File.WriteAllText(workspace.ProductPath(EvolutionWorkspace.PersonaPath), Original + " Mudança durante geração.", Utf8);
            return Proposal(EvolutionWorkspace.PersonaPath, Improved);
        })) && evolver.List().Length == 1, "product changes while AI drafts invalidate the stale-context candidate");
    }
}
