using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CapriDesktop;

internal static class Program
{
    private static int checks;

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        checks++;
        Console.WriteLine("PASS " + description);
    }

    private static int Main()
    {
        string folder = Path.Combine(Path.GetTempPath(), "capri-core-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(folder);
            ModuleTests.Run(Check);
            EvolutionTests.Run(Check);
            NeonRuntimeTests.Run(Check);
            string path = Path.Combine(folder, "persona.txt");
            Check(CapriProfile.LoadPersona(path) == CapriProfile.FallbackPersona, "missing profile has an honest fallback");
            File.WriteAllText(path, "  Você é CAPRI. Português, coração e precisão.  ", new UTF8Encoding(false));
            Check(CapriProfile.LoadPersona(path) == "Você é CAPRI. Português, coração e precisão.", "UTF-8 preserves accents and trims surrounding whitespace");
            File.WriteAllText(path, "Persona com BOM: ação", new UTF8Encoding(true));
            Check(CapriProfile.LoadPersona(path) == "Persona com BOM: ação", "UTF-8 BOM is accepted without leaking into the prompt");
            File.WriteAllText(path, " \r\n ");
            Check(CapriProfile.LoadPersona(path) == CapriProfile.FallbackPersona, "blank profile falls back");
            File.WriteAllBytes(path, new byte[] { 0xC3, 0x28 });
            Check(CapriProfile.LoadPersona(path) == CapriProfile.FallbackPersona, "invalid UTF-8 falls back instead of corrupting identity");
            File.WriteAllBytes(path, new byte[CapriProfile.MaximumPersonaBytes + 1]);
            Check(CapriProfile.LoadPersona(path) == CapriProfile.FallbackPersona, "oversized profile cannot consume the context window");
            Check(CapriProfile.LoadPersona(null) == CapriProfile.FallbackPersona, "invalid path uses fallback");

            Check(CapriProfile.PreferredModel(new[] { "gemma4:e4b", "capri:latest" }, null) == "capri:latest", "custom Capri wins when no model was saved");
            Check(CapriProfile.PreferredModel(new[] { "gemma4:e4b", "capri:latest" }, "gemma4:e4b") == "gemma4:e4b", "installed saved model wins over Capri default");
            Check(CapriProfile.PreferredModel(new[] { "capri:latest" }, "missing:latest") == "capri:latest", "missing saved model falls back to installed Capri");
            Check(CapriProfile.PreferredModel(new[] { "capri" }, null) == "capri", "untagged Capri alias is recognized");
            Check(CapriProfile.PreferredModel(new[] { "qwen3:4b" }, null) == null, "other models continue through upstream selection");
            Check(CapriProfile.PreferredModel(null, null) == null, "offline model selection does not invent an installation");
            Check(Brain.PickModel(new[] { "capri:latest", "qwen3:4b-instruct-2507-q4_K_M" }, null, Engine.Picks[2]) == "capri:latest", "Brain selects Capri before hardware-fit model");
            Check(Brain.PickModel(new[] { "capri:latest", "gemma4:e4b" }, "gemma4:e4b", null) == "gemma4:e4b", "Brain preserves saved upstream model selection");
            Check(Brain.PickModel(new[] { "qwen3:1.7b" }, null, null) == "qwen3:1.7b", "Brain preserves upstream fallback when Capri is absent");
            Check(Hands.Address("HTTPS://Example.COM/AbC?token=XyZ", null) == "https://example.com/AbC?token=XyZ", "website tool preserves case-sensitive path and query");
            Check(Hands.Address("YouTube", "ação") == "https://www.youtube.com/results?search_query=a%C3%A7%C3%A3o", "website search still recognizes mixed-case site names");

            int ran = 0;
            Func<string> action = delegate { ran++; return "executed"; };
            Check(new ToolPermission(null).Execute("open_app spotify", action) == ToolPermission.Denied && ran == 0, "missing consent UI denies without executing");
            Check(new ToolPermission(delegate { return false; }).Execute("open_app spotify", action) == ToolPermission.Denied && ran == 0, "cancel or close denies without executing");
            Check(new ToolPermission(delegate { throw new InvalidOperationException("UI unavailable"); }).Execute("open_app spotify", action) == ToolPermission.Denied && ran == 0, "consent callback failure denies without executing");
            string shown = null;
            ToolPermission allowed = new ToolPermission(delegate(string operation) { shown = operation; return true; });
            string description = ToolPermission.Describe("open_website", new Arguments { Site = "https://example.com/AbC?token=XyZ", Search = "ação" });
            Check(allowed.Execute(description, action) == "executed" && ran == 1 && shown == description, "explicit consent executes exactly once and receives complete operation");
            Check(description.Contains("AbC?token=XyZ") && (description.Contains("ação") || description.Contains("a\\u00e7\\u00e3o")), "consent description preserves every case-sensitive argument");
            Check(allowed.Execute(" ", action) == ToolPermission.Denied && ran == 1, "empty operation cannot be authorized");
            Check(allowed.Execute("open_app spotify", null) == ToolPermission.Denied && ran == 1, "missing action cannot be authorized");
            bool first = true;
            ToolPermission once = new ToolPermission(delegate { bool answer = first; first = false; return answer; });
            Check(once.Execute("media next", action) == "executed" && once.Execute("media next", action) == ToolPermission.Denied && ran == 2, "consent is requested anew and never cached between calls");
            Brain pending = new Brain();
            int pendingVersion = pending.CancellationVersion;
            ToolPermission cancelled = new ToolPermission(delegate { pending.Cancel(); return true; });
            Check(cancelled.Execute("open_app spotify", action, delegate { return pendingVersion == pending.CancellationVersion; }) == ToolPermission.Denied && ran == 2, "chat cancellation during consent prevents late tool execution");
            bool chatting = true;
            ToolPermission endedVoice = new ToolPermission(delegate { chatting = false; return true; });
            Check(endedVoice.Execute("open_app spotify", action, delegate { return chatting; }) == ToolPermission.Denied && ran == 2, "voice session ending during consent prevents late tool execution");
            int asked = 0;
            ToolPermission unavailable = new ToolPermission(delegate { asked++; return true; });
            Check(unavailable.Execute("open_app spotify", action, delegate { return false; }) == ToolPermission.Denied && asked == 0 && ran == 2, "expired operation is denied before prompting");
            Check(unavailable.Execute("open_app spotify", action, delegate { throw new InvalidOperationException(); }) == ToolPermission.Denied && ran == 2, "session validation failure denies execution");

            Check(ToolPermission.SubmitsInput("type", "oi\n", "no", null), "embedded newline is treated as real Enter before typing");
            Check(ToolPermission.SubmitsInput("type", "oi", "yes", null), "explicit Enter requests consent");
            Check(ToolPermission.SubmitsInput("press", null, null, "return"), "Return alias requests consent");
            Check(ToolPermission.SubmitsInput("press", null, null, "ctrl + enter"), "modified Enter requests consent");
            Check(ToolPermission.SubmitsInput("press", null, null, "en ter") && ToolPermission.SubmitsInput("press", null, null, "ctrl + re turn"), "submission detection normalizes whitespace exactly like key executor");
            Check(!ToolPermission.SubmitsInput("type", "oi", "no", null) && !ToolPermission.SubmitsInput("press", null, null, "ctrl+l"), "ordinary typing and navigation are not falsely classified as submit");
            OllamaContract();
            Console.WriteLine("Passed " + checks + " core checks. Only a loopback fake server was used; no GUI, external service, models or real tools.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL " + error); return 1; }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    private static void OllamaContract()
    {
        string previous = Brain.Ollama;
        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Task<string> received = Task.Run(delegate
        {
            using (TcpClient client = listener.AcceptTcpClient())
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;
                using (NetworkStream stream = client.GetStream())
                {
                    StringBuilder header = new StringBuilder();
                    while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
                    {
                        int next = stream.ReadByte();
                        if (next < 0 || header.Length > 65536) throw new IOException("Invalid HTTP header from Brain.");
                        header.Append((char)next);
                    }
                    if (!header.ToString().StartsWith("POST /api/chat HTTP/", StringComparison.Ordinal)) throw new IOException("Brain called an unexpected endpoint.");
                    int length = -1;
                    foreach (string line in header.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None))
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) length = int.Parse(line.Substring(15).Trim());
                    if (length < 0 || length > 65536) throw new IOException("Invalid request body length.");
                    byte[] body = new byte[length];
                    int offset = 0;
                    while (offset < length)
                    {
                        int count = stream.Read(body, offset, length - offset);
                        if (count <= 0) throw new IOException("Incomplete HTTP body.");
                        offset += count;
                    }
                    byte[] answer = Encoding.UTF8.GetBytes("{\"message\":{\"role\":\"assistant\",\"content\":\"Sinal recebido, chum.\"},\"done\":true,\"done_reason\":\"stop\"}");
                    byte[] headers = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + answer.Length + "\r\nConnection: close\r\n\r\n");
                    stream.Write(headers, 0, headers.Length);
                    stream.Write(answer, 0, answer.Length);
                    return Encoding.UTF8.GetString(body);
                }
            }
        });
        try
        {
            Brain.Ollama = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
            Brain brain = new Brain { Model = CapriProfile.DefaultModel };
            Check(brain.Ask("oi, teste de contrato") == "Sinal recebido, chum.", "real Brain.Ask accepts a fake Ollama response");
            if (!received.Wait(5000)) throw new TimeoutException("Fake Ollama did not receive the request.");
            using (JsonDocument payload = JsonDocument.Parse(received.Result))
            {
                JsonElement root = payload.RootElement;
                Check(root.GetProperty("model").GetString() == "capri:latest" && !root.GetProperty("stream").GetBoolean(), "real Ollama request uses selected Capri model and nonstreaming contract");
                JsonElement messages = root.GetProperty("messages");
                Check(messages[0].GetProperty("role").GetString() == "system" && messages[0].GetProperty("content").GetString().StartsWith(CapriProfile.Persona, StringComparison.Ordinal), "runtime persona reaches the real chat request");
                Check(messages[1].GetProperty("content").GetString() == "oi, teste de contrato", "real chat request preserves the user message");
                Check(root.GetProperty("options").GetProperty("num_ctx").GetInt32() == 4096, "upstream context contract remains intact");
            }
        }
        finally { Brain.Ollama = previous; listener.Stop(); }
    }

    [DataContract]
    private sealed class Arguments
    {
        [DataMember(Name = "site")] public string Site;
        [DataMember(Name = "search")] public string Search;
    }
}
