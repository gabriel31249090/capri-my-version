using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    [DataContract]
    internal sealed class ChronicleEntry
    {
        [DataMember] public long Sequence;
        [DataMember] public string AtUtc;
        [DataMember] public string Kind;
        [DataMember] public string Scope;
        [DataMember] public string OperationHash;
        [DataMember] public string Outcome;
        [DataMember] public string PreviousHash;
        [DataMember] public string Hash;
    }

    internal sealed class ChronicleCheck
    {
        internal bool Valid;
        internal long Count;
        internal string Error;
    }

    // Append-only at the API/SQLite-trigger layer. A separate durable head detects
    // truncation or a rewritten DB chain. It cannot defeat a Windows owner/admin
    // who replaces both the database and head, or modifies this application's code.
    internal sealed class Chronicle
    {
        private const string Origin = "0000000000000000000000000000000000000000000000000000000000000000";
        private readonly CapriStore store;
        private readonly string anchor;

        internal Chronicle(CapriStore store)
        {
            if (store == null) throw new ArgumentNullException("store");
            this.store = store;
            anchor = store.DbPath + ".chronicle-head";
            store.Read(delegate(CapriStore.Sql sql)
            {
                if (!File.Exists(anchor) && sql.Scalar("SELECT COUNT(*) FROM chronicle") == "0" && sql.Scalar("SELECT value FROM settings WHERE key='chronicle.sequence'") == null)
                    WriteAnchor(0, Origin);
                return 0;
            });
        }

        internal ChronicleEntry Append(string kind, string scope, string operationHash, string outcome)
        {
            Label(kind, "kind"); Label(scope, "scope"); Label(outcome, "outcome");
            if (!Regex.IsMatch(operationHash ?? "", "^[a-f0-9]{64}$")) throw new ArgumentException("Only an operation digest may be audited.", "operationHash");
            return store.Read(delegate(CapriStore.Sql outer)
            {
                ChronicleCheck integrity = Verify(outer);
                if (!integrity.Valid) throw new InvalidDataException("Chronicle integrity failed: " + integrity.Error);
                ChronicleEntry item = store.Write(delegate(CapriStore.Sql sql)
                {
                    ChronicleEntry entry = new ChronicleEntry
                    {
                        Sequence = integrity.Count + 1,
                        AtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                        Kind = kind,
                        Scope = scope,
                        OperationHash = operationHash,
                        Outcome = outcome,
                        PreviousHash = sql.Scalar("SELECT value FROM settings WHERE key='chronicle.hash'") ?? Origin
                    };
                    entry.Hash = EntryHash(entry);
                    sql.Execute("INSERT INTO chronicle(seq,at_utc,kind,scope,operation_hash,outcome,previous_hash,entry_hash) VALUES(?,?,?,?,?,?,?,?)", entry.Sequence, entry.AtUtc, entry.Kind, entry.Scope, entry.OperationHash, entry.Outcome, entry.PreviousHash, entry.Hash);
                    sql.SetSetting("chronicle.sequence", entry.Sequence.ToString(CultureInfo.InvariantCulture));
                    sql.SetSetting("chronicle.hash", entry.Hash);
                    return entry;
                });
                // Hold the store's process/path lock until both durable records agree.
                // A power loss between commit and this replace fails closed on restart.
                WriteAnchor(item.Sequence, item.Hash);
                return item;
            });
        }

        internal ChronicleCheck Verify()
        {
            try { return store.Read(Verify); }
            catch (Exception) { return new ChronicleCheck { Valid = false, Error = "Audit storage is unavailable." }; }
        }

        private ChronicleCheck Verify(CapriStore.Sql sql)
        {
            List<string[]> rows = sql.Query("SELECT seq,at_utc,kind,scope,operation_hash,outcome,previous_hash,entry_hash FROM chronicle ORDER BY seq");
            long sequence = 0;
            string previous = Origin;
            foreach (string[] row in rows)
            {
                ChronicleEntry entry = FromRow(row);
                if (entry.Sequence != ++sequence || entry.PreviousHash != previous || entry.Hash != EntryHash(entry)) return new ChronicleCheck { Valid = false, Count = sequence - 1, Error = "The audit hash chain was changed." };
                previous = entry.Hash;
            }
            string storedSequence = sql.Scalar("SELECT value FROM settings WHERE key='chronicle.sequence'") ?? "0";
            string storedHash = sql.Scalar("SELECT value FROM settings WHERE key='chronicle.hash'") ?? Origin;
            string expected = sequence.ToString(CultureInfo.InvariantCulture) + "|" + previous;
            if (storedSequence != sequence.ToString(CultureInfo.InvariantCulture) || storedHash != previous) return new ChronicleCheck { Valid = false, Count = sequence, Error = "The audit head in SQLite does not match its chain." };
            if (!File.Exists(anchor) || new FileInfo(anchor).Length > 256 || File.ReadAllText(anchor, Encoding.UTF8) != expected) return new ChronicleCheck { Valid = false, Count = sequence, Error = "The separate audit head is missing or changed; restore a verified backup." };
            return new ChronicleCheck { Valid = true, Count = sequence };
        }

        internal List<ChronicleEntry> Read(int limit)
        {
            limit = Math.Max(1, Math.Min(500, limit));
            return store.Read(delegate(CapriStore.Sql sql)
            {
                ChronicleCheck check = Verify(sql);
                if (!check.Valid) throw new InvalidDataException(check.Error);
                List<ChronicleEntry> entries = new List<ChronicleEntry>();
                foreach (string[] row in sql.Query("SELECT seq,at_utc,kind,scope,operation_hash,outcome,previous_hash,entry_hash FROM chronicle ORDER BY seq DESC LIMIT ?", limit)) entries.Add(FromRow(row));
                return entries;
            });
        }

        internal void Export(string path)
        {
            List<ChronicleEntry> entries = store.Read(delegate(CapriStore.Sql sql)
            {
                ChronicleCheck check = Verify(sql);
                if (!check.Valid) throw new InvalidDataException(check.Error);
                List<ChronicleEntry> all = new List<ChronicleEntry>();
                foreach (string[] row in sql.Query("SELECT seq,at_utc,kind,scope,operation_hash,outcome,previous_hash,entry_hash FROM chronicle ORDER BY seq")) all.Add(FromRow(row));
                return all;
            });
            store.Export(path, delegate(Stream output) { new DataContractJsonSerializer(typeof(List<ChronicleEntry>)).WriteObject(output, entries); });
        }

        internal static string Digest(string text)
        {
            using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""))).Replace("-", "").ToLowerInvariant();
        }

        private static string EntryHash(ChronicleEntry entry)
        {
            string[] fields = { entry.Sequence.ToString(CultureInfo.InvariantCulture), entry.AtUtc, entry.Kind, entry.Scope, entry.OperationHash, entry.Outcome, entry.PreviousHash };
            StringBuilder canonical = new StringBuilder("CAPRI-CHRONICLE-1\n");
            foreach (string field in fields) canonical.Append((field ?? "").Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(field ?? "");
            return Digest(canonical.ToString());
        }

        private static ChronicleEntry FromRow(string[] row)
        {
            return new ChronicleEntry { Sequence = long.Parse(row[0], CultureInfo.InvariantCulture), AtUtc = row[1], Kind = row[2], Scope = row[3], OperationHash = row[4], Outcome = row[5], PreviousHash = row[6], Hash = row[7] };
        }

        private static void Label(string value, string name)
        {
            if (!Regex.IsMatch(value ?? "", "^[a-zA-Z0-9_.:-]{1,80}$")) throw new ArgumentException("Audit fields accept short event labels only.", name);
        }

        private void WriteAnchor(long sequence, string hash)
        {
            if (File.Exists(anchor) && (File.GetAttributes(anchor) & FileAttributes.ReparsePoint) != 0) throw new IOException("The audit head cannot be a link.");
            string temporary = anchor + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(sequence.ToString(CultureInfo.InvariantCulture) + "|" + hash);
                using (FileStream file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                if (File.Exists(anchor)) File.Replace(temporary, anchor, null); else File.Move(temporary, anchor);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
