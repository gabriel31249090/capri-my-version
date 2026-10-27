using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed class Mainframe : Form
    {
        private static readonly Color Field = Color.FromArgb(17, 21, 28);
        private static readonly Color Hover = Color.FromArgb(26, 32, 42);
        private static readonly Color Pressed = Color.FromArgb(34, 42, 55);
        private static readonly Color Edge = Color.FromArgb(52, 60, 74);
        private static readonly Color Track = Color.FromArgb(30, 36, 46);
        private const int Wide = 380;
        private static readonly int Tall = Companion.FreeChatOn ? 708 : 680;
        private static readonly int[] Gaps = { 10, 15, 30, 60, 90, 120, 180, 300, 600 };
        private static readonly string[] GapNames = { "10 s", "15 s", "30 s", "1 min", "1.5 min", "2 min", "3 min", "5 min", "10 min" };
        private const int SetupHeight = 118;
        private const int KeyRow = 72;
        internal const string KeyNote = "No key needed, she runs on your PC. Keys are for low-power PCs or if her brain won't run.";
        private static readonly int VoiceHeight = Companion.FreeChatOn ? 224 : 196;
        private readonly Companion capri;
        private readonly Label brainName;
        private readonly Label brainNote;
        private readonly Key models;
        private readonly ContextMenuStrip modelMenu;
        private readonly Key refresh;
        private readonly Label[] steps = new Label[3];
        private readonly string[] stepText = new string[3];
        private readonly Key setup;
        private readonly Label hint;
        private readonly Label keyNote;
        private readonly Key showHide;
        private readonly Key perch;
        private readonly Key[] sizes;
        private readonly Toggle motion;
        private readonly Toggle onTop;
        private readonly Toggle watch;
        private readonly Toggle speak;
        private readonly Toggle freeVoice;
        private readonly Slider gap;
        private readonly Label gapName;
        private readonly Entry keyEntry;
        private readonly Key connect;
        private readonly Entry voiceEntry;
        private readonly ContextMenuStrip voiceMenu;
        private readonly Key speechModels;
        private readonly ContextMenuStrip speechModelMenu;
        private readonly Label voiceNote;
        private readonly Label updateNote;
        private readonly Key neon;
        private INeonRuntime neonRuntime;
        private NeonConsole neonConsole;
        private readonly Key keyHelp;
        private readonly Label shortcut;
        private bool shortcutReady;
        private readonly Label muteShortcut;
        private readonly Key mics;
        private readonly ContextMenuStrip micMenu;
        private bool keySaved;
        private readonly Entry textEntry;
        private readonly Key textConnect;
        private readonly Key freeKey;
        private bool textSaved;
        private readonly List<KeyValuePair<Control, int>> lower = new List<KeyValuePair<Control, int>>();
        private string[] lastInstalled;
        private float filled;
        private bool expanded = true;
        private bool loaded;
        private bool syncing;
        internal bool Quitting;

        internal Mainframe(Companion owner, Icon icon, string version)
        {
            capri = owner;
            Text = "Capri Mainframe";
            Icon = icon;
            ShowInTaskbar = true;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            Font = Look.Mono(9f, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Look.Panel;
            ForeColor = Look.Ink;
            DoubleBuffered = true;
            AutoScroll = true;
            ClientSize = new Size(Wide, Tall + SetupHeight + KeyRow);
            Caption("MAINFRAME", 26, 20, Look.Mono(13f, FontStyle.Bold, GraphicsUnit.Point), Look.Ink);
            Label build = Caption("v" + version, 200, 20, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim);
            build.AutoSize = false;
            build.TextAlign = ContentAlignment.MiddleRight;
            build.Bounds = new Rectangle(200, 20, 92, 22);
            Key minimize = Make("", 300, 20, 26, 22, delegate { WindowState = FormWindowState.Minimized; });
            minimize.Glyph = 1;
            minimize.AccessibleName = "Minimize";
            Key close = Make("", 330, 20, 26, 22, delegate { Close(); });
            close.Glyph = 2;
            close.AccessibleName = "Close";
            Caption("BRAIN >", 28, 58, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
            brainName = Caption("", 28, 78, Look.Mono(10.5f, FontStyle.Bold, GraphicsUnit.Point), Look.Ink);
            brainNote = Caption("", 28, 100, Font, Look.Dim);
            modelMenu = Dropdown();
            models = Make("", 28, 134, 244, 28, delegate { modelMenu.Show(models, new Point(0, models.Height)); });
            models.Chevron = true;
            refresh = Make("Refresh", 280, 134, 76, 28, delegate { capri.FindBrain(); });
            for (int i = 0; i < 3; i++) steps[i] = Caption("", 28, 138 + i * 22, Font, Look.Dim);
            setup = Make("Set up her brain", 28, 226, 170, 28, delegate { if (capri.SettingUp) capri.CancelSetup(); else capri.SetUpBrain(); });
            setup.Accent = Look.Cyan;
            setup.Loud = true;
            hint = Caption("Have Ollama? Open it, then press Refresh.", 28, 264, Look.Mono(9f, FontStyle.Italic, GraphicsUnit.Point), Look.Dim);
            textEntry = Input(28, 170, 244);
            textEntry.Box.UseSystemPasswordChar = true;
            textEntry.Box.AccessibleName = "NVIDIA, OpenAI, Anthropic or DeepSeek API key";
            textEntry.Cue = "Optional: NVIDIA/GPT/Claude/DeepSeek";
            textConnect = Make("Connect", 280, 170, 76, 28, delegate { ConnectText(); });
            freeKey = Make("Free key", 256, 54, 100, 22, delegate { capri.OpenFreeKeyPage(); });
            keyNote = Caption(KeyNote, 28, 202, Look.Mono(8.5f, FontStyle.Italic, GraphicsUnit.Point), Look.Dim);
            textEntry.Box.TextChanged += delegate { RelabelText(); };
            textEntry.Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ConnectText(); } };
            Lower(Caption("VOICE >", 28, 184, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow));
            keyHelp = Lower(Make("Get a key", 256, 180, 100, 22, delegate { capri.OpenKeyPage(); }));
            shortcut = Caption("Ctrl + Alt + V to talk", 156, 182, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim);
            shortcut.AutoSize = false;
            shortcut.TextAlign = ContentAlignment.MiddleRight;
            shortcut.Bounds = new Rectangle(156, 182, 200, 20);
            Lower(shortcut);
            keyEntry = Lower(Input(28, 206, 244));
            keyEntry.Box.UseSystemPasswordChar = true;
            keyEntry.Box.AccessibleName = "ElevenLabs API key";
            connect = Lower(Make("Connect", 280, 206, 76, 28, delegate { Connect(); }));
            keyEntry.Box.TextChanged += delegate { Relabel(); };
            keyEntry.Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Connect(); } };
            voiceMenu = Dropdown();
            voiceEntry = Lower(Input(28, 242, 196));
            voiceEntry.Chevron = true;
            voiceEntry.Cue = "Voice ID";
            voiceEntry.Box.AccessibleName = "ElevenLabs voice ID";
            voiceEntry.Drop += delegate { voiceMenu.Show(voiceEntry, new Point(0, voiceEntry.Height)); };
            voiceEntry.Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; capri.ChooseVoice(voiceEntry.Box.Text); } };
            voiceEntry.Box.Leave += delegate { capri.ChooseVoice(voiceEntry.Box.Text); };
            speechModelMenu = Dropdown();
            speechModels = Lower(Make("Default model", 232, 242, 124, 28, delegate { speechModelMenu.Show(speechModels, new Point(0, speechModels.Height)); }));
            speechModels.Chevron = true;
            micMenu = Dropdown();
            mics = Lower(Make("Mic: default", 28, 278, 244, 28, delegate { ShowMics(); }));
            mics.Chevron = true;
            mics.AccessibleName = "Microphone";
            Lower(Make("Test", 280, 278, 76, 28, delegate { capri.TestVoice(); }));
            muteShortcut = Lower(Caption("Ctrl + Alt + N mutes mic", 28, 313, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim));
            speak = Lower(Check("Speak replies", 212, 311, 144, delegate { if (!syncing) capri.ToggleSpeaking(); }));
            freeVoice = Lower(Check("Free voice chat", 28, 339, 328, delegate { if (!syncing) capri.ToggleFreeVoice(); }));
            freeVoice.Hint = "You talk, she types";
            freeVoice.HintFont = Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point);
            freeVoice.Visible = Companion.FreeChatOn;
            voiceNote = Lower(Caption("", 28, Companion.FreeChatOn ? 367 : 339, Font, Look.Dim));
            int v = VoiceHeight;
            Lower(Caption("CAPRI >", 28, 184 + v, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow));
            showHide = Lower(Make("Hide Capri", 28, 206 + v, 104, 28, delegate { capri.ToggleHidden(); }));
            Lower(Make("Bring here", 140, 206 + v, 104, 28, delegate { capri.BringHere(); }));
            perch = Lower(Make("Ride cursor", 252, 206 + v, 104, 28, delegate { capri.TogglePerch(); }));
            Lower(Caption("Size", 28, 251 + v, Font, Look.Dim));
            sizes = new Key[4];
            int[] values = { 170, 240, 320, 440 };
            string[] names = { "Small", "Medium", "Large", "Huge" };
            for (int i = 0; i < 4; i++)
            {
                int size = values[i];
                sizes[i] = Lower(Make(names[i], 84 + i * 68, 246 + v, 64, 26, delegate { capri.SetCharacterSize(size); }));
                sizes[i].Tag = size;
            }
            motion = Lower(Check("Idle motion", 28, 286 + v, 150, delegate { if (!syncing) capri.ToggleMotion(); }));
            onTop = Lower(Check("Always on top", 186, 286 + v, 170, delegate { if (!syncing) capri.ToggleOnTop(); }));
            watch = Lower(Check("Watch my screen", 28, 318 + v, 328, delegate { if (!syncing) capri.ToggleWatch(); }));
            watch.Hint = "She comments now and then";
            watch.HintFont = Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point);
            Lower(Caption("Every", 28, 353 + v, Font, Look.Dim));
            gap = new Slider(GapNames);
            gap.Bounds = new Rectangle(78, 346 + v, 206, 30);
            Lower(gap);
            gap.AccessibleName = "Time between glances";
            gap.Changed += delegate { if (syncing) return; gapName.Text = GapNames[gap.Value]; capri.SetGlanceGap(Gaps[gap.Value]); };
            Controls.Add(gap);
            gapName = Lower(Caption("2 min", 292, 353 + v, Font, Look.Ink));
            Lower(Make("Check for updates", 28, 392 + v, 170, 28, delegate { capri.CheckUpdates(); }));
            Key quit = Lower(Make("Quit Capri", 256, 392 + v, 100, 28, delegate { capri.Quit(); }));
            quit.Accent = Look.Red;
            neon = Lower(Make("MEMÓRIA / EVOLUÇÃO", 28, 430 + v, 328, 28, delegate { OpenNeonConsole(); }));
            neon.Accent = Look.Cyan;
            neon.Loud = true;
            neon.Enabled = false;
            neon.AccessibleDescription = "Abrir memória persistente, propostas de evolução, Guardian e auditoria";
            updateNote = Lower(Caption("", 28, 464 + v, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim));
            Searching();
            Sync();
        }

        private T Lower<T>(T control) where T : Control
        {
            lower.Add(new KeyValuePair<Control, int>(control, control.Top));
            return control;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams value = base.CreateParams;
                value.Style = (value.Style | 0x00020000 | 0x00080000) & ~0x00010000;
                return value;
            }
        }

        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            if (message.Msg == 0x0084 && message.Result == new IntPtr(1)) message.Result = new IntPtr(2);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            loaded = true;
            Arrange(expanded);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            Shape();
            Invalidate(true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float s = DeviceScale();
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Backdrop(graphics, this, s);
            using (GraphicsPath edge = Look.Plate(new RectangleF(s, s, Width - 2 * s - 1, Height - 2 * s - 1), 12 * s, -1, Height / 2f, 0, true))
            using (Pen cyan = new Pen(Look.Cyan, 2 * s))
                graphics.DrawPath(cyan, edge);
            int shift = (expanded ? SetupHeight : 0) + KeyRow;
            using (Brush red = new SolidBrush(Look.Red))
            {
                graphics.FillRectangle(red, 13 * s, 58 * s, 3 * s, ((expanded ? 280 : 162) + KeyRow - 58) * s);
                graphics.FillRectangle(red, 13 * s, (184 + shift) * s, 3 * s, (396 - 184) * s);
                graphics.FillRectangle(red, 13 * s, (184 + VoiceHeight + shift) * s, 3 * s, (372 - 184) * s);
            }
            if (expanded)
            {
                float pitch = 6 * s;
                int count = (int)(328 / 6f);
                int lit = (int)Math.Round(count * filled);
                using (Brush on = new SolidBrush(Look.Cyan))
                using (Brush off = new SolidBrush(Track))
                    for (int i = 0; i < count; i++) graphics.FillRectangle(i < lit ? on : off, 28 * s + i * pitch, 210 * s, pitch - 2 * s, 6 * s);
            }
        }

        private static void Backdrop(Graphics graphics, Control control, float s)
        {
            graphics.Clear(Look.Panel);
            float pitch = 3 * s;
            float first = control is Form ? 0 : (pitch - control.Top % pitch) % pitch;
            using (Pen scan = new Pen(Color.FromArgb(9, 255, 255, 255), Math.Max(1, s)))
                for (float row = first; row < control.Height; row += pitch) graphics.DrawLine(scan, 0, row, control.Width, row);
        }

        private void Shape()
        {
            float s = DeviceScale();
            Region old = Region;
            using (GraphicsPath plate = Look.Plate(new RectangleF(0, 0, Width, Height), 12 * s, -1, Height / 2f, 0, true)) Region = new Region(plate);
            if (old != null) old.Dispose();
        }

        private float DeviceScale() { return CurrentAutoScaleDimensions.Width / 96f; }

        private int Scaled(int value) { return loaded ? (int)Math.Round(value * DeviceScale()) : value; }

        private void Arrange(bool setupShown)
        {
            AutoScrollPosition = Point.Empty;
            expanded = setupShown;
            int shift = (setupShown ? SetupHeight : 0) + KeyRow;
            textEntry.Top = Scaled(170 + shift - KeyRow);
            textConnect.Top = textEntry.Top;
            keyNote.Top = textEntry.Top + Scaled(32);
            keyNote.MaximumSize = new Size(Scaled(328), 0);
            foreach (KeyValuePair<Control, int> entry in lower) entry.Key.Top = Scaled(entry.Value + shift);
            foreach (Label step in steps) step.Visible = setupShown;
            setup.Visible = setupShown;
            refresh.Top = Scaled(setupShown ? 226 : 134);
            brainNote.MaximumSize = new Size(Scaled(328), 0);
            voiceNote.MaximumSize = new Size(Scaled(328), 0);
            updateNote.MaximumSize = new Size(Scaled(328), 0);
            int contentHeight = Scaled(Tall + shift);
            int availableHeight = Math.Max(300, Screen.FromControl(this).WorkingArea.Height - Scaled(24));
            AutoScrollMinSize = new Size(0, contentHeight);
            ClientSize = new Size(Scaled(Wide), Math.Min(contentHeight, availableHeight));
            Invalidate(true);
        }

        internal void BindNeonRuntime(INeonRuntime runtime)
        {
            neonRuntime = runtime;
            neon.Enabled = runtime != null;
        }

        private void OpenNeonConsole()
        {
            if (neonRuntime == null) return;
            if (neonConsole == null || neonConsole.IsDisposed)
            {
                neonConsole = new NeonConsole(neonRuntime);
                neonConsole.FormClosed += delegate { neonConsole = null; };
                neonConsole.Show(this);
            }
            else
            {
                if (neonConsole.WindowState == FormWindowState.Minimized) neonConsole.WindowState = FormWindowState.Normal;
                neonConsole.Activate();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!Quitting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; WindowState = FormWindowState.Minimized; }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { modelMenu.Dispose(); voiceMenu.Dispose(); speechModelMenu.Dispose(); micMenu.Dispose(); }
            base.Dispose(disposing);
        }

        internal void Restore()
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        internal void Sync()
        {
            syncing = true;
            showHide.Text = capri.IsHidden ? "Show Capri" : "Hide Capri";
            perch.Text = capri.Riding ? "Hop off" : "Ride cursor";
            foreach (Key option in sizes)
            {
                option.Picked = (int)option.Tag == capri.CharacterSize;
                option.Invalidate();
            }
            motion.Checked = capri.Motion;
            onTop.Checked = capri.OnTop;
            watch.Checked = capri.Watching;
            speak.Checked = capri.Speaking;
            freeVoice.Checked = capri.FreeVoice;
            int chosen = Array.IndexOf(Gaps, capri.GlanceGap);
            gap.Value = chosen < 0 ? 5 : chosen;
            gapName.Text = GapNames[gap.Value];
            syncing = false;
        }

        internal string Status { get { return brainName.Text + " | " + brainNote.Text; } }

        internal bool WatchShown { get { return watch.Checked; } }

        internal bool SpeakShown { get { return speak.Checked; } }

        internal bool FreeVoiceShown { get { return freeVoice.Checked; } }

        internal bool FreeVoiceOffered { get { return freeVoice.Visible; } }

        internal string Overlap()
        {
            for (int i = 0; i < Controls.Count; i++)
                for (int j = i + 1; j < Controls.Count; j++)
                {
                    Control a = Controls[i], b = Controls[j];
                    if (!a.Visible || !b.Visible || (a is Label && a.Text.Length == 0) || (b is Label && b.Text.Length == 0)) continue;
                    if (a.Bounds.IntersectsWith(b.Bounds)) return a.Text + " / " + b.Text;
                }
            return null;
        }

        internal string GapShown { get { return gapName.Text; } }

        internal void SlideGap(int steps) { gap.Focus(); for (int i = 0; i < Math.Abs(steps); i++) { KeyEventArgs press = new KeyEventArgs(steps > 0 ? Keys.Right : Keys.Left); typeof(Control).GetMethod("OnKeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(gap, new object[] { press }); } }

        internal void Searching()
        {
            brainName.Text = "Looking for a brain...";
            brainNote.Text = "";
            models.Visible = false;
            hint.Visible = false;
            Arrange(false);
        }

        internal void ShowOllama(string[] installed, string chosen)
        {
            brainName.Text = "Ollama · " + chosen;
            brainNote.Text = "Running on this PC";
            models.Text = chosen;
            models.Visible = true;
            hint.Visible = false;
            lastInstalled = installed;
            modelMenu.Items.Clear();
            foreach (string name in installed)
            {
                string pick = name;
                ToolStripMenuItem item = new ToolStripMenuItem(name, null, delegate { capri.ChooseModel(pick); ShowOllama(lastInstalled, pick); });
                item.Checked = name == chosen;
                modelMenu.Items.Add(item);
            }
            Arrange(false);
        }

        internal void ShowCloud(Cloud remote, List<string> found)
        {
            brainName.Text = remote.Name + " · " + remote.Model;
            brainNote.Text = "Using your " + remote.Company + " key";
            models.Text = remote.Model;
            models.Visible = true;
            hint.Visible = false;
            modelMenu.Items.Clear();
            foreach (string name in found ?? new List<string> { remote.Model })
            {
                string pick = name;
                modelMenu.Items.Add(Choice(name, name == remote.Model, delegate { capri.ChooseModel(pick); ShowCloud(remote, found); }));
            }
            Arrange(false);
        }

        internal void ShowTextKey(string tail)
        {
            textSaved = tail != null;
            freeKey.Visible = !textSaved;
            keyNote.Visible = !textSaved;
            textEntry.Cue = textSaved ? "Key saved, ends " + tail : "Optional: NVIDIA/GPT/Claude/DeepSeek";
            RelabelText();
        }

        internal bool KeyNoteFits { get { return keyNote.Visible && keyNote.Top >= textEntry.Bottom && keyNote.Bottom <= keyHelp.Top && keyNote.Right <= ClientSize.Width; } }

        internal string TextStatus { get { return brainName.Text + " | " + brainNote.Text + " | " + textEntry.Cue + " | " + textConnect.Text + " | " + (freeKey.Visible ? "free" : "-"); } }

        private void ConnectText()
        {
            string typed = textEntry.Box.Text.Trim();
            if (typed.Length > 0) { textEntry.Box.Text = ""; capri.ConnectText(typed); }
            else if (textSaved) capri.ForgetText();
            else textEntry.Box.Focus();
        }

        private void RelabelText()
        {
            bool forget = textSaved && textEntry.Box.Text.Trim().Length == 0;
            textConnect.Text = forget ? "Forget" : "Connect";
            textConnect.Accent = forget ? Look.Red : Look.Ink;
            textConnect.Invalidate();
        }

        internal void ShowLocal(Pick pick)
        {
            brainName.Text = "Built-in · " + pick.Name;
            brainNote.Text = "Running on this PC";
            models.Visible = false;
            hint.Visible = false;
            Arrange(false);
        }

        internal void ShowSetup(Machine machine, Pick pick, bool viaOllama, string problem)
        {
            brainName.Text = problem == null ? "No brain yet" : "Setup stopped";
            brainNote.Text = problem ?? "Best fit for this PC: " + pick.Name;
            models.Visible = false;
            Step(0, "✓", machine.Describe(), Look.Ink);
            Step(1, "2", "Download " + pick.Name + " (" + pick.Download + ")" + (viaOllama ? " with Ollama" : ""), Look.Dim);
            Step(2, "3", "Wake her up", Look.Dim);
            filled = 0;
            setup.Text = problem == null ? "Set up her brain" : "Try again";
            setup.Enabled = true;
            hint.Visible = !viaOllama;
            Arrange(true);
        }

        internal void ShowProgress(int step, string text, long done, long total)
        {
            Step(1, step > 1 ? "✓" : "2", stepText[1], step == 1 ? Look.Cyan : Look.Ink);
            Step(2, "3", stepText[2], step == 2 ? Look.Cyan : Look.Dim);
            brainName.Text = step == 1 ? "Setting up her brain" : "Waking her up";
            brainNote.Text = total > 0 ? text + " · " + Engine.Amount(done) + " of " + Engine.Amount(total) : text;
            filled = total > 0 ? Math.Min(1f, done / (float)total) : step == 2 ? 1f : 0f;
            setup.Text = step == 1 ? "Cancel" : "Starting...";
            setup.Enabled = step == 1;
            hint.Visible = false;
            if (!expanded) Arrange(true);
            else Invalidate(new Rectangle(0, Scaled(204), Width, Scaled(18)));
        }

        internal void ShowProblem(string problem) { brainNote.Text = problem; }

        internal void ShowVoice(string tail, string voiceId, string model, VoiceListing listing)
        {
            keySaved = tail != null;
            keyEntry.Cue = keySaved ? "Key saved, ends " + tail : "Paste ElevenLabs API key";
            Relabel();
            keyHelp.Visible = !keySaved;
            shortcut.Visible = keySaved && shortcutReady;
            if (!voiceEntry.Box.Focused || voiceEntry.Box.Text.Trim().Length == 0) voiceEntry.Box.Text = voiceId;
            voiceMenu.Items.Clear();
            voiceEntry.Chevron = listing != null && listing.Voices.Count > 0;
            speechModelMenu.Items.Clear();
            speechModelMenu.Items.Add(Choice("Default model", string.IsNullOrEmpty(model), delegate { capri.ChooseVoiceModel(null); }));
            speechModels.Text = string.IsNullOrEmpty(model) ? "Default model" : model;
            if (listing != null)
            {
                foreach (VoiceEntry entry in listing.Voices)
                {
                    string id = entry.Id;
                    voiceMenu.Items.Add(Choice(entry.Name ?? id, id == voiceId, delegate { voiceEntry.Box.Text = id; capri.ChooseVoice(id); }));
                }
                foreach (VoiceModel option in listing.Models)
                {
                    string id = option.Id;
                    string name = option.Name ?? id;
                    if (id == model) speechModels.Text = name.StartsWith("Eleven ", StringComparison.OrdinalIgnoreCase) ? name.Substring(7) : name;
                    speechModelMenu.Items.Add(Choice(name, id == model, delegate { capri.ChooseVoiceModel(id); }));
                }
            }
            speechModels.Invalidate();
        }

        internal void ShowShortcut(bool talk, bool mute)
        {
            shortcutReady = talk;
            shortcut.Visible = keySaved && talk;
            muteShortcut.Visible = mute;
        }

        internal void ShowMic(string name)
        {
            mics.Text = "Mic: " + (name ?? "default");
            mics.Invalidate();
        }

        private void ShowMics()
        {
            string chosen = capri.MicId;
            List<Mic> found = Ears.List();
            micMenu.Items.Clear();
            micMenu.Items.Add(Choice("Windows default", string.IsNullOrEmpty(chosen), delegate { capri.ChooseMic(null); }));
            if (found.Count > 0) micMenu.Items.Add(new ToolStripSeparator());
            foreach (Mic mic in found)
            {
                string id = mic.Id;
                micMenu.Items.Add(Choice(mic.Name, id == chosen, delegate { capri.ChooseMic(id); }));
            }
            micMenu.Show(mics, new Point(0, mics.Height));
        }

        internal string MicStatus { get { return "mic | " + mics.Text + " | " + (keyHelp.Visible ? "help" : "-") + " | " + (shortcut.Visible ? "keys" : "-") + " | " + (muteShortcut.Visible ? "mute" : "-"); } }

        internal void VoiceNote(string text, bool problem)
        {
            voiceNote.Text = text;
            voiceNote.ForeColor = problem ? Look.Red : Look.Dim;
        }

        internal void UpdateNote(string text, bool problem)
        {
            updateNote.Text = text;
            updateNote.ForeColor = problem ? Look.Red : Look.Dim;
        }

        internal string UpdateStatus { get { return updateNote.Text; } }

        internal string VoiceStatus { get { return voiceNote.Text + " | " + voiceEntry.Box.Text + " | " + speechModels.Text + " | " + connect.Text; } }
        private void Connect()
        {
            string typed = keyEntry.Box.Text.Trim();
            if (typed.Length > 0) { keyEntry.Box.Text = ""; capri.ConnectVoice(typed); }
            else if (keySaved) capri.ForgetVoice();
            else keyEntry.Box.Focus();
        }

        private void Relabel()
        {
            bool forget = keySaved && keyEntry.Box.Text.Trim().Length == 0;
            connect.Text = forget ? "Forget" : "Connect";
            connect.Accent = forget ? Look.Red : Look.Ink;
            connect.Invalidate();
        }

        private static ToolStripMenuItem Choice(string text, bool chosen, EventHandler click)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text, null, click);
            item.Checked = chosen;
            return item;
        }

        private ContextMenuStrip Dropdown()
        {
            ContextMenuStrip strip = new ContextMenuStrip();
            strip.Renderer = new ToolStripProfessionalRenderer(new CapriMenuColors());
            strip.BackColor = Look.Panel;
            strip.ForeColor = Look.Ink;
            strip.Font = Font;
            strip.ShowImageMargin = false;
            strip.ShowCheckMargin = true;
            return strip;
        }

        private Entry Input(int x, int y, int width)
        {
            Entry entry = new Entry();
            entry.Bounds = new Rectangle(x, y, width, 28);
            Controls.Add(entry);
            return entry;
        }

        private void Step(int index, string mark, string text, Color colour)
        {
            stepText[index] = text;
            steps[index].Text = mark + "  " + text;
            steps[index].ForeColor = colour;
        }

        private Label Caption(string text, int x, int y, Font font, Color colour)
        {
            Label label = new Note();
            label.Text = text;
            label.Font = font;
            label.ForeColor = colour;
            label.BackColor = Look.Panel;
            label.AutoSize = true;
            label.Location = new Point(x, y);
            Controls.Add(label);
            return label;
        }

        private Key Make(string text, int x, int y, int width, int height, EventHandler click)
        {
            Key key = new Key();
            key.Text = text;
            key.Bounds = new Rectangle(x, y, width, height);
            key.Click += click;
            Controls.Add(key);
            return key;
        }

        private Toggle Check(string text, int x, int y, int width, EventHandler changed)
        {
            Toggle box = new Toggle();
            box.Text = text;
            box.Bounds = new Rectangle(x, y, width, 22);
            box.CheckedChanged += changed;
            Controls.Add(box);
            return box;
        }

        private sealed class Note : Label
        {
            internal Note() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); }

            protected override void OnPaintBackground(PaintEventArgs e) { Backdrop(e.Graphics, this, ((Mainframe)Parent).DeviceScale()); }

            protected override void WndProc(ref Message message)
            {
                if (message.Msg == 0x0084) { message.Result = new IntPtr(-1); return; }
                base.WndProc(ref message);
            }
        }

        private sealed class Key : Button
        {
            internal Color Accent = Look.Ink;
            internal bool Loud;
            internal bool Picked;
            internal bool Chevron;
            internal int Glyph;
            private bool hot;
            private bool down;

            internal Key()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
                BackColor = Look.Panel;
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }

            protected override void OnMouseLeave(EventArgs e) { hot = false; down = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }

            protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                float s = ((Mainframe)Parent).DeviceScale();
                Graphics graphics = e.Graphics;
                Backdrop(graphics, this, s);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                bool lit = hot || Picked || (Focused && ShowFocusCues);
                Color hotColour = Accent == Look.Ink ? Look.Cyan : Accent;
                if (Glyph == 0 || hot)
                {
                    using (GraphicsPath plate = Look.Plate(new RectangleF(0.7f * s, 0.7f * s, Width - 1.4f * s - 1, Height - 1.4f * s - 1), 6 * s, -1, Height / 2f, 0, true))
                    {
                        using (Brush fill = new SolidBrush(down ? Pressed : hot ? Hover : Field)) graphics.FillPath(fill, plate);
                        if (Glyph == 0)
                        {
                            Color edge = !Enabled ? Field : lit ? hotColour : Loud ? Color.FromArgb(170, Accent) : Edge;
                            using (Pen pen = new Pen(edge, 1.4f * s)) graphics.DrawPath(pen, plate);
                        }
                    }
                }
                Color ink = !Enabled ? Look.Dim : Picked ? Look.Cyan : Accent;
                float x = Width / 2f;
                float y = Height / 2f;
                if (Glyph != 0)
                {
                    using (Pen pen = new Pen(hot ? (Glyph == 2 ? Look.Red : Look.Cyan) : Look.Dim, 1.5f * s))
                    {
                        if (Glyph == 1) graphics.DrawLine(pen, x - 5 * s, y, x + 5 * s, y);
                        else
                        {
                            graphics.DrawLine(pen, x - 4.5f * s, y - 4.5f * s, x + 4.5f * s, y + 4.5f * s);
                            graphics.DrawLine(pen, x - 4.5f * s, y + 4.5f * s, x + 4.5f * s, y - 4.5f * s);
                        }
                    }
                    return;
                }
                if (Chevron)
                {
                    float left = Width - 18 * s;
                    using (Brush mark = new SolidBrush(Look.Dim)) graphics.FillPolygon(mark, new[] { new PointF(left, y - 2 * s), new PointF(left + 8 * s, y - 2 * s), new PointF(left + 4 * s, y + 3 * s) });
                }
                Rectangle area = Chevron ? new Rectangle((int)(10 * s), 0, Width - (int)(30 * s), Height) : ClientRectangle;
                TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | (Chevron ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter);
                TextRenderer.DrawText(graphics, Text, Font, area, ink, flags);
            }
        }

        private sealed class Entry : Control
        {
            internal readonly TextBox Box = new TextBox();
            internal event EventHandler Drop;
            private bool chevron;
            private string cue = "";
            private bool hot;

            internal Entry()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = Look.Panel;
                Cursor = Cursors.IBeam;
                Box.BorderStyle = BorderStyle.None;
                Box.BackColor = Field;
                Box.ForeColor = Look.Ink;
                Box.GotFocus += delegate { Invalidate(); };
                Box.LostFocus += delegate { Invalidate(); };
                Box.HandleCreated += delegate { ShowCue(); };
                Controls.Add(Box);
            }

            internal string Cue { get { return cue; } set { cue = value ?? ""; ShowCue(); } }

            internal bool Chevron { get { return chevron; } set { chevron = value; PerformLayout(); Invalidate(); } }

            private float Unit()
            {
                Mainframe owner = Parent as Mainframe;
                return owner == null ? 1f : owner.DeviceScale();
            }

            private bool OnChevron(int x) { return Chevron && x >= Width - 28 * Unit(); }

            private void ShowCue() { if (Box.IsHandleCreated) Native.SendMessage(Box.Handle, 0x1501, new IntPtr(1), cue); }

            protected override void OnLayout(LayoutEventArgs e)
            {
                base.OnLayout(e);
                float s = Unit();
                int left = (int)(10 * s);
                int right = (int)((Chevron ? 28 : 10) * s);
                int height = Box.PreferredHeight;
                Box.SetBounds(left, (Height - height) / 2, Math.Max(1, Width - left - right), height);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                bool over = OnChevron(e.X);
                Cursor = over ? Cursors.Hand : Cursors.IBeam;
                if (over != hot) { hot = over; Invalidate(); }
                base.OnMouseMove(e);
            }

            protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (!OnChevron(e.X)) Box.Focus();
                    else if (Drop != null) Drop(this, EventArgs.Empty);
                }
                base.OnMouseDown(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                float s = Unit();
                Graphics graphics = e.Graphics;
                Backdrop(graphics, this, s);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath plate = Look.Plate(new RectangleF(0.7f * s, 0.7f * s, Width - 1.4f * s - 1, Height - 1.4f * s - 1), 6 * s, -1, Height / 2f, 0, true))
                {
                    using (Brush fill = new SolidBrush(Field)) graphics.FillPath(fill, plate);
                    using (Pen pen = new Pen(Box.Focused ? Look.Cyan : Edge, 1.4f * s)) graphics.DrawPath(pen, plate);
                }
                if (!Chevron) return;
                float left = Width - 18 * s;
                float y = Height / 2f;
                using (Brush mark = new SolidBrush(hot ? Look.Cyan : Look.Dim)) graphics.FillPolygon(mark, new[] { new PointF(left, y - 2 * s), new PointF(left + 8 * s, y - 2 * s), new PointF(left + 4 * s, y + 3 * s) });
            }
        }

        private sealed class Slider : Control
        {
            private readonly string[] labels;
            private int value;
            private bool hot;
            private bool sliding;
            internal event EventHandler Changed;

            internal Slider(string[] labels)
            {
                this.labels = labels;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Selectable, true);
                BackColor = Look.Panel;
                Cursor = Cursors.Hand;
                TabStop = true;
                AccessibleRole = AccessibleRole.Slider;
            }

            internal int Value
            {
                get { return value; }
                set
                {
                    int next = Math.Max(0, Math.Min(labels.Length - 1, value));
                    if (next == this.value) return;
                    this.value = next;
                    AccessibleDescription = labels[next];
                    Invalidate();
                }
            }

            private void Pick(int next)
            {
                int before = value;
                Value = next;
                if (value != before && Changed != null) Changed(this, EventArgs.Empty);
            }

            private float Unit() { return ((Mainframe)Parent).DeviceScale(); }

            private int At(int x)
            {
                float s = Unit();
                float start = 8 * s;
                float span = Width - 16 * s;
                return (int)Math.Round((x - start) / span * (labels.Length - 1));
            }

            protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }

            protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnMouseDown(MouseEventArgs e) { Focus(); sliding = true; Pick(At(e.X)); base.OnMouseDown(e); }

            protected override void OnMouseMove(MouseEventArgs e) { if (sliding) Pick(At(e.X)); base.OnMouseMove(e); }

            protected override void OnMouseUp(MouseEventArgs e) { sliding = false; base.OnMouseUp(e); }

            protected override bool IsInputKey(Keys key) { return key == Keys.Left || key == Keys.Right || base.IsInputKey(key); }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Left) Pick(value - 1);
                else if (e.KeyCode == Keys.Right) Pick(value + 1);
                base.OnKeyDown(e);
            }

            protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }

            protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                float s = Unit();
                Graphics graphics = e.Graphics;
                Backdrop(graphics, this, s);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float start = 8 * s;
                float span = Width - 16 * s;
                float middle = Height / 2f;
                float thumb = start + span * value / (labels.Length - 1);
                using (Brush track = new SolidBrush(Track)) graphics.FillRectangle(track, start, middle - 2 * s, span, 4 * s);
                using (Brush lit = new SolidBrush(Look.Cyan)) graphics.FillRectangle(lit, start, middle - 2 * s, thumb - start, 4 * s);
                using (Brush tick = new SolidBrush(Edge))
                    for (int i = 0; i < labels.Length; i++) graphics.FillRectangle(tick, start + span * i / (labels.Length - 1) - 0.5f * s, middle + 5 * s, 1.5f * s, 4 * s);
                RectangleF knob = new RectangleF(thumb - 5 * s, middle - 8 * s, 10 * s, 16 * s);
                using (Brush fill = new SolidBrush(Field)) graphics.FillRectangle(fill, knob);
                using (Pen edge = new Pen(hot || Focused || sliding ? Look.Cyan : Look.Ink, 1.6f * s)) graphics.DrawRectangle(edge, knob.X, knob.Y, knob.Width, knob.Height);
            }
        }

        private sealed class Toggle : CheckBox
        {
            private bool hot;
            internal string Hint;
            internal Font HintFont;

            internal Toggle()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
                BackColor = Look.Panel;
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }

            protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                float s = ((Mainframe)Parent).DeviceScale();
                Graphics graphics = e.Graphics;
                Backdrop(graphics, this, s);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float side = 13 * s;
                RectangleF box = new RectangleF(s, (Height - side) / 2, side, side);
                using (Brush fill = new SolidBrush(Field)) graphics.FillRectangle(fill, box);
                using (Pen edge = new Pen(hot || (Focused && ShowFocusCues) ? Look.Cyan : Edge, 1.4f * s)) graphics.DrawRectangle(edge, box.X, box.Y, box.Width, box.Height);
                if (Checked) using (Brush mark = new SolidBrush(Look.Cyan)) graphics.FillRectangle(mark, box.X + 3.5f * s, box.Y + 3.5f * s, box.Width - 7 * s, box.Height - 7 * s);
                int left = (int)(box.Right + 9 * s);
                TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
                TextRenderer.DrawText(graphics, Text, Font, new Rectangle(left, 0, Width - left, Height), Look.Ink, flags);
                if (Hint == null) return;
                int after = left + TextRenderer.MeasureText(graphics, Text, Font, Size.Empty, flags).Width + (int)(14 * s);
                TextRenderer.DrawText(graphics, Hint, HintFont ?? Font, new Rectangle(after, 0, Math.Max(0, Width - after), Height), Look.Dim, flags);
            }
        }
    }
}
