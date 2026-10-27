using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace CapriDesktop
{
    // Only these declarative product files can be changed. No generated code is executed.
    internal sealed class EvolutionWorkspace
    {
        internal const string PersonaPath = "CAPRI/PERSONA_DESKTOP.txt";
        internal const string SettingsPath = "CAPRI/EVOLUTION_SETTINGS.json";
        internal readonly string ProductRoot;
        internal readonly string StateRoot;
        private readonly byte[] key;

        internal EvolutionWorkspace(string productRoot, string stateRoot)
        {
            ProductRoot = Path.GetFullPath(productRoot).TrimEnd(Path.DirectorySeparatorChar);
            StateRoot = Path.GetFullPath(stateRoot).TrimEnd(Path.DirectorySeparatorChar);
            if (!Directory.Exists(ProductRoot) || string.Equals(ProductRoot, StateRoot, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose an existing product directory and a separate evolution state directory.");
            CheckAncestors(ProductRoot);
            CheckAncestors(StateRoot);
            Directory.CreateDirectory(StateRoot);
            using (Lock())
            {
                string keyPath = StatePath("integrity.key");
                if (!File.Exists(keyPath))
                {
                    byte[] made = new byte[32];
                    using (RandomNumberGenerator random = RandomNumberGenerator.Create()) random.GetBytes(made);
                    WriteNew(keyPath, made);
                }
                key = ReadLimited(keyPath, 32);
                if (key.Length != 32) throw new InvalidDataException("Evolution integrity key is invalid.");
            }
        }

        internal IDisposable Lock()
        {
            CheckAncestors(StateRoot);
            string path = Path.Combine(StateRoot, "workspace.lock");
            CheckAncestors(path);
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        internal string ProductPath(string relative)
        {
            if (relative != PersonaPath && relative != SettingsPath)
                throw new InvalidDataException("Evolution scope is limited to the persona and typed generation settings.");
            string path = Path.GetFullPath(Path.Combine(ProductRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(ProductRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Evolution path left the product directory.");
            CheckAncestors(path);
            return path;
        }

        internal string StatePath(string relative)
        {
            if (string.IsNullOrEmpty(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("Invalid state path.");
            foreach (string part in relative.Replace('\\', '/').Split('/'))
                if (part == ".." || part == "." || part.Length == 0 || part.IndexOf(':') >= 0) throw new InvalidDataException("Invalid state path.");
            string path = Path.GetFullPath(Path.Combine(StateRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(StateRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("State path left the workspace.");
            CheckAncestors(path);
            return path;
        }

        internal static void CheckAncestors(string path)
        {
            string current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException("Evolution refuses junctions, symbolic links and other reparse points.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                string parent = Path.GetDirectoryName(current);
                if (parent == current) break;
                current = parent;
            }
        }

        internal byte[] ReadProduct(string relative)
        {
            string path = ProductPath(relative);
            return File.Exists(path) ? ReadLimited(path, 65536) : null;
        }

        internal static byte[] ReadLimited(string path, int maximum)
        {
            CheckAncestors(path);
            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (file.Length > maximum) throw new InvalidDataException("Evolution file exceeds its size limit.");
                byte[] bytes = new byte[(int)file.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int count = file.Read(bytes, offset, bytes.Length - offset);
                    if (count == 0) throw new EndOfStreamException();
                    offset += count;
                }
                return bytes;
            }
        }

        internal static string Sha256(byte[] bytes)
        {
            if (bytes == null) return "absent";
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        internal static string Sha256(string text) { return Sha256(new UTF8Encoding(false, true).GetBytes(text)); }

        internal static void WriteNew(string path, byte[] bytes)
        {
            CheckAncestors(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            CheckAncestors(path);
            using (FileStream file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
        }

        internal static void AtomicWrite(string path, byte[] bytes)
        {
            CheckAncestors(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            CheckAncestors(path);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                WriteNew(temporary, bytes);
                CheckAncestors(path);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        internal void ReplaceProduct(string relative, byte[] bytes, string expectedHash)
        {
            string path = ProductPath(relative);
            if (Sha256(ReadProduct(relative)) != expectedHash) throw new InvalidDataException("The product changed since this operation was reviewed.");
            if (bytes == null)
            {
                CheckAncestors(path);
                if (File.Exists(path)) File.Delete(path);
            }
            else AtomicWrite(path, bytes);
        }

        internal void Save<T>(string relative, T value)
        {
            byte[] payload = Serialize(value);
            string mac;
            using (HMACSHA256 hash = new HMACSHA256(key)) mac = Convert.ToBase64String(hash.ComputeHash(payload));
            AtomicWrite(StatePath(relative), Serialize(new EvolutionEnvelope { Payload = Convert.ToBase64String(payload), Mac = mac }));
        }

        internal T Load<T>(string relative)
        {
            EvolutionEnvelope envelope = Deserialize<EvolutionEnvelope>(ReadLimited(StatePath(relative), 262144));
            byte[] payload = Convert.FromBase64String(envelope.Payload ?? "");
            byte[] given = Convert.FromBase64String(envelope.Mac ?? "");
            byte[] expected;
            using (HMACSHA256 hash = new HMACSHA256(key)) expected = hash.ComputeHash(payload);
            int mismatch = given.Length ^ expected.Length;
            for (int i = 0; i < expected.Length; i++) mismatch |= expected[i] ^ (i < given.Length ? given[i] : 0);
            if (mismatch != 0) throw new InvalidDataException("Evolution state was modified or its integrity key changed.");
            return Deserialize<T>(payload);
        }

        private static byte[] Serialize<T>(T value)
        {
            using (MemoryStream stream = new MemoryStream())
            { new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value); return stream.ToArray(); }
        }

        private static T Deserialize<T>(byte[] bytes)
        {
            using (MemoryStream stream = new MemoryStream(bytes)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }
    }

    [DataContract]
    internal sealed class EvolutionEnvelope
    {
        [DataMember] public string Payload;
        [DataMember] public string Mac;
    }

    internal sealed class EvolutionSettings
    {
        internal int OutputTokens = 384;
        internal int HistoryMessages = 12;
        internal double Temperature = 0.8;

        internal static EvolutionSettings Parse(string json)
        {
            Dictionary<string, object> values = EvolutionJson.Object(json);
            EvolutionJson.Keys(values, "outputTokens", "historyMessages", "temperature");
            double tokens = EvolutionJson.Number(values, "outputTokens");
            double history = EvolutionJson.Number(values, "historyMessages");
            double temperature = EvolutionJson.Number(values, "temperature");
            if (tokens != Math.Truncate(tokens) || tokens < 64 || tokens > 2048 || history != Math.Truncate(history) || history < 2 || history > 40 || history % 2 != 0 || temperature < 0 || temperature > 1.5)
                throw new InvalidDataException("Settings require outputTokens 64..2048, even historyMessages 2..40 and temperature 0..1.5.");
            return new EvolutionSettings { OutputTokens = (int)tokens, HistoryMessages = (int)history, Temperature = temperature };
        }

        internal static EvolutionSettings Load(string productRoot)
        {
            try
            {
                string path = Path.Combine(productRoot, EvolutionWorkspace.SettingsPath.Replace('/', Path.DirectorySeparatorChar));
                return Parse(new UTF8Encoding(false, true).GetString(EvolutionWorkspace.ReadLimited(path, 4096)).TrimStart('\uFEFF'));
            }
            catch (Exception error)
            {
                if (!(error is IOException) && !(error is InvalidDataException) && !(error is UnauthorizedAccessException) && !(error is ArgumentException) && !(error is FormatException) && !(error is System.Security.SecurityException)) throw;
                return new EvolutionSettings();
            }
        }
    }
}
