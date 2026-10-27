using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Principal;

namespace CapriDesktop
{
    internal sealed class GuardianGrant
    {
        internal readonly string Id;
        internal readonly string Scope;
        internal readonly string OperationHash;
        internal readonly string OwnerSid;
        internal readonly DateTime ExpiresUtc;
        internal readonly long PolicyVersion;
        internal bool Consumed { get; private set; }
        internal GuardianGrant(string id, string scope, string operationHash, string ownerSid, DateTime expiresUtc, long policyVersion)
        {
            Id = id; Scope = scope; OperationHash = operationHash; OwnerSid = ownerSid; ExpiresUtc = expiresUtc; PolicyVersion = policyVersion;
        }
        internal void MarkConsumed() { Consumed = true; }
    }

    // Policy and authentication live here, outside model prompts. The injected
    // authenticator must provide a SID proven by the trusted local UI/OS, never
    // a SID supplied by a chat message or voice transcription.
    internal sealed class Guardian
    {
        internal const string Denied = "Ação não autorizada. Nada foi executado.";
        internal const string AuditAfterExecutionFailure = "A operação foi iniciada, mas seu resultado não pôde ser auditado. O Guardian foi bloqueado; verifique o estado antes de tentar novamente.";
        internal static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(10);
        internal static readonly TimeSpan GrantLifetime = TimeSpan.FromSeconds(30);
        private static readonly string[] scopes = { "apps", "network", "media", "screen", "computer", "memory", "evolution" };
        private readonly object gate = new object();
        private readonly CapriStore store;
        private readonly Chronicle chronicle;
        private readonly Func<string> identity;
        private readonly Func<DateTime> utcNow;
        private readonly Dictionary<string, GuardianGrant> grants = new Dictionary<string, GuardianGrant>(StringComparer.Ordinal);
        private bool locked = true;
        private DateTime sessionExpires = DateTime.MinValue;
        private long policyVersion;
        internal string OwnerSid { get; private set; }
        internal string LastError { get; private set; }

        internal Guardian(CapriStore store, Chronicle chronicle, Func<string> currentIdentity = null, Func<DateTime> utcNow = null)
        {
            if (store == null || chronicle == null) throw new ArgumentNullException();
            this.store = store;
            this.chronicle = chronicle;
            identity = currentIdentity ?? WindowsSid;
            this.utcNow = utcNow ?? delegate { return DateTime.UtcNow; };
            string current = CurrentIdentity();
            OwnerSid = store.Write(delegate(CapriStore.Sql sql)
            {
                string owner = sql.Scalar("SELECT value FROM settings WHERE key='guardian.owner_sid'");
                if (owner != null) return owner;
                if (string.IsNullOrWhiteSpace(current)) throw new InvalidOperationException("The current Windows owner could not be identified.");
                sql.SetSetting("guardian.owner_sid", current);
                return current;
            });
            if (!chronicle.Verify().Valid) LastError = "A auditoria precisa ser verificada antes de desbloquear.";
            // A prior unlocked state and prior grants never survive a restart.
        }

        internal bool IsOwner { get { return !string.IsNullOrEmpty(OwnerSid) && string.Equals(CurrentIdentity(), OwnerSid, StringComparison.Ordinal); } }
        internal bool Locked { get { lock (gate) { RefreshSession(); return locked; } } }
        internal DateTime SessionExpiresUtc { get { lock (gate) return locked ? DateTime.MinValue : sessionExpires; } }

        internal bool Unlock(Func<string> authenticate)
        {
            long attemptVersion;
            lock (gate)
            {
                Revoke();
                if (!IsOwner || authenticate == null) { Deny("guardian", Chronicle.Digest("unlock"), "identity_denied"); return false; }
                if (!Audit("authentication", "guardian", Chronicle.Digest("unlock"), "requested")) return false;
                attemptVersion = policyVersion;
            }
            string authenticated = null;
            try { authenticated = authenticate(); } catch (Exception) { }
            lock (gate)
            {
                if (attemptVersion != policyVersion || !IsOwner || string.IsNullOrEmpty(authenticated) || !string.Equals(authenticated, OwnerSid, StringComparison.Ordinal))
                {
                    Deny("guardian", Chronicle.Digest("unlock"), "authentication_denied");
                    return false;
                }
                if (!Audit("authentication", "guardian", Chronicle.Digest("unlock"), "authenticated")) return false;
                sessionExpires = Now() + SessionLifetime;
                locked = false;
                policyVersion++;
                LastError = null;
                return true;
            }
        }

        internal void Lock()
        {
            lock (gate) { Revoke(); Audit("session", "guardian", Chronicle.Digest("lock"), "locked"); }
        }

        internal Dictionary<string, bool> GetScopes()
        {
            Dictionary<string, bool> result = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (string scope in scopes)
            {
                string value = store.GetSetting("guardian.scope." + scope);
                result.Add(scope, value == null || value == "1");
            }
            return result;
        }

        internal bool SetScope(string scope, bool enabled)
        {
            lock (gate)
            {
                string digest = Chronicle.Digest((scope ?? "") + "=" + (enabled ? "1" : "0"));
                if (!KnownScope(scope) || !Ready()) { Deny(scope, digest, "policy_denied"); return false; }
                if (!Audit("policy", scope, digest, "requested")) return false;
                try { store.SetSetting("guardian.scope." + scope, enabled ? "1" : "0"); }
                catch (Exception) { Audit("policy", scope, digest, "failed"); Revoke(); return false; }
                grants.Clear(); policyVersion++;
                return Audit("policy", scope, digest, enabled ? "enabled" : "disabled");
            }
        }

        internal GuardianGrant Request(string scope, string operation, Func<string, bool> confirm, Func<bool> stillActive = null)
        {
            string digest = Chronicle.Digest(operation);
            bool active = Active(stillActive);
            long version;
            lock (gate)
            {
                if (string.IsNullOrWhiteSpace(operation) || operation.Length > 65536 || confirm == null || !KnownScope(scope) || !Ready() || !ScopeEnabled(scope) || !active)
                {
                    Deny(scope, digest, "request_denied");
                    return null;
                }
                version = policyVersion;
                if (!Audit("consent", scope, digest, "requested")) return null;
            }
            bool approved = false;
            try { approved = confirm(operation); } catch (Exception) { }
            active = Active(stillActive);
            lock (gate)
            {
                if (!approved || !Ready() || version != policyVersion || !ScopeEnabled(scope) || !active)
                {
                    Deny(scope, digest, "consent_denied");
                    return null;
                }
                if (!Audit("consent", scope, digest, "approved")) return null;
                DateTime now = Now();
                foreach (string expired in new List<string>(grants.Keys)) if (grants[expired].ExpiresUtc <= now) grants.Remove(expired);
                if (grants.Count >= 128) { Deny(scope, digest, "too_many_grants"); return null; }
                DateTime expires = now + GrantLifetime;
                if (expires > sessionExpires) expires = sessionExpires;
                GuardianGrant grant = new GuardianGrant(Token(), scope, digest, OwnerSid, expires, policyVersion);
                grants.Add(grant.Id, grant);
                LastError = null;
                return grant;
            }
        }

        internal string Execute(GuardianGrant grant, string scope, string operation, Func<string> action, Func<bool> stillActive = null)
        {
            string digest = Chronicle.Digest(operation);
            bool active = Active(stillActive);
            lock (gate)
            {
                GuardianGrant stored;
                bool known = grant != null && grants.TryGetValue(grant.Id ?? "", out stored) && object.ReferenceEquals(stored, grant);
                if (known) { grants.Remove(grant.Id); grant.MarkConsumed(); }
                if (!known || action == null || !ValidGrant(grant, scope, digest) || !active)
                {
                    Deny(scope, digest, "execution_denied");
                    return Denied;
                }
                if (!Audit("execution", scope, digest, "permitted")) return Denied;
            }
            // Consent permits this operation to start. In-flight desktop work keeps
            // its existing cancellation/ESC handling; it is not a transaction.
            string result;
            try
            {
                active = Active(stillActive);
                lock (gate)
                {
                    if (!active || !ValidGrant(grant, scope, digest)) { Deny(scope, digest, "cancelled"); return Denied; }
                    if (!Audit("execution", scope, digest, "started")) return Denied;
                }
                result = action();
            }
            catch (Exception)
            {
                lock (gate) { if (!Audit("execution", scope, digest, "failed")) return AuditAfterExecutionFailure; }
                return "A operação falhou. Consulte o estado do aplicativo antes de tentar novamente.";
            }
            lock (gate) { if (!Audit("execution", scope, digest, "completed")) return AuditAfterExecutionFailure; LastError = null; }
            return result;
        }

        private bool Ready()
        {
            RefreshSession();
            if (!IsOwner || locked) return false;
            try { if (store.GetSetting("guardian.owner_sid") == OwnerSid) return true; Revoke(); return false; }
            catch (Exception) { Revoke(); return false; }
        }

        private bool ValidGrant(GuardianGrant grant, string scope, string digest)
        {
            return grant != null && Ready() && KnownScope(scope) && ScopeEnabled(scope) && grant.Scope == scope && grant.OperationHash == digest && grant.OwnerSid == OwnerSid && grant.PolicyVersion == policyVersion && grant.ExpiresUtc > Now();
        }

        private bool ScopeEnabled(string scope)
        {
            try { string value = store.GetSetting("guardian.scope." + scope); return value == null || value == "1"; }
            catch (Exception) { Revoke(); return false; }
        }

        private void RefreshSession()
        {
            if (!locked && (!IsOwner || Now() >= sessionExpires)) { Revoke(); Audit("session", "guardian", Chronicle.Digest("session"), "expired"); }
        }

        private bool Audit(string kind, string scope, string digest, string outcome)
        {
            try { chronicle.Append("guardian." + kind, KnownScope(scope) || scope == "guardian" ? scope : "unknown", digest, outcome); return true; }
            catch (Exception) { Revoke(); LastError = "A auditoria está indisponível ou foi alterada. O Guardian permanece bloqueado."; return false; }
        }

        private void Deny(string scope, string digest, string outcome)
        {
            if (Audit("denial", scope, digest, outcome)) LastError = "Operação negada. Verifique proprietário, sessão, escopo e consentimento.";
        }

        private void Revoke() { locked = true; sessionExpires = DateTime.MinValue; grants.Clear(); policyVersion++; }
        private static bool KnownScope(string scope) { return Array.IndexOf(scopes, scope) >= 0; }
        private static bool Active(Func<bool> active) { try { return active == null || active(); } catch (Exception) { return false; } }
        private string CurrentIdentity() { try { return identity(); } catch (Exception) { return null; } }
        private DateTime Now() { return utcNow().ToUniversalTime(); }

        private static string WindowsSid()
        {
            using (WindowsIdentity current = WindowsIdentity.GetCurrent()) return current.User == null ? null : current.User.Value;
        }

        private static string Token()
        {
            byte[] random = new byte[32];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create()) generator.GetBytes(random);
            return BitConverter.ToString(random).Replace("-", "").ToLowerInvariant();
        }
    }
}
