using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    // A local consent gate, not the planned authenticated Guardian or Chronicle.
    internal sealed class ToolPermission
    {
        internal const string Denied = "Ação não autorizada. Nada foi executado.";
        private readonly Func<string, bool> confirm;

        internal ToolPermission(Func<string, bool> confirm) { this.confirm = confirm; }

        internal string Execute(string operation, Func<string> action, Func<bool> stillActive = null)
        {
            if (string.IsNullOrWhiteSpace(operation) || action == null || confirm == null) return Denied;
            bool allowed;
            try
            {
                if (stillActive != null && !stillActive()) return Denied;
                allowed = confirm(operation);
                if (stillActive != null && !stillActive()) return Denied;
            }
            catch (Exception) { return Denied; }
            if (!allowed) return Denied;
            return action();
        }

        internal static string Describe(string tool, object arguments)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                if (arguments == null) return tool + "\n{}";
                new DataContractJsonSerializer(arguments.GetType()).WriteObject(stream, arguments);
                return tool + "\n" + Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        internal static bool SubmitsInput(string tool, string text, string enter, string keys)
        {
            string normalizedKeys = Regex.Replace(keys ?? "", @"\s+", "");
            return (tool == "type" && (enter == "yes" || (text ?? "").IndexOf('\n') >= 0)) ||
                (tool == "press" && Regex.IsMatch(normalizedKeys, @"(^|\+)(enter|return)($|\+)", RegexOptions.IgnoreCase));
        }
    }
}
