// Estes Monitor engine: config, file tailing, and error matching shared by the console and UI apps.
// Targets .NET Framework 4.x and the built-in C# 5 compiler: no installs needed to build or run.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace EstesMonitor
{
    internal sealed class Settings
    {
        public int PollSeconds = 30;
        public int InitialScanHours = 24;
        public int MaxAlertGroupsPerFile = 20;
        public bool IncludeWarnings;
        public bool DedupePerDay = true;
        public string OutputDir = "logs";
        public string BackupDir = "backups";
        public bool AutoBackup;
        public int BackupIntervalMinutes = 15;
        public int KeepBackups = 30;
        public int MaxBackupFileMB = 200;
        public List<string> FilePatterns = new List<string>();
        public List<string> ErrorWords = new List<string>();
        public List<string> WarningWords = new List<string>();
        public List<string> IgnoreRegex = new List<string>();
        public List<KeyValuePair<string, List<string>>> Areas = new List<KeyValuePair<string, List<string>>>();
    }

    internal sealed class FileState
    {
        public Encoding Enc;
        public long Offset;
        public long LineNo; // -1 when the line number is unknown (file started mid-way)
        public long LastSeenLength = -1;
    }

    internal sealed class AlertGroup
    {
        public string Severity;
        public string Area;
        public string File;
        public string Sample;
        public long FirstLine;
        public int Count;
    }

    // An Event Viewer source: a whole log, or only some providers within a log (e.g. Application).
    internal sealed class EventSource
    {
        public const string Prefix = "eventlog:";
        public string Log;
        public List<string> Providers = new List<string>();

        // Entry format: "eventlog:LogName" or "eventlog:LogName!Provider1,Provider2".
        public static bool TryParse(string item, out EventSource src)
        {
            src = null;
            if (item == null || !item.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return false;
            string rest = item.Substring(Prefix.Length).Trim();
            int bang = rest.IndexOf('!');
            EventSource s = new EventSource();
            s.Log = (bang < 0 ? rest : rest.Substring(0, bang)).Trim();
            if (bang >= 0)
            {
                foreach (string p in rest.Substring(bang + 1).Split(','))
                    if (p.Trim().Length > 0) s.Providers.Add(p.Trim());
            }
            if (s.Log.Length == 0) return false;
            src = s;
            return true;
        }

        public string Display()
        {
            return "Event log: " + Log + (Providers.Count > 0 ? " [" + string.Join(", ", Providers.ToArray()) + "]" : "");
        }
    }

    internal sealed class EventState
    {
        public DateTime LastQueryUtc;
        public readonly Dictionary<long, DateTime> Seen = new Dictionary<long, DateTime>();
    }

    internal static class Config
    {
        public const string DefaultJson = @"{
  ""pollSeconds"": 30,
  ""initialScanHours"": 24,
  ""includeWarnings"": false,
  ""dedupePerDay"": true,
  ""maxAlertGroupsPerFile"": 20,
  ""outputDir"": ""logs"",
  ""backupDir"": ""backups"",
  ""autoBackup"": false,
  ""backupIntervalMinutes"": 15,
  ""keepBackups"": 30,
  ""maxBackupFileMB"": 200,
  ""filePatterns"": [""*.log"", ""*.txt"", ""*.out"", ""*.err""],
  ""errorWords"": [""ERROR"", ""CRITICAL"", ""FATAL"", ""EXCEPTION"", ""FAILED"", ""CRASH"", ""TIMEOUT""],
  ""warningWords"": [""WARNING"", ""WARN"", ""CAUTION"", ""DEPRECATED""],
  ""ignoreRegex"": [
    ""TG='\\$error'"",
    ""NAME='Failed OPEN'"",
    ""DSC='Communication Error'"",
    ""Failed processing 0 files"",
    ""Windows Error Reporting""
  ],
  ""areas"": {
    ""Control Modules"": [],
    ""Batch Operations"": [
      ""D:\\DeltaV\\DVData\\Batch\\Logs"",
      ""eventlog:Application!DVBManager,DVBRuntime,DVBHelper,DVBSim,DVBSPS""
    ],
    ""Alarms & Events"": [
      ""D:\\DeltaV\\DVData\\AlarmMosaicLogs"",
      ""D:\\DeltaV\\DVData\\AlarmReports"",
      ""eventlog:DeltaVChronicle""
    ],
    ""Historical Data"": [
      ""eventlog:Application!DvHistorianServerHost_2""
    ],
    ""Configuration"": [
      ""D:\\DeltaV\\DVData\\Import-Export"",
      ""eventlog:Application!Exploring DeltaV""
    ],
    ""Diagnostics & System"": [
      ""C:\\DeltaV\\InstallLogs"",
      ""D:\\DeltaV\\DVData\\Diagnostics"",
      ""D:\\DeltaV\\DVData\\YellowPages\\Diagnostics"",
      ""D:\\DeltaV\\DVData\\SysRegLogFiles"",
      ""D:\\DeltaV\\DVData\\Runtime\\Logs"",
      ""D:\\DeltaV\\DVData\\IFSlogs"",
      ""D:\\DeltaV\\ProgramData\\Emerson\\Logs"",
      ""eventlog:DVTaskService"",
      ""eventlog:DeltaV_Trace"",
      ""eventlog:DeltaV Virtual Studio"",
      ""eventlog:Application!DVSYS,DVTaskService-Init,RamService""
    ],
    ""Communication & Network"": [
      ""D:\\DeltaV\\ProgramData\\Emerson\\CIF\\Logging"",
      ""D:\\DeltaV\\DVData\\DeltaV Web Services\\Logs"",
      ""C:\\DeltaV\\OPCLogs"",
      ""eventlog:Application!DVBOpcSvr,DvOpcUaServer,DeltaV OPC UA Server,DeltaV_OPC.NET,DeltaV_OPC.NETDiscovery,EmersonProcess.Web.Services""
    ],
    ""Operator Interface"": [
      ""eventlog:DeltaV Live""
    ],
    ""Security & Audit"": [
      ""D:\\DeltaV\\DVData\\OPCUA\\Server\\Audit"",
      ""eventlog:DeltaV Security"",
      ""eventlog:DVSecBroker"",
      ""eventlog:Application!DVSecBroker-Init""
    ],
    ""Database & Archive"": [
      ""eventlog:Application!DvDbServer,MSSQL$DELTAV_INSTANCE,MSSQL$DELTAV_ADV_CNTRL,MSSQL$DELTAV_CHRONICLE""
    ],
    ""Application Station"": [],
    ""Device Integration (CHARMS)"": [
      ""D:\\DeltaV\\DVData\\AddHartDeviceUtilLogs"",
      ""eventlog:Application!AmsCommService,AmsFdiService,AmsHierService,AmsServerStarter,AmsUCService""
    ]
  }
}
";

        // Loads the explicit file, else estes-monitor.config.json next to the exe, else the built-in defaults.
        public static Settings Load(string explicitPath, out string source)
        {
            string defaultFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "estes-monitor.config.json");
            string json;
            if (explicitPath != null)
            {
                if (!File.Exists(explicitPath)) throw new ArgumentException("Config file not found: " + explicitPath);
                json = File.ReadAllText(explicitPath);
                source = explicitPath;
            }
            else if (File.Exists(defaultFile))
            {
                json = File.ReadAllText(defaultFile);
                source = defaultFile;
            }
            else
            {
                json = DefaultJson;
                source = "built-in defaults";
            }
            return Parse(json);
        }

        public static Settings Parse(string json)
        {
            JavaScriptSerializer ser = new JavaScriptSerializer();
            ser.MaxJsonLength = int.MaxValue;
            Dictionary<string, object> root = ser.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null) throw new InvalidDataException("Config root must be a JSON object.");

            Settings s = new Settings();
            s.PollSeconds = GetInt(root, "pollSeconds", s.PollSeconds);
            s.InitialScanHours = GetInt(root, "initialScanHours", s.InitialScanHours);
            s.MaxAlertGroupsPerFile = GetInt(root, "maxAlertGroupsPerFile", s.MaxAlertGroupsPerFile);
            s.IncludeWarnings = GetBool(root, "includeWarnings", false);
            s.OutputDir = GetString(root, "outputDir", s.OutputDir);
            s.BackupDir = GetString(root, "backupDir", s.BackupDir);
            s.AutoBackup = GetBool(root, "autoBackup", false);
            s.DedupePerDay = GetBool(root, "dedupePerDay", true);
            s.BackupIntervalMinutes = Math.Max(1, GetInt(root, "backupIntervalMinutes", s.BackupIntervalMinutes));
            s.KeepBackups = Math.Max(0, GetInt(root, "keepBackups", s.KeepBackups));
            s.MaxBackupFileMB = Math.Max(0, GetInt(root, "maxBackupFileMB", s.MaxBackupFileMB));
            s.FilePatterns = GetList(root, "filePatterns");
            s.ErrorWords = GetList(root, "errorWords");
            s.WarningWords = GetList(root, "warningWords");
            s.IgnoreRegex = GetList(root, "ignoreRegex");

            object areasObj;
            if (root.TryGetValue("areas", out areasObj))
            {
                Dictionary<string, object> areas = areasObj as Dictionary<string, object>;
                if (areas != null)
                {
                    foreach (KeyValuePair<string, object> kv in areas)
                        s.Areas.Add(new KeyValuePair<string, List<string>>(kv.Key, ToList(kv.Value)));
                }
            }

            if (s.PollSeconds < 1) s.PollSeconds = 1;
            if (s.FilePatterns.Count == 0) throw new InvalidDataException("filePatterns must not be empty.");
            if (s.ErrorWords.Count == 0) throw new InvalidDataException("errorWords must not be empty.");
            return s;
        }

        static List<string> ToList(object o)
        {
            List<string> r = new List<string>();
            IEnumerable e = o as IEnumerable;
            if (e != null && !(o is string))
            {
                foreach (object x in e) if (x != null) r.Add(x.ToString());
            }
            return r;
        }

        static List<string> GetList(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) ? ToList(v) : new List<string>();
        }

        static int GetInt(Dictionary<string, object> d, string key, int def)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? Convert.ToInt32(v) : def;
        }

        static bool GetBool(Dictionary<string, object> d, string key, bool def)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? Convert.ToBoolean(v) : def;
        }

        static string GetString(Dictionary<string, object> d, string key, string def)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? v.ToString() : def;
        }
    }

    // One row of results: a message found in a file or an Event Viewer entry.
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

        public static Finding FromGroup(AlertGroup g, DateTime when)
        {
            Finding f = new Finding();
            f.Time = when;
            f.Severity = g.Severity;
            f.Area = g.Area;
            f.File = g.File;
            f.Line = g.FirstLine;
            f.Count = g.Count;
            f.Message = g.Sample;
            return f;
        }

        public bool IsEventLog
        {
            get { return File != null && File.StartsWith("Event Log:", StringComparison.Ordinal); }
        }
    }

    internal static class CsvUtil
    {
        public const string Header = "Time,Severity,Area,File,Line,Count,Message";

        public static string Row(Finding f)
        {
            return string.Join(",", new string[]
            {
                Quote(f.Time.ToString("yyyy-MM-dd HH:mm:ss")),
                Quote(f.Severity),
                Quote(f.Area),
                Quote(f.File),
                f.Line >= 0 ? f.Line.ToString() : "",
                f.Severity == "INFO" ? "" : f.Count.ToString(),
                Quote(f.Message)
            });
        }

        // Quotes as needed and neutralizes leading = + - @ so Excel does not run log text as a formula.
        public static string Quote(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if ("=+-@\t\r".IndexOf(s[0]) >= 0) s = "'" + s;
            if (s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0) s = "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }

    internal sealed class BackupResult
    {
        public string ZipPath;
        public int FilesAdded;
        public int FilesSkipped;
        public int Findings;
        public long BytesCopied;
        public int Pruned;
        public List<string> Notes = new List<string>();
    }

    // Copies the files that contain findings into a zip. Source files are only read, never changed or deleted.
    internal static class BackupTool
    {
        public const string Prefix = "EstesErrors_";

        public static string ResolveDir(string configured)
        {
            return Path.IsPathRooted(configured) ? configured : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configured);
        }

        public static List<FileInfo> ListBackups(string dir)
        {
            if (!Directory.Exists(dir)) return new List<FileInfo>();
            return new DirectoryInfo(dir).GetFiles(Prefix + "*.zip")
                .Where(f => f.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();
        }

        // Deletes only this tool's own output files (never anything else in the folder).
        public static int DeleteFiles(IEnumerable<FileInfo> files, out long bytes)
        {
            int n = 0;
            bytes = 0;
            foreach (FileInfo f in files)
            {
                try { long len = f.Length; f.Delete(); n++; bytes += len; }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return n;
        }

        public static List<FileInfo> ListAlertLogs(string dir)
        {
            if (!Directory.Exists(dir)) return new List<FileInfo>();
            List<FileInfo> r = new DirectoryInfo(dir).GetFiles("estes-alerts-*.log")
                .Where(f => f.Extension.Equals(".log", StringComparison.OrdinalIgnoreCase))
                .ToList();
            // The daily-duplicate memory goes with the alert logs, so cleared entries can be reported again.
            string state = Path.Combine(dir, DailyDeduper.StateFileName);
            if (File.Exists(state)) r.Add(new FileInfo(state));
            return r;
        }

        // Keeps the newest 'keep' backups and deletes the rest; keep <= 0 keeps everything.
        public static int Prune(string dir, int keep)
        {
            if (keep <= 0) return 0;
            long bytes;
            return DeleteFiles(ListBackups(dir).Skip(keep).ToList(), out bytes);
        }

        static string EntryPath(string path)
        {
            string p = path.Replace('\\', '/');
            if (p.StartsWith("//", StringComparison.Ordinal)) return "UNC/" + p.TrimStart('/');
            if (p.Length >= 2 && p[1] == ':') return p.Substring(0, 1) + "/" + p.Substring(2).TrimStart('/');
            return p.TrimStart('/');
        }

        static void AddText(ZipArchive zip, string name, string text, bool bom)
        {
            ZipArchiveEntry e = zip.CreateEntry(name, CompressionLevel.Optimal);
            byte[] pre = bom ? new UTF8Encoding(true).GetPreamble() : new byte[0];
            byte[] body = new UTF8Encoding(false).GetBytes(text);
            using (Stream s = e.Open())
            {
                s.Write(pre, 0, pre.Length);
                s.Write(body, 0, body.Length);
            }
        }

        // Returns null when there is nothing to back up.
        public static BackupResult CreateZip(string dir, IList<Finding> findings, int maxFileMB, int keep)
        {
            List<Finding> items = findings.Where(f => f.Severity != "INFO").ToList();
            if (items.Count == 0) return null;

            BackupResult r = new BackupResult();
            r.Findings = items.Count;

            try
            {
                Directory.CreateDirectory(dir);
                using (File.Create(Path.Combine(dir, ".write-test"), 1, FileOptions.DeleteOnClose)) { }
            }
            catch (Exception)
            {
                dir = Path.Combine(Path.GetTempPath(), "EstesMonitor", "backups");
                Directory.CreateDirectory(dir);
                r.Notes.Add("Backup folder is not writable; used " + dir);
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string baseName = Prefix + Environment.MachineName + "_" + stamp;
            string zipPath = Path.Combine(dir, baseName + ".zip");
            for (int i = 2; File.Exists(zipPath); i++) zipPath = Path.Combine(dir, baseName + "_" + i + ".zip");
            r.ZipPath = zipPath;

            long maxBytes = maxFileMB > 0 ? maxFileMB * 1024L * 1024L : long.MaxValue;
            List<string> files = items.Where(f => !f.IsEventLog && !string.IsNullOrEmpty(f.File))
                .Select(f => f.File).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            using (FileStream zfs = new FileStream(zipPath, FileMode.CreateNew))
            using (ZipArchive zip = new ZipArchive(zfs, ZipArchiveMode.Create))
            {
                foreach (string file in files)
                {
                    try
                    {
                        FileInfo fi = new FileInfo(file);
                        if (!fi.Exists) { r.FilesSkipped++; r.Notes.Add("Missing: " + file); continue; }
                        if (fi.Length > maxBytes) { r.FilesSkipped++; r.Notes.Add("Skipped (over " + maxFileMB + " MB): " + file); continue; }

                        using (FileStream src = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        {
                            ZipArchiveEntry entry = zip.CreateEntry("files/" + EntryPath(file), CompressionLevel.Optimal);
                            DateTime t = fi.LastWriteTime;
                            if (t.Year >= 1980 && t.Year < 2107) entry.LastWriteTime = new DateTimeOffset(t);
                            using (Stream dst = entry.Open()) { src.CopyTo(dst); }
                        }
                        r.FilesAdded++;
                        r.BytesCopied += fi.Length;
                    }
                    catch (IOException ex) { r.FilesSkipped++; r.Notes.Add("Could not read " + file + ": " + ex.Message); }
                    catch (UnauthorizedAccessException ex) { r.FilesSkipped++; r.Notes.Add("No access to " + file + ": " + ex.Message); }
                }

                StringBuilder csv = new StringBuilder(CsvUtil.Header).AppendLine();
                foreach (Finding f in items) csv.AppendLine(CsvUtil.Row(f));
                AddText(zip, "findings.csv", csv.ToString(), true);

                StringBuilder sum = new StringBuilder();
                sum.AppendLine("Estes Monitor error backup");
                sum.AppendLine("Machine : " + Environment.MachineName);
                sum.AppendLine("Created : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sum.AppendLine("Findings: " + items.Count + " (see findings.csv)");
                sum.AppendLine("Files copied: " + r.FilesAdded + ", skipped: " + r.FilesSkipped);
                sum.AppendLine("Event Viewer findings are listed in findings.csv only (no files to copy).");
                sum.AppendLine("Copies are under files/<drive>/<original path>.");
                foreach (string n in r.Notes) sum.AppendLine("- " + n);
                AddText(zip, "summary.txt", sum.ToString(), false);
            }

            r.Pruned = Prune(Path.GetDirectoryName(zipPath), keep);
            return r;
        }
    }

    // Keeps one entry per unique message per day. The same message in several files is one entry
    // (counts are added together); a message already reported today is dropped. The memory can be
    // kept in a small file so repeated runs on the same day stay quiet; it resets at midnight.
    internal sealed class DailyDeduper
    {
        public const string StateFileName = "estes-dedupe-state.txt";

        readonly HashSet<ulong> seen = new HashSet<ulong>();
        readonly object gate = new object();
        readonly string stateFile; // null = memory only
        string day;
        bool fileIsToday;

        public DailyDeduper(string stateFile)
        {
            this.stateFile = stateFile;
            day = Today(DateTime.Now);
            Load();
        }

        static string Today(DateTime t)
        {
            return t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public void Reset()
        {
            lock (gate)
            {
                seen.Clear();
                day = Today(DateTime.Now);
                fileIsToday = false;
            }
        }

        // Returns the groups that are new today; 'hidden' counts the ones left out.
        public List<AlertGroup> Filter(List<AlertGroup> groups, DateTime now, out int hidden)
        {
            lock (gate)
            {
                return FilterLocked(groups, now, out hidden);
            }
        }

        List<AlertGroup> FilterLocked(List<AlertGroup> groups, DateTime now, out int hidden)
        {
            hidden = 0;
            string today = Today(now);
            if (today != day)
            {
                seen.Clear();
                day = today;
                fileIsToday = false;
            }

            List<AlertGroup> kept = new List<AlertGroup>();
            Dictionary<ulong, AlertGroup> keptByHash = new Dictionary<ulong, AlertGroup>();
            foreach (AlertGroup g in groups
                .OrderBy(x => x.Severity == "ERROR" ? 0 : 1)
                .ThenBy(x => x.Area)
                .ThenBy(x => x.File)
                .ThenByDescending(x => x.Count))
            {
                ulong h = Hash(g);
                AlertGroup first;
                if (keptByHash.TryGetValue(h, out first)) { first.Count += g.Count; hidden++; continue; }
                if (!seen.Add(h)) { hidden++; continue; }
                keptByHash[h] = g;
                kept.Add(g);
                Append(h);
            }
            return kept;
        }

        // Event Viewer messages stay separate per log; file messages match across files.
        static ulong Hash(AlertGroup g)
        {
            string scope = g.File != null && g.File.StartsWith("Event Log:", StringComparison.Ordinal) ? g.File : "";
            string key = g.Severity + "|" + scope + "|" + MonitorEngine.Normalize(g.Sample);
            unchecked
            {
                ulong h = 14695981039346656037UL; // FNV-1a
                foreach (char c in key) { h ^= c; h *= 1099511628211UL; }
                return h;
            }
        }

        void Load()
        {
            if (stateFile == null || !File.Exists(stateFile)) return;
            try
            {
                string[] lines = File.ReadAllLines(stateFile);
                if (lines.Length == 0 || lines[0].Trim() != "day=" + day) return;
                for (int i = 1; i < lines.Length; i++)
                {
                    ulong h;
                    if (ulong.TryParse(lines[i].Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out h)) seen.Add(h);
                }
                fileIsToday = true;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        void Append(ulong h)
        {
            if (stateFile == null) return;
            try
            {
                string line = h.ToString("x16", CultureInfo.InvariantCulture) + Environment.NewLine;
                if (!fileIsToday)
                {
                    File.WriteAllText(stateFile, "day=" + day + Environment.NewLine + line, new UTF8Encoding(false));
                    fileIsToday = true;
                }
                else File.AppendAllText(stateFile, line, new UTF8Encoding(false));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    // Collects findings between backups so one zip covers everything since the last one.
    internal sealed class ErrorCollector
    {
        const int MaxPending = 200000;
        readonly List<Finding> pending = new List<Finding>();
        DateTime lastBackup = DateTime.MinValue;

        public int PendingCount { get { return pending.Count; } }

        public void Add(IEnumerable<Finding> items)
        {
            foreach (Finding f in items)
            {
                if (f.Severity == "INFO") continue;
                if (pending.Count >= MaxPending) break;
                pending.Add(f);
            }
        }

        public List<Finding> TakeIfDue(int intervalMinutes)
        {
            if (pending.Count == 0) return null;
            if ((DateTime.Now - lastBackup).TotalMinutes < Math.Max(1, intervalMinutes)) return null;
            return TakeAll();
        }

        public List<Finding> TakeAll()
        {
            List<Finding> r = new List<Finding>(pending);
            pending.Clear();
            lastBackup = DateTime.Now;
            return r;
        }
    }

    internal sealed class MonitorEngine
    {
        const int MaxReadBytes = 8 * 1024 * 1024;

        readonly Settings S;
        readonly Regex ErrRx;
        readonly Regex WarnRx;
        readonly List<Regex> IgnoreRx = new List<Regex>();
        readonly List<Regex> FileRx = new List<Regex>();
        readonly Dictionary<string, FileState> States = new Dictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> MissingReported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, EventState> EventStates = new Dictionary<string, EventState>(StringComparer.OrdinalIgnoreCase);
        static readonly Regex DigitsRx = new Regex(@"\d+", RegexOptions.Compiled);

        // Numbers become # so "disk failed 5" and "disk failed 7" count as the same message.
        public static string Normalize(string text)
        {
            return DigitsRx.Replace(text ?? "", "#");
        }

        // Receives informational messages such as "folder not found".
        public Action<string> Log;

        // Event sources queried in the last RunCycle.
        public int EventSourcesQueried;

        public MonitorEngine(Settings settings)
        {
            S = settings;
            ErrRx = WordRegex(S.ErrorWords);
            WarnRx = WordRegex(S.WarningWords);
            foreach (string r in S.IgnoreRegex)
                IgnoreRx.Add(new Regex(r, RegexOptions.IgnoreCase | RegexOptions.Compiled));
            foreach (string p in S.FilePatterns)
                FileRx.Add(new Regex("^" + Regex.Escape(p).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase | RegexOptions.Compiled));
        }

        static Regex WordRegex(List<string> words)
        {
            if (words.Count == 0) return null;
            string alt = string.Join("|", words.Select(w => Regex.Escape(w)).ToArray());
            return new Regex(@"\b(" + alt + @")\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        void Info(string message)
        {
            Action<string> log = Log;
            if (log != null) log(message);
        }

        public IEnumerable<string> EnumFiles(string folder)
        {
            Stack<string> stack = new Stack<string>();
            stack.Push(folder);
            while (stack.Count > 0)
            {
                string dir = stack.Pop();
                string[] files = null;
                string[] subs = null;
                try { files = Directory.GetFiles(dir); } catch (Exception) { }
                try { subs = Directory.GetDirectories(dir); } catch (Exception) { }
                if (files != null)
                {
                    foreach (string f in files)
                    {
                        string name = Path.GetFileName(f);
                        if (FileRx.Any(rx => rx.IsMatch(name))) yield return f;
                    }
                }
                if (subs != null)
                {
                    foreach (string d in subs) stack.Push(d);
                }
            }
        }

        static FileStream OpenShared(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }

        static Encoding DetectEncoding(string path, out int preamble)
        {
            preamble = 0;
            byte[] b = new byte[4];
            int n = 0;
            try
            {
                using (FileStream fs = OpenShared(path)) { n = fs.Read(b, 0, 4); }
            }
            catch (IOException) { return new UTF8Encoding(false); }
            if (n >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) { preamble = 3; return new UTF8Encoding(false); }
            if (n >= 2 && b[0] == 0xFF && b[1] == 0xFE) { preamble = 2; return Encoding.Unicode; }
            if (n >= 2 && b[0] == 0xFE && b[1] == 0xFF) { preamble = 2; return Encoding.BigEndianUnicode; }
            return new UTF8Encoding(false);
        }

        static bool IsUtf16(Encoding enc)
        {
            return enc.CodePage == 1200 || enc.CodePage == 1201;
        }

        // Number of bytes up to and including the last newline, or 0 when there is none.
        static int LastLineEnd(byte[] buf, int n, Encoding enc)
        {
            if (enc.CodePage == 1200)
            {
                for (int i = n - 2; i >= 0; i -= 2) if (buf[i] == 0x0A && buf[i + 1] == 0x00) return i + 2;
                return 0;
            }
            if (enc.CodePage == 1201)
            {
                for (int i = n - 2; i >= 0; i -= 2) if (buf[i] == 0x00 && buf[i + 1] == 0x0A) return i + 2;
                return 0;
            }
            for (int i = n - 1; i >= 0; i--) if (buf[i] == 0x0A) return i + 1;
            return 0;
        }

        static bool IsOwnAlertFile(string path)
        {
            string name = Path.GetFileName(path);
            if (name.Equals(DailyDeduper.StateFileName, StringComparison.OrdinalIgnoreCase)) return true;
            return name.StartsWith("estes-alerts-", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".log", StringComparison.OrdinalIgnoreCase);
        }

        // Returns the record count of an Event Viewer log, or -1 when it does not exist or cannot be read.
        public static long EventLogRecordCount(string log)
        {
            try
            {
                EventLogInformation info = EventLogSession.GlobalSession.GetLogInformation(log, PathType.LogName);
                return info.RecordCount.HasValue ? info.RecordCount.Value : 0;
            }
            catch (Exception) { return -1; }
        }

        static string QuoteXPath(string s)
        {
            return s.IndexOf('\'') >= 0 ? "\"" + s + "\"" : "'" + s + "'";
        }

        // Event queries allow only ~20 conditions, so callers pass at most 8 providers per query.
        static string BuildEventQuery(List<string> providers, bool includeWarnings, DateTime sinceUtc)
        {
            StringBuilder sb = new StringBuilder("*[System[");
            if (providers != null && providers.Count > 0)
            {
                sb.Append("(");
                for (int i = 0; i < providers.Count; i++)
                {
                    if (i > 0) sb.Append(" or ");
                    sb.Append("Provider[@Name=").Append(QuoteXPath(providers[i])).Append("]");
                }
                sb.Append(") and ");
            }
            sb.Append(includeWarnings ? "(Level=1 or Level=2 or Level=3)" : "(Level=1 or Level=2)");
            sb.Append(" and TimeCreated[@SystemTime>='")
              .Append(sinceUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture))
              .Append("']]]");
            return sb.ToString();
        }

        // Reads Critical/Error (and optionally Warning) events newer than the last check.
        void QueryEvents(string area, string item, EventSource src, bool initial, DateTime cutoff, Dictionary<string, AlertGroup> groups)
        {
            string stateKey = area + "|" + item;
            DateTime nowUtc = DateTime.UtcNow;
            DateTime sinceUtc;
            EventState st;
            if (!EventStates.TryGetValue(stateKey, out st))
            {
                st = new EventState();
                EventStates[stateKey] = st;
                DateTime floor = new DateTime(1971, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                if (initial) sinceUtc = cutoff <= floor ? floor : cutoff.ToUniversalTime();
                else sinceUtc = nowUtc.AddMinutes(-5);
            }
            else sinceUtc = st.LastQueryUtc;

            List<List<string>> chunks = new List<List<string>>();
            if (src.Providers.Count == 0) chunks.Add(null);
            else for (int i = 0; i < src.Providers.Count; i += 8) chunks.Add(src.Providers.Skip(i).Take(8).ToList());

            bool failed = false;
            foreach (List<string> chunk in chunks)
            {
                try
                {
                    EventLogQuery q = new EventLogQuery(src.Log, PathType.LogName, BuildEventQuery(chunk, S.IncludeWarnings, sinceUtc));
                    using (EventLogReader reader = new EventLogReader(q))
                    {
                        EventRecord rec;
                        while ((rec = reader.ReadEvent()) != null)
                        {
                            using (rec) { AddEvent(area, src, st, rec, groups); }
                        }
                    }
                }
                catch (EventLogException ex) { failed = true; ReportEventProblem(stateKey, src, ex.Message); }
                catch (UnauthorizedAccessException ex) { failed = true; ReportEventProblem(stateKey, src, ex.Message); }
            }
            if (!failed) MissingReported.Remove(stateKey);

            st.LastQueryUtc = nowUtc.AddSeconds(-5);
            DateTime keepFrom = nowUtc.AddMinutes(-15);
            List<long> old = st.Seen.Where(kv => kv.Value < keepFrom).Select(kv => kv.Key).ToList();
            foreach (long id in old) st.Seen.Remove(id);
            EventSourcesQueried++;
        }

        void ReportEventProblem(string stateKey, EventSource src, string detail)
        {
            if (MissingReported.Add(stateKey))
                Info("Event log not available, will keep checking: " + src.Display() + " (" + detail.Trim() + ")");
        }

        void AddEvent(string area, EventSource src, EventState st, EventRecord rec, Dictionary<string, AlertGroup> groups)
        {
            long id = rec.RecordId.HasValue ? rec.RecordId.Value : -1;
            DateTime whenUtc = rec.TimeCreated.HasValue ? rec.TimeCreated.Value.ToUniversalTime() : DateTime.UtcNow;
            if (id >= 0)
            {
                if (st.Seen.ContainsKey(id)) return;
                st.Seen[id] = whenUtc;
            }

            int level = rec.Level.HasValue ? rec.Level.Value : 4;
            string severity = level <= 2 ? "ERROR" : "WARNING";
            if (severity == "WARNING" && !S.IncludeWarnings) return;

            string msg = null;
            try { msg = rec.FormatDescription(); } catch (Exception) { }
            if (string.IsNullOrEmpty(msg))
            {
                try { msg = string.Join(" | ", rec.Properties.Select(p => p.Value == null ? "" : p.Value.ToString()).ToArray()); }
                catch (Exception) { }
            }
            msg = (msg ?? "").Trim();
            int nl = msg.IndexOfAny(new[] { '\r', '\n' });
            if (nl > 0) msg = msg.Substring(0, nl).Trim();

            string sample = "[" + (rec.ProviderName ?? "?") + " id " + rec.Id + "] " + msg;
            if (sample.Length > 300) sample = sample.Substring(0, 300);
            foreach (Regex rx in IgnoreRx) { if (rx.IsMatch(sample)) return; }

            string file = "Event Log: " + src.Log;
            string key = severity + "|" + file + "|" + DigitsRx.Replace(sample, "#");
            AlertGroup g;
            if (groups.TryGetValue(key, out g)) { g.Count++; return; }

            g = new AlertGroup();
            g.Severity = severity;
            g.Area = area;
            g.File = file;
            g.Sample = sample;
            g.FirstLine = id;
            g.Count = 1;
            groups[key] = g;
        }

        // initial = first cycle: files older than cutoff start at their end, newer ones are read from the top.
        public List<AlertGroup> RunCycle(List<KeyValuePair<string, List<string>>> areas, bool initial, DateTime cutoff, out int filesSeen)
        {
            Dictionary<string, AlertGroup> groups = new Dictionary<string, AlertGroup>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            filesSeen = 0;
            EventSourcesQueried = 0;

            foreach (KeyValuePair<string, List<string>> area in areas)
            {
                foreach (string item in area.Value)
                {
                    EventSource es;
                    if (EventSource.TryParse(item, out es))
                    {
                        QueryEvents(area.Key, item, es, initial, cutoff, groups);
                        continue;
                    }

                    string folder = item;
                    if (!Directory.Exists(folder))
                    {
                        if (MissingReported.Add(folder))
                            Info("Folder not found, will keep checking: " + folder + "  [" + area.Key + "]");
                        continue;
                    }
                    MissingReported.Remove(folder);

                    foreach (string file in EnumFiles(folder))
                    {
                        if (!seen.Add(file)) continue;
                        if (IsOwnAlertFile(file)) continue;
                        filesSeen++;
                        try { ProcessFile(area.Key, file, initial, cutoff, groups); }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                }
            }

            List<string> gone = States.Keys.Where(k => !seen.Contains(k)).ToList();
            foreach (string k in gone) States.Remove(k);

            return new List<AlertGroup>(groups.Values);
        }

        void ProcessFile(string area, string path, bool initial, DateTime cutoff, Dictionary<string, AlertGroup> groups)
        {
            FileInfo fi = new FileInfo(path);
            long length = fi.Length;
            FileState st;
            if (!States.TryGetValue(path, out st))
            {
                int pre;
                st = new FileState();
                st.Enc = DetectEncoding(path, out pre);
                if (initial && fi.LastWriteTime < cutoff) { st.Offset = length; st.LineNo = -1; }
                else { st.Offset = pre; st.LineNo = 0; }
                States[path] = st;
            }

            if (length < st.Offset)
            {
                int pre2;
                st.Enc = DetectEncoding(path, out pre2);
                st.Offset = pre2;
                st.LineNo = 0;
                st.LastSeenLength = -1;
            }
            if (length == st.Offset) { st.LastSeenLength = length; return; }

            int want = (int)Math.Min(length - st.Offset, (long)MaxReadBytes);
            byte[] buf = new byte[want];
            int got = 0;
            using (FileStream fs = OpenShared(path))
            {
                fs.Seek(st.Offset, SeekOrigin.Begin);
                while (got < want)
                {
                    int r = fs.Read(buf, got, want - got);
                    if (r <= 0) break;
                    got += r;
                }
            }
            if (IsUtf16(st.Enc)) got -= got % 2;

            int consume = LastLineEnd(buf, got, st.Enc);
            if (consume < got)
            {
                bool reachedEnd = (st.Offset + got) >= length;
                // Flush an unterminated last line once the file has stopped growing, or a huge line.
                if ((reachedEnd && st.LastSeenLength == length) || (consume == 0 && got >= MaxReadBytes)) consume = got;
            }
            st.LastSeenLength = length;
            if (consume == 0) return;

            string text = st.Enc.GetString(buf, 0, consume);
            st.Offset += consume;

            string[] lines = text.Split('\n');
            int count = lines.Length;
            if (text.EndsWith("\n", StringComparison.Ordinal)) count--;

            for (int i = 0; i < count; i++)
            {
                if (st.LineNo >= 0) st.LineNo++;
                string line = lines[i].TrimEnd('\r');
                if (line.Length == 0) continue;

                string severity = null;
                if (ErrRx.IsMatch(line)) severity = "ERROR";
                else if (S.IncludeWarnings && WarnRx != null && WarnRx.IsMatch(line)) severity = "WARNING";
                if (severity == null) continue;

                bool ignored = false;
                foreach (Regex rx in IgnoreRx) { if (rx.IsMatch(line)) { ignored = true; break; } }
                if (ignored) continue;

                string trimmed = line.Trim();
                if (trimmed.Length > 300) trimmed = trimmed.Substring(0, 300);
                string key = severity + "|" + path + "|" + DigitsRx.Replace(trimmed, "#");

                AlertGroup g;
                if (groups.TryGetValue(key, out g)) { g.Count++; }
                else
                {
                    g = new AlertGroup();
                    g.Severity = severity;
                    g.Area = area;
                    g.File = path;
                    g.Sample = trimmed;
                    g.FirstLine = st.LineNo;
                    g.Count = 1;
                    groups[key] = g;
                }
            }
        }
    }
}
