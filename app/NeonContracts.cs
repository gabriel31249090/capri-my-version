using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CapriDesktop
{
    // UI contract. Implementations enforce ownership, scopes, exact hashes and persistence.
    // A disabled/offline model must not disable local memory, consent or audit operations.
    internal interface INeonRuntime
    {
        Task<NeonRuntimeStatus> GetStatusAsync();
        Task SetMemoryEnabledAsync(bool enabled);
        Task<IList<NeonMemoryItem>> ListMemoryAsync();
        Task<NeonMemoryItem> SaveMemoryAsync(NeonMemoryItem item);
        Task DeleteMemoryAsync(string id, string revision);
        Task<string> ExportMemoryAsync();
        Task<string[]> ListEvolutionTargetsAsync();
        Task<string> ReadEvolutionTargetAsync(string target);
        Task<IList<NeonProposalItem>> ListProposalsAsync();
        Task<NeonProposalItem> GetProposalAsync(string id);
        Task<string> ExportProposalAsync(string id);
        Task<NeonProposalItem> CreateProposalAsync(string target, string goal, CancellationToken cancellation);
        Task<NeonProposalItem> CreateManualProposalAsync(string target, string content, string goal);
        Task<NeonProposalItem> ImportProposalAsync(string json);
        Task<NeonProposalItem> ValidateProposalAsync(string id);
        Task<NeonProposalItem> ApplyProposalAsync(string id, string expectedHash);
        Task<NeonProposalItem> RestoreProposalAsync(string id);
        Task SetGuardianLockedAsync(bool locked);
        Task SetScopeAsync(string scope, bool enabled);
        Task<IList<NeonAuditItem>> ListAuditAsync();
        Task<string> VerifyAuditAsync();
        Task<string> ExportAuditAsync();
    }

    internal sealed class NeonRuntimeStatus
    {
        public bool ModelConnected { get; set; }
        public string ModelName { get; set; }
        public string Owner { get; set; }
        public bool GuardianLocked { get; set; }
        public bool MemoryEnabled { get; set; }
        public string StorageStatus { get; set; }
        public IList<NeonScopeItem> Scopes { get; set; } = new List<NeonScopeItem>();
    }

    internal sealed class NeonScopeItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }
    }

    internal sealed class NeonMemoryItem
    {
        public string Id { get; set; }
        public string Revision { get; set; }
        public string Kind { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public string Source { get; set; }
        public string UpdatedAt { get; set; }
    }

    internal sealed class NeonProposalItem
    {
        public string Id { get; set; }
        public string Target { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public string ContentHash { get; set; }
        public string Before { get; set; }
        public string After { get; set; }
        public string Diff { get; set; }
        public string Validation { get; set; }
        public string CreatedAt { get; set; }
        public bool CanApply { get; set; }
        public bool CanRestore { get; set; }
    }

    internal sealed class NeonAuditItem
    {
        public string Id { get; set; }
        public string CreatedAt { get; set; }
        public string Action { get; set; }
        public string Result { get; set; }
        public string Details { get; set; }
    }

}
