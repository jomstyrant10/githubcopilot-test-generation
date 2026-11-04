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
        readonly bool backupAfterScan;
        readonly ErrorCollector collector = new ErrorCollector();
        readonly DailyDeduper deduper = new DailyDeduper(null);
        readonly List<Finding> rawAll = new List<Finding>();

        Settings settings;
        string configSource;

        CheckedListBox areaList;
        ComboBox historyBox;
        NumericUpDown intervalBox;
        CheckBox warnBox;
        Button scanBtn, startBtn, stopBtn, clearBtn, exportBtn, configBtn;
        Button backupBtn, openBackupBtn, clearBackupsBtn;
        CheckBox autoBackupBox, dedupeBox;
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
        long hiddenDuplicates;
        int filesWatched;
        int eventSources;
        DateTime lastScan = DateTime.MinValue;
        bool running;
        ManualResetEvent stopEvent;

        public MainForm(string configPathArg, bool autoScan, string exportPath, bool backupAfterScan)
        {
            this.configPathArg = configPathArg;
            this.autoScan = autoScan;
            this.exportPath = exportPath;
            this.backupAfterScan = backupAfterScan;

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
            top.Height = 196;
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
            clearBtn = MakeButton("Clear list", 358, 52, 80);
            exportBtn = MakeButton("Export to CSV...", 446, 52, 130);
            configBtn = MakeButton("Load config...", 584, 52, 120);
            scanBtn.Click += delegate { StartRun(true); };
            startBtn.Click += delegate { StartRun(false); };
            stopBtn.Click += delegate { if (stopEvent != null) stopEvent.Set(); stateLabel.Text = "Stopping..."; };
            clearBtn.Click += delegate { ClearFindings(); };
            exportBtn.Click += delegate { ExportDialog(); };
            configBtn.Click += delegate { LoadConfigDialog(); };

            autoBackupBox = new CheckBox();
            autoBackupBox.Text = "Auto-backup error files";
            autoBackupBox.AutoSize = true;
            autoBackupBox.Location = new Point(14, 102);
            backupBtn = MakeButton("Backup error files to ZIP", 190, 94, 190);
            openBackupBtn = MakeButton("Open backup folder", 388, 94, 150);
            clearBackupsBtn = MakeButton("Clear backups...", 546, 94, 130);
            backupBtn.Click += delegate { BackupNow(); };
            openBackupBtn.Click += delegate { OpenBackupFolder(); };
            clearBackupsBtn.Click += delegate { ClearBackups(); };
            ToolTip tips = new ToolTip();
            tips.SetToolTip(autoBackupBox, "While scanning or monitoring, zip copies of the files that have errors (at most once per backupIntervalMinutes, and when you press Stop).");
            tips.SetToolTip(backupBtn, "Zip copies of the files that have errors (the filter applies), including files whose repeated messages are hidden by the one-per-day rule. Source files are only read, never changed or deleted.");
            tips.SetToolTip(clearBackupsBtn, "Deletes this tool's backup zips (EstesErrors_*.zip) after you confirm. Nothing else is deleted.");
            tips.SetToolTip(clearBtn, "Empties the findings list only. Files and backups are not touched. Messages hidden as duplicates can be reported again after this.");

            dedupeBox = new CheckBox();
            dedupeBox.Text = "One entry per message per day";
            dedupeBox.AutoSize = true;
            dedupeBox.Location = new Point(14, 138);
            tips.SetToolTip(dedupeBox, "A message already shown today is not listed again, and the same message in several files is one entry. Backups still include every file with errors.");

            configLabel = new Label();
            configLabel.AutoEllipsis = true;
            configLabel.Location = new Point(14, 166);
            configLabel.Size = new Size(690, 20);
            configLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            configLabel.ForeColor = SystemColors.GrayText;

            opts.Controls.Add(l1);
            opts.Controls.Add(historyBox);
            opts.Controls.Add(warnBox);
            opts.Controls.Add(l2);
            opts.Controls.Add(intervalBox);
            opts.Controls.Add(l3);
            opts.Controls.Add(dedupeBox);
            opts.Controls.Add(scanBtn);
            opts.Controls.Add(startBtn);
            opts.Controls.Add(stopBtn);
            opts.Controls.Add(clearBtn);
            opts.Controls.Add(exportBtn);
            opts.Controls.Add(configBtn);
            opts.Controls.Add(autoBackupBox);
            opts.Controls.Add(backupBtn);
            opts.Controls.Add(openBackupBtn);
            opts.Controls.Add(clearBackupsBtn);
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
            configLabel.Text = "Config: " + source + "    |    Backups: " + BackupTool.ResolveDir(settings.BackupDir);

            areaList.Items.Clear();
            foreach (KeyValuePair<string, List<string>> a in settings.Areas)
                areaList.Items.Add(a.Key + "  (" + a.Value.Count + (a.Value.Count == 1 ? " source)" : " sources)"), a.Value.Count > 0);

            int idx = Array.IndexOf(HistoryHours, settings.InitialScanHours);
            historyBox.SelectedIndex = idx >= 0 ? idx : 3;
            intervalBox.Value = Math.Max(intervalBox.Minimum, Math.Min(intervalBox.Maximum, settings.PollSeconds));
            warnBox.Checked = settings.IncludeWarnings;
            autoBackupBox.Checked = settings.AutoBackup;
            dedupeBox.Checked = settings.DedupePerDay;

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
            dedupeBox.Enabled = !value;
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
            bool dedupe = dedupeBox.Checked;
            ManualResetEvent stop = new ManualResetEvent(false);
            stopEvent = stop;

            SetRunning(true);
            stateLabel.Text = once ? "Scanning..." : "Monitoring (every " + poll + " s)";

            Thread t = new Thread(delegate() { Worker(areas, hours, once, poll, stop, dedupe); });
            t.IsBackground = true;
            t.Start();
        }

        void Worker(List<KeyValuePair<string, List<string>>> areas, int hours, bool once, int poll, ManualResetEvent stop, bool dedupe)
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

                    // Backups cover every file with errors, so keep the findings before duplicates are dropped.
                    List<Finding> raw = groups.Select(g => Finding.FromGroup(g, now)).ToList();

                    int hidden = 0;
                    List<AlertGroup> shown = dedupe ? deduper.Filter(groups, now, out hidden) : groups;
                    long errs = shown.Where(g => g.Severity == "ERROR").Sum(g => (long)g.Count);
                    PostFindings(ToFindings(shown, now), raw, files, sources, errs, now);
                    int hiddenNow = hidden;
                    if (hiddenNow > 0) Post(delegate { hiddenDuplicates += hiddenNow; UpdateStatus(); });

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
            PostFindings(items, items, files, sources, errs, when);
        }

        // backupItems = what the backup uses (every file with errors, even when duplicates are hidden from the list).
        void PostFindings(List<Finding> items, List<Finding> backupItems, int files, int sources, long errs, DateTime when)
        {
            Post(delegate { AddResults(items, backupItems, files, sources, errs, when); });
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
            AddResults(items, items, files, sources, errs, when);
        }

        void AddResults(List<Finding> items, List<Finding> backupItems, int files, int sources, long errs, DateTime when)
        {
            foreach (Finding f in items) f.Seq = ++seq;
            all.AddRange(items);
            if (all.Count > MaxRows) all.RemoveRange(0, all.Count - MaxRows);

            rawAll.AddRange(backupItems.Where(x => x.Severity != "INFO"));
            if (rawAll.Count > MaxRows) rawAll.RemoveRange(0, rawAll.Count - MaxRows);

            errorLines += errs;
            if (files >= 0)
            {
                filesWatched = files;
                eventSources = sources;
                lastScan = when;
            }
            RefreshView();
            UpdateStatus();

            if (autoBackupBox.Checked)
            {
                collector.Add(backupItems);
                List<Finding> due = collector.TakeIfDue(settings.BackupIntervalMinutes);
                if (due != null) StartBackup(due, true);
            }
        }

        void Finished()
        {
            SetRunning(false);
            stateLabel.Text = "Idle";
            UpdateStatus();
            RefreshFolders();

            if (backupAfterScan)
            {
                try { BackupTool.CreateZip(BackupTool.ResolveDir(settings.BackupDir), rawAll, settings.MaxBackupFileMB, settings.KeepBackups); }
                catch (Exception) { GuiProgram.ExitCode = 2; }
            }
            else if (autoBackupBox.Checked)
            {
                List<Finding> rest = collector.TakeAll();
                if (rest.Count > 0) StartBackup(rest, true);
            }

            if (exportPath != null)
            {
                int rc = 0;
                try { WriteCsv(exportPath); if (errorLines > 0) rc = 1; }
                catch (Exception) { rc = 2; }
                GuiProgram.ExitCode = rc;
                Close();
            }
        }

        // ---------- backup ----------

        void BackupNow()
        {
            string filterText = filterBox.Text.Trim();
            List<Finding> items = rawAll.Where(f => filterText.Length == 0 || Matches(f, filterText)).ToList();
            if (items.Count == 0)
            {
                MessageBox.Show(this, "There are no findings to back up. Run a scan first (the filter applies).", "Estes Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            StartBackup(items, false);
        }

        void StartBackup(List<Finding> items, bool auto)
        {
            string dir = BackupTool.ResolveDir(settings.BackupDir);
            int keep = settings.KeepBackups;
            int maxMb = settings.MaxBackupFileMB;
            AddInfo("Creating backup of files with errors...");
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                BackupResult r = null;
                string error = null;
                try { r = BackupTool.CreateZip(dir, items, maxMb, keep); }
                catch (Exception ex) { error = ex.Message; }
                Post(delegate { BackupDone(r, error, auto); });
            });
        }

        void BackupDone(BackupResult r, string error, bool auto)
        {
            if (error != null)
            {
                AddInfo("Backup failed: " + error);
                if (!auto) MessageBox.Show(this, "The backup failed:\r\n\r\n" + error, "Estes Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (r == null) return;

            AddInfo("Backup created: " + r.ZipPath + " (" + r.FilesAdded + " files"
                + (r.FilesSkipped > 0 ? ", " + r.FilesSkipped + " skipped" : "")
                + (r.Pruned > 0 ? ", " + r.Pruned + " old backup(s) removed" : "") + ")");
            foreach (string n in r.Notes) AddInfo(n);

            if (!auto && MessageBox.Show(this, "Saved " + r.FilesAdded + " file(s) (" + (r.BytesCopied / 1048576.0).ToString("N1") + " MB before compression) to:\r\n" + r.ZipPath
                + "\r\n\nOpen the folder?", "Estes Monitor", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                try { Process.Start("explorer.exe", "/select,\"" + r.ZipPath + "\""); } catch (Exception) { }
            }
        }

        void AddInfo(string message)
        {
            AddResults(new List<Finding> { Info(message, DateTime.Now) }, -1, -1, 0, DateTime.Now);
        }

        void OpenBackupFolder()
        {
            string dir = BackupTool.ResolveDir(settings.BackupDir);
            try
            {
                Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open " + dir + ":\r\n" + ex.Message, "Estes Monitor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Deletes only EstesErrors_*.zip files in the backup folder.
        void ClearBackups()
        {
            string dir = BackupTool.ResolveDir(settings.BackupDir);
            List<FileInfo> files = BackupTool.ListBackups(dir);
            if (files.Count == 0)
            {
                MessageBox.Show(this, "There are no backups in:\r\n" + dir, "Estes Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            double mb = files.Sum(f => f.Length) / 1048576.0;
            if (MessageBox.Show(this, "Delete " + files.Count + " backup zip(s) (" + mb.ToString("N1") + " MB) from:\r\n" + dir
                + "\r\n\nOnly this tool's EstesErrors_*.zip files are removed. The original log files are not touched.",
                "Clear backups", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

            long freed;
            int deleted = BackupTool.DeleteFiles(files, out freed);
            AddInfo("Cleared " + deleted + " backup(s), " + (freed / 1048576.0).ToString("N1") + " MB, from " + dir);
        }

        void ClearFindings()
        {
            all.Clear();
            rawAll.Clear();
            deduper.Reset();
            errorLines = 0;
            hiddenDuplicates = 0;
            RefreshView();
            UpdateStatus();
        }

        void UpdateStatus()
        {
            countsLabel.Text = "Files watched: " + filesWatched + "    Event sources: " + eventSources + "    Error lines/events: " + errorLines + "    Duplicates hidden: " + hiddenDuplicates + "    Rows shown: " + view.Count + " of " + all.Count;
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
                w.WriteLine(CsvUtil.Header);
                foreach (Finding f in view) w.WriteLine(CsvUtil.Row(f));
            }
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
            bool backupAfterScan = false;
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
                        case "--backup": backupAfterScan = true; autoScan = true; break;
                        default: throw new ArgumentException("Unknown option: " + args[i]);
                    }
                }
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show(ex.Message + "\r\n\r\nOptions: --config <file>, --autoscan, --export <file.csv>, --backup (scan once and zip the files with errors)", "Estes Monitor",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 2;
            }

            Application.Run(new MainForm(configPath, autoScan, exportPath, backupAfterScan));
            return ExitCode;
        }
    }
}
