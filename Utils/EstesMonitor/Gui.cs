// Estes Monitor window app: scan or watch the Estes folders, browse findings, export to CSV.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EstesMonitor
{
    internal sealed class Finding
    {
        public int Seq;
        public DateTime Time;
        public string Severity;
        public string Area;
        public string File;
        public long Line = -1;
        public int Count = 1;
        public string Message;
    }

    internal sealed class BufferedListView : ListView
    {
        public BufferedListView() { DoubleBuffered = true; }
    }

    internal sealed class MainForm : Form
    {
        const string Version = "1.0.0";
        const int MaxRows = 50000;

        static readonly string[] HistoryLabels = { "New lines only", "Last 1 hour", "Last 6 hours", "Last 24 hours", "Last 3 days", "Last 7 days", "All history" };
        static readonly int[] HistoryHours = { 0, 1, 6, 24, 72, 168, -1 };

        readonly string configPathArg;
        readonly bool autoScan;
        readonly string exportPath;

        Settings settings;
        string configSource;

        CheckedListBox areaList;
        ComboBox historyBox;
        NumericUpDown intervalBox;
        CheckBox warnBox;
        Button scanBtn, startBtn, stopBtn, clearBtn, exportBtn, configBtn;
        Label configLabel;
        TextBox filterBox;
        ListView findingsView, foldersView;
        ToolStripStatusLabel stateLabel, countsLabel, lastLabel;

        readonly List<Finding> all = new List<Finding>();
        List<Finding> view = new List<Finding>();
        int seq;
        int sortCol;
        bool sortAsc;
        long errorLines;
        int filesWatched;
        int eventSources;
        DateTime lastScan = DateTime.MinValue;
        bool running;
        ManualResetEvent stopEvent;

        public MainForm(string configPathArg, bool autoScan, string exportPath)
        {
            this.configPathArg = configPathArg;
            this.autoScan = autoScan;
            this.exportPath = exportPath;

            BuildUi();
            LoadSettings(configPathArg, true);

            FormClosing += delegate { if (stopEvent != null) stopEvent.Set(); };
            Shown += delegate { OnShownHandler(); };
        }

        void OnShownHandler()
        {
            if (exportPath != null)
            {
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
            }
            if (autoScan || exportPath != null) StartRun(true);
        }

        // ---------- UI construction ----------

        void BuildUi()
        {
            Text = "Estes Monitor " + Version + " - " + Environment.MachineName;
            Font = new Font("Segoe UI", 9f);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1200, 740);
            MinimumSize = new Size(960, 540);

            // Tabs: Findings + Folders
            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            TabPage findingsTab = new TabPage("Findings");
            TabPage foldersTab = new TabPage("Folders");
            tabs.TabPages.Add(findingsTab);
            tabs.TabPages.Add(foldersTab);

            Panel filterPanel = new Panel();
            filterPanel.Dock = DockStyle.Top;
            filterPanel.Height = 34;
            Label filterLabel = new Label();
            filterLabel.Text = "Filter:";
            filterLabel.AutoSize = true;
            filterLabel.Location = new Point(8, 10);
            filterBox = new TextBox();
            filterBox.Location = new Point(55, 6);
            filterBox.Width = 340;
            filterBox.TextChanged += delegate { RefreshView(); };
            Label filterHint = new Label();
            filterHint.Text = "matches any column; double-click a row to open its folder; Export saves what is shown";
            filterHint.AutoSize = true;
            filterHint.ForeColor = SystemColors.GrayText;
            filterHint.Location = new Point(410, 10);
            filterPanel.Controls.Add(filterLabel);
            filterPanel.Controls.Add(filterBox);
            filterPanel.Controls.Add(filterHint);

            findingsView = new BufferedListView();
            findingsView.Dock = DockStyle.Fill;
            findingsView.View = System.Windows.Forms.View.Details;
            findingsView.FullRowSelect = true;
            findingsView.GridLines = true;
            findingsView.HideSelection = false;
            findingsView.MultiSelect = false;
            findingsView.VirtualMode = true;
            findingsView.Columns.Add("Time", 140);
            findingsView.Columns.Add("Severity", 75);
            findingsView.Columns.Add("Area", 150);
            findingsView.Columns.Add("File", 380);
            findingsView.Columns.Add("Line", 60);
            findingsView.Columns.Add("Count", 60);
            findingsView.Columns.Add("Message", 600);
            findingsView.RetrieveVirtualItem += OnRetrieveVirtualItem;
            findingsView.ColumnClick += OnColumnClick;
            findingsView.MouseDoubleClick += delegate { OpenSelectedFolder(); };

            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem copyItem = new ToolStripMenuItem("Copy message");
            copyItem.Click += delegate { CopySelectedMessage(); };
            ToolStripMenuItem openItem = new ToolStripMenuItem("Open containing folder");
            openItem.Click += delegate { OpenSelectedFolder(); };
            menu.Items.Add(copyItem);
            menu.Items.Add(openItem);
            findingsView.ContextMenuStrip = menu;

            findingsTab.Controls.Add(findingsView);
            findingsTab.Controls.Add(filterPanel);

            foldersView = new BufferedListView();
            foldersView.Dock = DockStyle.Fill;
            foldersView.View = System.Windows.Forms.View.Details;
            foldersView.FullRowSelect = true;
            foldersView.GridLines = true;
            foldersView.Columns.Add("Area", 220);
            foldersView.Columns.Add("Folder / Event Viewer source", 560);
            foldersView.Columns.Add("Status", 110);
            foldersView.Columns.Add("Files / events", 100);
            foldersTab.Controls.Add(foldersView);

            // Top panel: areas + options + buttons
            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 150;
            top.Padding = new Padding(8, 4, 8, 4);

            GroupBox areaBox = new GroupBox();
            areaBox.Text = "Estes areas (checked = monitored)";
            areaBox.Dock = DockStyle.Left;
            areaBox.Width = 320;
            areaList = new CheckedListBox();
            areaList.Dock = DockStyle.Fill;
            areaList.CheckOnClick = true;
            areaList.IntegralHeight = false;
            areaBox.Controls.Add(areaList);

            Panel opts = new Panel();
            opts.Dock = DockStyle.Fill;

            Label l1 = new Label();
            l1.Text = "Start with:";
            l1.AutoSize = true;
            l1.Location = new Point(14, 14);
            historyBox = new ComboBox();
            historyBox.DropDownStyle = ComboBoxStyle.DropDownList;
            historyBox.Items.AddRange(HistoryLabels);
            historyBox.Location = new Point(84, 10);
            historyBox.Width = 150;

            warnBox = new CheckBox();
            warnBox.Text = "Include warnings";
            warnBox.AutoSize = true;
            warnBox.Location = new Point(256, 12);
            warnBox.CheckedChanged += delegate { if (settings != null) settings.IncludeWarnings = warnBox.Checked; };

            Label l2 = new Label();
            l2.Text = "Check every";
            l2.AutoSize = true;
            l2.Location = new Point(398, 14);
            intervalBox = new NumericUpDown();
            intervalBox.Minimum = 1;
            intervalBox.Maximum = 3600;
            intervalBox.Value = 30;
            intervalBox.Location = new Point(474, 10);
            intervalBox.Width = 64;
            Label l3 = new Label();
            l3.Text = "seconds";
            l3.AutoSize = true;
            l3.Location = new Point(544, 14);

            scanBtn = MakeButton("Scan once", 14, 52, 110);
            startBtn = MakeButton("Start monitoring", 132, 52, 130);
            stopBtn = MakeButton("Stop", 270, 52, 80);
            clearBtn = MakeButton("Clear", 358, 52, 80);
            exportBtn = MakeButton("Export to CSV...", 446, 52, 130);
            configBtn = MakeButton("Load config...", 584, 52, 120);
            scanBtn.Click += delegate { StartRun(true); };
            startBtn.Click += delegate { StartRun(false); };
            stopBtn.Click += delegate { if (stopEvent != null) stopEvent.Set(); stateLabel.Text = "Stopping..."; };
            clearBtn.Click += delegate { ClearFindings(); };
            exportBtn.Click += delegate { ExportDialog(); };
            configBtn.Click += delegate { LoadConfigDialog(); };

            configLabel = new Label();
            configLabel.AutoEllipsis = true;
            configLabel.Location = new Point(14, 100);
            configLabel.Size = new Size(690, 20);
            configLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            configLabel.ForeColor = SystemColors.GrayText;

            opts.Controls.Add(l1);
            opts.Controls.Add(historyBox);
            opts.Controls.Add(warnBox);
            opts.Controls.Add(l2);
            opts.Controls.Add(intervalBox);
            opts.Controls.Add(l3);
            opts.Controls.Add(scanBtn);
            opts.Controls.Add(startBtn);
            opts.Controls.Add(stopBtn);
            opts.Controls.Add(clearBtn);
            opts.Controls.Add(exportBtn);
            opts.Controls.Add(configBtn);
            opts.Controls.Add(configLabel);

            top.Controls.Add(opts);
            top.Controls.Add(areaBox);

            StatusStrip status = new StatusStrip();
            stateLabel = new ToolStripStatusLabel("Idle");
            stateLabel.AutoSize = false;
            stateLabel.Width = 250;
            stateLabel.TextAlign = ContentAlignment.MiddleLeft;
            countsLabel = new ToolStripStatusLabel("");
            countsLabel.Spring = true;
            countsLabel.TextAlign = ContentAlignment.MiddleLeft;
            lastLabel = new ToolStripStatusLabel("No scan yet");
            status.Items.Add(stateLabel);
            status.Items.Add(countsLabel);
            status.Items.Add(lastLabel);

            Controls.Add(tabs);
            Controls.Add(top);
            Controls.Add(status);

            SetRunning(false);
            UpdateStatus();
        }

        static Button MakeButton(string text, int x, int y, int w)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, 32);
            return b;
        }

        // ---------- settings / config ----------

        void LoadSettings(string path, bool useDefaultsOnError)
        {
            string source;
            Settings s;
            try
            {
                s = Config.Load(path, out source);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load the config:\r\n\r\n" + ex.Message + (useDefaultsOnError ? "\r\n\r\nBuilt-in defaults will be used." : ""),
                    "Estes Monitor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (!useDefaultsOnError) return;
                s = Config.Parse(Config.DefaultJson);
                source = "built-in defaults";
            }

            settings = s;
            configSource = source;
            configLabel.Text = "Config: " + source;

            areaList.Items.Clear();
            foreach (KeyValuePair<string, List<string>> a in settings.Areas)
                areaList.Items.Add(a.Key + "  (" + a.Value.Count + (a.Value.Count == 1 ? " source)" : " sources)"), a.Value.Count > 0);

            int idx = Array.IndexOf(HistoryHours, settings.InitialScanHours);
            historyBox.SelectedIndex = idx >= 0 ? idx : 3;
            intervalBox.Value = Math.Max(intervalBox.Minimum, Math.Min(intervalBox.Maximum, settings.PollSeconds));
            warnBox.Checked = settings.IncludeWarnings;

            RefreshFolders();
        }

        void LoadConfigDialog()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "Config files (*.json)|*.json|All files (*.*)|*.*";
                dlg.Title = "Load Estes Monitor config";
                if (dlg.ShowDialog(this) == DialogResult.OK) LoadSettings(dlg.FileName, false);
            }
        }

        List<KeyValuePair<string, List<string>>> SelectedAreas()
        {
            List<KeyValuePair<string, List<string>>> r = new List<KeyValuePair<string, List<string>>>();
            for (int i = 0; i < areaList.Items.Count; i++)
                if (areaList.GetItemChecked(i)) r.Add(settings.Areas[i]);
            return r;
        }

        // ---------- running ----------

        void SetRunning(bool value)
        {
            running = value;
            scanBtn.Enabled = !value;
            startBtn.Enabled = !value;
            configBtn.Enabled = !value;
            historyBox.Enabled = !value;
            intervalBox.Enabled = !value;
            areaList.Enabled = !value;
            stopBtn.Enabled = value;
        }

        void StartRun(bool once)
        {
            if (running) return;
            List<KeyValuePair<string, List<string>>> areas = SelectedAreas();
            if (areas.Count == 0)
            {
                MessageBox.Show(this, "Check at least one area.", "Estes Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int hours = HistoryHours[historyBox.SelectedIndex];
            int poll = (int)intervalBox.Value;
            settings.IncludeWarnings = warnBox.Checked;
            ManualResetEvent stop = new ManualResetEvent(false);
            stopEvent = stop;

            SetRunning(true);
            stateLabel.Text = once ? "Scanning..." : "Monitoring (every " + poll + " s)";

            Thread t = new Thread(delegate() { Worker(areas, hours, once, poll, stop); });
            t.IsBackground = true;
            t.Start();
        }

        void Worker(List<KeyValuePair<string, List<string>>> areas, int hours, bool once, int poll, ManualResetEvent stop)
        {
            try
            {
                MonitorEngine engine = new MonitorEngine(settings);
                engine.Log = delegate(string m) { PostFindings(new List<Finding> { Info(m, DateTime.Now) }, -1, -1, 0, DateTime.Now); };
                DateTime cutoff = hours < 0 ? DateTime.MinValue : DateTime.Now.AddHours(-hours);
                bool initial = true;

                while (true)
                {
                    int files;
                    DateTime now = DateTime.Now;
                    List<AlertGroup> groups = engine.RunCycle(areas, initial, cutoff, out files);
                    initial = false;
                    int sources = engine.EventSourcesQueried;

                    long errs = groups.Where(g => g.Severity == "ERROR").Sum(g => (long)g.Count);
                    PostFindings(ToFindings(groups, now), files, sources, errs, now);

                    if (once || stop.WaitOne(poll * 1000)) break;
                }
            }
            catch (Exception ex)
            {
                PostFindings(new List<Finding> { Info("Monitor stopped by an error: " + ex.Message, DateTime.Now) }, -1, -1, 0, DateTime.Now);
            }
            Post(delegate { Finished(); });
        }

        void Post(Action a)
        {
            try { if (!IsDisposed && IsHandleCreated) BeginInvoke(a); }
            catch (InvalidOperationException) { }
        }

        void PostFindings(List<Finding> items, int files, int sources, long errs, DateTime when)
        {
            Post(delegate { AddResults(items, files, sources, errs, when); });
        }

        Finding Info(string message, DateTime when)
        {
            Finding f = new Finding();
            f.Time = when;
            f.Severity = "INFO";
            f.Message = message;
            return f;
        }

        List<Finding> ToFindings(List<AlertGroup> groups, DateTime now)
        {
            List<Finding> result = new List<Finding>();
            Dictionary<string, int> printed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> hidden = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (AlertGroup g in groups
                .OrderBy(x => x.Severity == "ERROR" ? 0 : 1)
                .ThenBy(x => x.Area)
                .ThenBy(x => x.File)
                .ThenByDescending(x => x.Count))
            {
                int n;
                printed.TryGetValue(g.File, out n);
                if (n >= settings.MaxAlertGroupsPerFile)
                {
                    int h;
                    hidden.TryGetValue(g.File, out h);
                    hidden[g.File] = h + 1;
                    continue;
                }
                printed[g.File] = n + 1;

                Finding f = new Finding();
                f.Time = now;
                f.Severity = g.Severity;
                f.Area = g.Area;
                f.File = g.File;
                f.Line = g.FirstLine;
                f.Count = g.Count;
                f.Message = g.Sample;
                result.Add(f);
            }

            foreach (KeyValuePair<string, int> kv in hidden)
            {
                Finding f = Info(kv.Value + " more distinct message(s) not shown for this file (maxAlertGroupsPerFile)", now);
                f.File = kv.Key;
                result.Add(f);
            }
            return result;
        }

        // ---------- UI-thread updates ----------

        void AddResults(List<Finding> items, int files, int sources, long errs, DateTime when)
        {
            foreach (Finding f in items) f.Seq = ++seq;
            all.AddRange(items);
            if (all.Count > MaxRows) all.RemoveRange(0, all.Count - MaxRows);

            errorLines += errs;
            if (files >= 0)
            {
                filesWatched = files;
                eventSources = sources;
                lastScan = when;
            }
            RefreshView();
            UpdateStatus();
        }

        void Finished()
        {
            SetRunning(false);
            stateLabel.Text = "Idle";
            UpdateStatus();
            RefreshFolders();

            if (exportPath != null)
            {
                int rc = 0;
                try { WriteCsv(exportPath); if (errorLines > 0) rc = 1; }
                catch (Exception) { rc = 2; }
                GuiProgram.ExitCode = rc;
                Close();
            }
        }

        void ClearFindings()
        {
            all.Clear();
            errorLines = 0;
            RefreshView();
            UpdateStatus();
        }

        void UpdateStatus()
        {
            countsLabel.Text = "Files watched: " + filesWatched + "    Event sources: " + eventSources + "    Error lines/events: " + errorLines + "    Rows shown: " + view.Count + " of " + all.Count;
            lastLabel.Text = lastScan == DateTime.MinValue ? "No scan yet" : "Last scan: " + lastScan.ToString("yyyy-MM-dd HH:mm:ss");
        }

        static int SevRank(string s)
        {
            return s == "ERROR" ? 0 : (s == "WARNING" ? 1 : 2);
        }

        static bool Matches(Finding f, string text)
        {
            return Has(f.Severity, text) || Has(f.Area, text) || Has(f.File, text) || Has(f.Message, text)
                || Has(f.Time.ToString("yyyy-MM-dd HH:mm:ss"), text);
        }

        static bool Has(string s, string text)
        {
            return s != null && s.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        int Compare(Finding a, Finding b)
        {
            int p;
            switch (sortCol)
            {
                case 0: p = a.Time.CompareTo(b.Time); break;
                case 1: p = SevRank(a.Severity).CompareTo(SevRank(b.Severity)); break;
                case 2: p = string.Compare(a.Area, b.Area, StringComparison.OrdinalIgnoreCase); break;
                case 3: p = string.Compare(a.File, b.File, StringComparison.OrdinalIgnoreCase); break;
                case 4: p = a.Line.CompareTo(b.Line); break;
                case 5: p = a.Count.CompareTo(b.Count); break;
                default: p = string.Compare(a.Message, b.Message, StringComparison.OrdinalIgnoreCase); break;
            }
            if (!sortAsc) p = -p;
            if (p != 0) return p;

            int t = SevRank(a.Severity).CompareTo(SevRank(b.Severity));
            if (t != 0) return t;
            t = b.Count.CompareTo(a.Count);
            if (t != 0) return t;
            return a.Seq.CompareTo(b.Seq);
        }

        void RefreshView()
        {
            string f = filterBox.Text.Trim();
            IEnumerable<Finding> q = all;
            if (f.Length > 0) q = q.Where(x => Matches(x, f));
            List<Finding> list = q.ToList();
            list.Sort(Compare);
            view = list;
            findingsView.VirtualListSize = view.Count;
            findingsView.Invalidate();
            UpdateStatus();
        }

        void OnColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column == sortCol) sortAsc = !sortAsc;
            else { sortCol = e.Column; sortAsc = (e.Column != 0 && e.Column != 5); }
            RefreshView();
        }

        void OnRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            Finding f = view[e.ItemIndex];
            ListViewItem it = new ListViewItem(f.Time.ToString("yyyy-MM-dd HH:mm:ss"));
            it.SubItems.Add(f.Severity);
            it.SubItems.Add(f.Area ?? "");
            it.SubItems.Add(f.File ?? "");
            it.SubItems.Add(f.Line >= 0 ? f.Line.ToString() : (f.Severity == "INFO" ? "" : "?"));
            it.SubItems.Add(f.Severity == "INFO" ? "" : f.Count.ToString());
            it.SubItems.Add(f.Message ?? "");
            it.ForeColor = f.Severity == "ERROR" ? Color.Firebrick : (f.Severity == "WARNING" ? Color.DarkOrange : Color.DimGray);
            e.Item = it;
        }

        Finding SelectedFinding()
        {
            if (findingsView.SelectedIndices.Count == 0) return null;
            int i = findingsView.SelectedIndices[0];
            return i >= 0 && i < view.Count ? view[i] : null;
        }

        void OpenSelectedFolder()
        {
            Finding f = SelectedFinding();
            if (f == null || string.IsNullOrEmpty(f.File)) return;
            try
            {
                if (f.File.StartsWith("Event Log:", StringComparison.Ordinal)) { Process.Start("eventvwr.msc"); return; }
                if (File.Exists(f.File)) Process.Start("explorer.exe", "/select,\"" + f.File + "\"");
                else if (Directory.Exists(Path.GetDirectoryName(f.File))) Process.Start("explorer.exe", "\"" + Path.GetDirectoryName(f.File) + "\"");
            }
            catch (Exception) { }
        }

        void CopySelectedMessage()
        {
            Finding f = SelectedFinding();
            if (f == null || string.IsNullOrEmpty(f.Message)) return;
            try { Clipboard.SetText(f.Message); } catch (Exception) { }
        }

        void RefreshFolders()
        {
            Settings s = settings;
            if (s == null) return;
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                MonitorEngine eng = new MonitorEngine(s);
                List<string[]> rows = new List<string[]>();
                foreach (KeyValuePair<string, List<string>> a in s.Areas)
                {
                    if (a.Value.Count == 0) { rows.Add(new[] { a.Key, "(no folders listed)", "-", "" }); continue; }
                    foreach (string folder in a.Value)
                    {
                        EventSource es;
                        if (EventSource.TryParse(folder, out es))
                        {
                            long events = MonitorEngine.EventLogRecordCount(es.Log);
                            rows.Add(new[] { a.Key, es.Display(), events < 0 ? "Missing" : "OK", events < 0 ? "" : events.ToString() });
                            continue;
                        }
                        if (!Directory.Exists(folder)) rows.Add(new[] { a.Key, folder, "Missing", "" });
                        else rows.Add(new[] { a.Key, folder, "OK", eng.EnumFiles(folder).Count().ToString() });
                    }
                }
                Post(delegate
                {
                    foldersView.BeginUpdate();
                    foldersView.Items.Clear();
                    foreach (string[] r in rows)
                    {
                        ListViewItem it = new ListViewItem(r);
                        if (r[2] != "OK") it.ForeColor = Color.Gray;
                        foldersView.Items.Add(it);
                    }
                    foldersView.EndUpdate();
                });
            });
        }

        // ---------- CSV export ----------

        void ExportDialog()
        {
            if (view.Count == 0)
            {
                MessageBox.Show(this, "There are no rows to export. Run a scan first.", "Estes Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "CSV files (*.csv)|*.csv";
                dlg.Title = "Export findings to CSV";
                dlg.FileName = "EstesMonitor_" + Environment.MachineName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    WriteCsv(dlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Could not write the file (is it open in Excel?):\r\n\r\n" + ex.Message, "Estes Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (MessageBox.Show(this, "Saved " + view.Count + " rows to:\r\n" + dlg.FileName + "\r\n\r\nOpen it now?", "Estes Monitor",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    try { Process.Start(dlg.FileName); } catch (Exception) { }
                }
            }
        }

        // Writes the rows currently shown (filter and sort applied), UTF-8 with BOM so Excel reads it correctly.
        void WriteCsv(string path)
        {
            using (StreamWriter w = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                w.WriteLine("Time,Severity,Area,File,Line,Count,Message");
                foreach (Finding f in view)
                {
                    w.WriteLine(string.Join(",", new string[]
                    {
                        Csv(f.Time.ToString("yyyy-MM-dd HH:mm:ss")),
                        Csv(f.Severity),
                        Csv(f.Area),
                        Csv(f.File),
                        f.Line >= 0 ? f.Line.ToString() : "",
                        f.Severity == "INFO" ? "" : f.Count.ToString(),
                        Csv(f.Message)
                    }));
                }
            }
        }

        // Quotes as needed and neutralizes leading = + - @ so Excel does not run log text as a formula.
        static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if ("=+-@\t\r".IndexOf(s[0]) >= 0) s = "'" + s;
            if (s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0) s = "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }

    internal static class GuiProgram
    {
        public static int ExitCode;

        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static int Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch (Exception) { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string configPath = null;
            string exportPath = null;
            bool autoScan = false;
            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i].ToLowerInvariant())
                    {
                        case "--config":
                            if (++i >= args.Length) throw new ArgumentException("Missing value after --config");
                            configPath = args[i];
                            break;
                        case "--export":
                            if (++i >= args.Length) throw new ArgumentException("Missing value after --export");
                            exportPath = args[i];
                            break;
                        case "--autoscan": autoScan = true; break;
                        default: throw new ArgumentException("Unknown option: " + args[i]);
                    }
                }
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show(ex.Message + "\r\n\r\nOptions: --config <file>, --autoscan, --export <file.csv>", "Estes Monitor",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 2;
            }

            Application.Run(new MainForm(configPath, autoScan, exportPath));
            return ExitCode;
        }
    }
}
