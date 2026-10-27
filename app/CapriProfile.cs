using System;
using System.IO;
using System.Text;

namespace CapriDesktop
{
    // Product identity is data. Loading it grants no permissions or capabilities.
    internal static class CapriProfile
    {
        internal const string DefaultModel = "capri:latest";
        internal const int MaximumPersonaBytes = 16384;
        internal const string FallbackPersona =
            "Você é CAPRI, uma assistente de IA de identidade original NEON SOUL. Fale português brasileiro, " +
            "com curiosidade, clareza e humor cuidadoso. Use chum ou preem com moderação. " +
            "Não afirme ter consciência, sentimentos humanos ou lembranças que não estão no contexto. " +
            "Não invente pesquisas, ações ou resultados: uma ação só aconteceu quando a ferramenta confirmou. " +
            "Ferramentas dependem de autorização externa ao modelo. Você só vê a tela quando ela é fornecida. " +
            "Memória, auditoria e evolução dependem dos módulos e permissões informados pela aplicação; não invente seu estado. " +
            "Responda brevemente, sem prejudicar precisão, e reconheça limites e incertezas.";

        private static readonly object Gate = new object();
        private static string persona = LoadPersona(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CAPRI", "PERSONA_DESKTOP.txt"));
        internal static string Persona { get { lock (Gate) return persona; } }
        internal static void Reload(string productRoot)
        {
            string updated = LoadPersona(Path.Combine(productRoot, "CAPRI", "PERSONA_DESKTOP.txt"));
            lock (Gate) persona = updated;
        }

        internal static string LoadPersona(string path)
        {
            try
            {
                using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (file.Length == 0 || file.Length > MaximumPersonaBytes) return FallbackPersona;
                    // Reject corrupt encoding instead of silently changing the identity text.
                    using (StreamReader reader = new StreamReader(file, new UTF8Encoding(false, true), false))
                    {
                        string text = reader.ReadToEnd().TrimStart('\uFEFF').Trim();
                        return text.Length == 0 ? FallbackPersona : text;
                    }
                }
            }
            catch (IOException) { return FallbackPersona; }
            catch (UnauthorizedAccessException) { return FallbackPersona; }
            catch (ArgumentException) { return FallbackPersona; }
            catch (System.Security.SecurityException) { return FallbackPersona; }
        }

        // null lets the upstream selector continue with hardware fit and other models.
        internal static string PreferredModel(string[] installed, string saved)
        {
            if (installed == null) return null;
            if (!string.IsNullOrEmpty(saved))
                foreach (string name in installed)
                    if (string.Equals(name, saved, StringComparison.OrdinalIgnoreCase)) return name;
            foreach (string name in installed)
                if (string.Equals(name, DefaultModel, StringComparison.OrdinalIgnoreCase)) return name;
            foreach (string name in installed)
                if (string.Equals(name, "capri", StringComparison.OrdinalIgnoreCase)) return name;
            return null;
        }
    }
}
