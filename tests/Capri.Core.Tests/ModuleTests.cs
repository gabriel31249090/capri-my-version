using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CapriDesktop;

internal static class ModuleTests
{
    internal static void Run(Action<bool, string> check)
    {
        string folder = Path.Combine(Path.GetTempPath(), "capri-modules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "capri.db");
        string owner = "S-1-5-21-1000";
        string current = owner;
        DateTime now = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
        string memoryId = null;
        long auditCount = 0;
        try
        {
            using (CapriStore store = new CapriStore(path))
            {
                MemoryCore memory = new MemoryCore(store);
                Chronicle journal = new Chronicle(store);
                Guardian guardian = new Guardian(store, journal, delegate { return current; }, delegate { return now; });
                check(store.Read(delegate(CapriStore.Sql sql) { return sql.Scalar("PRAGMA user_version"); }) == "1", "SQLite schema migration is applied");
                check(!memory.Enabled && Throws<InvalidOperationException>(delegate { memory.Save(null, "private", "not opted in", "manual"); }) && memory.List().Count == 0, "memory defaults off and cannot save before explicit opt-in");
                memory.SetEnabled(true);
                MemoryRecord saved = memory.Save(null, "Robótica vestível", "Decisão: usar sensores locais, ação e precisão.'); DROP TABLE memories; --", "nota manual");
                memoryId = saved.Id;
                check(memory.Get(saved.Id).Content == saved.Content && memory.List().Count == 1, "SQLite binds Unicode and hostile text as data");
                MemoryRecord edited = memory.Save(saved.Id, "Robótica vestível revisada", "Decisão corrigida: sensor de temperatura local.", "revisão autorizada");
                check(edited.CreatedUtc == saved.CreatedUtc && edited.Content != saved.Content && memory.List().Count == 1, "memory revision preserves identity and creation metadata");
                memory.Save(null, "Receita de bolo", "Farinha e chocolate.", "manual");
                check(memory.Search("robotica sensor", 1).Count == 1 && memory.Search("robotica sensor", 1)[0].Id == saved.Id, "bounded relevance finds accented project memory");
                check(memory.Search("palavrainexistente").Count == 0 && memory.Context("robotica", 400).Length <= 400, "memory retrieval excludes irrelevant items and obeys context budget");
                string longId = memory.Save(null, "Long note", new string('a', 100) + " um dois tres quatro cinco seis sete oito nove dez onze doze treze catorze quinze dezesseis dezessete dezoito dezenove vinte vinteum vintedois vintetres vintequatro vintecinco vinteseis vintesete vinteoito vintenove trinta trintaeum trintaedois acelerometro", "manual").Id;
                check(memory.Search("acelerometro").Count == 1 && memory.Context("robotica", 64).Length <= 64 && memory.Context("robotica", 0) == "", "relevance reads beyond the first words and honors even a tiny context budget");
                memory.Delete(longId);
                memory.SetEnabled(false);
                check(memory.Context("robotica") == "" && memory.List().Count == 2, "disabling memory stops context injection while retaining review access");
                check(Throws<InvalidOperationException>(delegate { memory.Save(null, "blocked", "still off", "manual"); }), "disabled memory cannot silently capture new records");
                memory.SetEnabled(true);
                string deleted = memory.Save(null, "Temporary", "Delete me.", "manual").Id;
                check(memory.Delete(deleted) && !memory.Delete(deleted) && memory.Get(deleted) == null, "memory deletion is persistent and reports missing records");
                check(Throws<InvalidOperationException>(delegate
                {
                    store.Write<int>(delegate(CapriStore.Sql sql) { sql.SetSetting("rollback.probe", "must disappear"); throw new InvalidOperationException("rollback"); });
                }) && store.GetSetting("rollback.probe") == null, "SQLite transactions roll back partial changes");

                using (CapriStore second = new CapriStore(path))
                {
                    MemoryCore other = new MemoryCore(second);
                    Parallel.For(0, 20, delegate(int i) { (i % 2 == 0 ? memory : other).Save(null, "Concurrent " + i, "Thread-safe record " + i, "test"); });
                    check(memory.List(100).Count == 22, "two SQLite connections serialize concurrent memory writes");
                }
                string export = Path.Combine(folder, "memories.json");
                memory.Export(export);
                check(File.ReadAllText(export).Contains("temperatura") && Throws<IOException>(delegate { memory.Export(export); }), "memory export contains records and never overwrites an existing file");
                check(Throws<IOException>(delegate { memory.Export(path); }), "export cannot overwrite the live SQLite database");

                string secretOperation = "open_app {password:super-secret-token}";
                journal.Append("test.operation", "apps", Chronicle.Digest(secretOperation), "observed");
                check(journal.Verify().Valid && journal.Read(1)[0].OperationHash == Chronicle.Digest(secretOperation), "Chronicle appends a verifiable digest-only audit event");
                check(Throws<IOException>(delegate { store.Write(delegate(CapriStore.Sql sql) { return sql.Execute("UPDATE chronicle SET outcome='modified' WHERE seq=1"); }); }), "SQLite rejects audit updates through append-only trigger");
                check(Throws<IOException>(delegate { store.Write(delegate(CapriStore.Sql sql) { return sql.Execute("DELETE FROM chronicle WHERE seq=1"); }); }), "SQLite rejects audit deletion through append-only trigger");
                check(Throws<ArgumentException>(delegate { journal.Append("test", "apps", secretOperation, "raw"); }), "Chronicle rejects raw operation arguments in the digest field");

                int prompts = 0;
                int ran = 0;
                Func<string, bool> approve = delegate { prompts++; return true; };
                Func<string> action = delegate { ran++; return "executed"; };
                check(guardian.Locked && guardian.Request("apps", "open calculator", approve) == null && prompts == 0, "Guardian starts locked and denies before presenting consent");
                check(!guardian.Unlock(delegate { return "S-1-5-21-2000"; }) && guardian.Locked, "authentication proof must match the enrolled Windows SID");
                check(guardian.Unlock(delegate { return owner; }) && !guardian.Locked && guardian.SessionExpiresUtc == now + Guardian.SessionLifetime, "owner authentication opens a bounded ten-minute session");
                check(guardian.Request("unknown", "open calculator", approve) == null, "unknown scopes fail closed");
                check(guardian.SetScope("media", false) && !guardian.GetScopes()["media"] && guardian.Request("media", "next track", approve) == null, "scope policy is enforced outside model prompts");
                check(guardian.SetScope("media", true), "owner can re-enable a scope with an audit trail");
                GuardianGrant grant = guardian.Request("apps", "open calculator", approve);
                check(grant != null && guardian.Execute(grant, "apps", "open calculator", action) == "executed" && ran == 1, "exact owner consent executes the authorized operation once");
                check(guardian.Execute(grant, "apps", "open calculator", action) == Guardian.Denied && ran == 1, "consumed grants cannot be replayed");
                grant = guardian.Request("apps", "open calculator", approve);
                check(guardian.Execute(grant, "apps", "open terminal", action) == Guardian.Denied && guardian.Execute(grant, "apps", "open calculator", action) == Guardian.Denied && ran == 1, "argument substitution denies execution and consumes the grant");
                grant = guardian.Request("apps", "open calculator", approve);
                check(guardian.Execute(grant, "network", "open calculator", action) == Guardian.Denied && ran == 1, "a grant cannot be transferred into another enabled scope");
                grant = guardian.Request("apps", "open calculator", approve);
                GuardianGrant forged = new GuardianGrant(grant.Id, grant.Scope, grant.OperationHash, grant.OwnerSid, grant.ExpiresUtc, grant.PolicyVersion);
                check(guardian.Execute(forged, "apps", "open calculator", action) == Guardian.Denied && ran == 1, "a copied grant object is not a registered capability");
                check(guardian.Execute(grant, "apps", "open calculator", action) == "executed" && ran == 2, "a forged copy does not consume the legitimate capability");
                grant = guardian.Request("apps", "open calculator", approve);
                now += Guardian.GrantLifetime + TimeSpan.FromSeconds(1);
                check(guardian.Execute(grant, "apps", "open calculator", action) == Guardian.Denied && ran == 2, "expired exact-operation grants cannot execute");
                check(guardian.Request("apps", "open calculator", delegate { return false; }) == null && guardian.Request("apps", "open calculator", delegate { throw new Exception("cancelled"); }) == null, "declined or failed consent never creates a capability");
                check(guardian.Request("apps", "open calculator", approve, delegate { return false; }) == null, "cancelled chat cannot acquire permission");
                current = "S-1-5-21-2000";
                check(!guardian.IsOwner && guardian.Request("apps", "open calculator", approve) == null && ran == 2, "a different Windows identity cannot use the owner's session");
                current = owner;
                check(!guardian.Unlock(delegate { guardian.Lock(); return owner; }), "locking during authentication cancels that authentication attempt");
                check(guardian.Unlock(delegate { return owner; }), "owner can authenticate again after cancellation");
                grant = guardian.Request("apps", "open calculator", approve);
                check(guardian.Execute(grant, "apps", "open calculator", action, delegate { guardian.Lock(); return true; }) == Guardian.Denied && ran == 2, "session callbacks cannot revoke and then bypass execution policy");
                check(guardian.Unlock(delegate { return owner; }), "owner session can be restored after explicit lock");
                grant = guardian.Request("apps", "open calculator", approve);
                int activeChecks = 0;
                check(guardian.Execute(grant, "apps", "open calculator", action, delegate { if (++activeChecks == 2) guardian.Lock(); return true; }) == Guardian.Denied && ran == 2, "last-moment cancellation is rechecked before starting the action");
                guardian.Unlock(delegate { return owner; });
                grant = guardian.Request("apps", secretOperation, approve);
                string failure = guardian.Execute(grant, "apps", secretOperation, delegate { throw new Exception("super-secret-token"); });
                check(failure != "executed" && journal.Read(1)[0].Outcome == "failed", "action failures are audited without logging exception secrets");
                now += Guardian.SessionLifetime + TimeSpan.FromSeconds(1);
                check(guardian.Locked && guardian.Request("apps", "open calculator", approve) == null, "session lease expiry relocks policy automatically");
                string auditExport = Path.Combine(folder, "chronicle.json");
                journal.Export(auditExport);
                string auditText = File.ReadAllText(auditExport);
                check(!auditText.Contains(secretOperation) && !auditText.Contains("super-secret-token"), "exported Chronicle never contains raw arguments or exception credentials");
                string backup = Path.Combine(folder, "backup.db");
                store.Backup(backup);
                using (CapriStore restored = new CapriStore(backup))
                {
                    check(new MemoryCore(restored).Get(memoryId).Content == edited.Content && new Chronicle(restored).Verify().Valid, "SQLite online backup includes memory and a matching audit head");
                }
                check(Throws<IOException>(delegate { store.Backup(backup); }), "backup refuses to replace an existing snapshot");
                auditCount = journal.Verify().Count;
            }
            using (CapriStore restarted = new CapriStore(path))
            {
                MemoryCore memory = new MemoryCore(restarted);
                Chronicle journal = new Chronicle(restarted);
                Guardian guardian = new Guardian(restarted, journal, delegate { return owner; }, delegate { return now; });
                check(memory.Enabled && memory.Get(memoryId).Content.Contains("corrigida"), "SQLite memory survives closing and reopening the process store");
                check(journal.Verify().Valid && journal.Append("test.restart", "memory", Chronicle.Digest(memoryId), "observed").Sequence == auditCount + 1, "Chronicle resumes the persisted chain after restart");
                check(guardian.OwnerSid == owner && guardian.Locked, "owner enrollment persists but unlocked sessions do not survive restart");
                Guardian outsider = new Guardian(restarted, journal, delegate { return "S-1-5-21-9000"; }, delegate { return now; });
                check(!outsider.IsOwner && !outsider.Unlock(delegate { return "S-1-5-21-9000"; }), "another identity cannot enroll over an existing owner");
            }
            TamperTests(folder, check);
            UnavailableAudit(folder, check);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    private static void TamperTests(string folder, Action<bool, string> check)
    {
        using (CapriStore store = new CapriStore(Path.Combine(folder, "tamper.db")))
        {
            Chronicle journal = new Chronicle(store);
            journal.Append("test", "apps", Chronicle.Digest("first"), "ok");
            store.Write(delegate(CapriStore.Sql sql) { sql.Execute("DROP TRIGGER chronicle_no_update"); return sql.Execute("UPDATE chronicle SET outcome='changed' WHERE seq=1"); });
            check(!journal.Verify().Valid, "hash-chain verification detects an externally changed audit event");
            Guardian guardian = new Guardian(store, journal, delegate { return "owner"; });
            check(!guardian.Unlock(delegate { return "owner"; }), "tampered Chronicle blocks authentication and tool execution");
        }
        using (CapriStore store = new CapriStore(Path.Combine(folder, "truncate.db")))
        {
            Chronicle journal = new Chronicle(store);
            ChronicleEntry first = journal.Append("test", "apps", Chronicle.Digest("first"), "ok");
            journal.Append("test", "apps", Chronicle.Digest("second"), "ok");
            store.Write(delegate(CapriStore.Sql sql)
            {
                sql.Execute("DROP TRIGGER chronicle_no_delete");
                sql.Execute("DELETE FROM chronicle WHERE seq=2");
                sql.SetSetting("chronicle.sequence", "1");
                sql.SetSetting("chronicle.hash", first.Hash);
                return 0;
            });
            check(!journal.Verify().Valid, "separate durable audit head detects tail truncation even when SQLite head is rewritten");
        }
    }

    private static void UnavailableAudit(string folder, Action<bool, string> check)
    {
        CapriStore store = new CapriStore(Path.Combine(folder, "unavailable.db"));
        Chronicle journal = new Chronicle(store);
        Guardian guardian = new Guardian(store, journal, delegate { return "owner"; });
        guardian.Unlock(delegate { return "owner"; });
        GuardianGrant grant = guardian.Request("apps", "never execute", delegate { return true; });
        store.Dispose();
        int ran = 0;
        check(guardian.Execute(grant, "apps", "never execute", delegate { ran++; return "wrong"; }) == Guardian.Denied && ran == 0 && guardian.Locked, "unavailable audit storage fails closed before the side effect");
        store = new CapriStore(Path.Combine(folder, "audit-after.db"));
        journal = new Chronicle(store);
        guardian = new Guardian(store, journal, delegate { return "owner"; });
        guardian.Unlock(delegate { return "owner"; });
        grant = guardian.Request("apps", "execute once", delegate { return true; });
        string result = guardian.Execute(grant, "apps", "execute once", delegate { ran++; store.Dispose(); return "completed outside audit"; });
        check(result == Guardian.AuditAfterExecutionFailure && ran == 1 && guardian.Locked, "audit failure after starting an action reports uncertainty instead of claiming nothing happened");
    }

    private static bool Throws<T>(Action action) where T : Exception
    {
        try { action(); return false; } catch (T) { return true; }
    }
}
