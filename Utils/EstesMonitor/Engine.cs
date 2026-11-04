// Estes Monitor engine: config, file tailing, and error matching shared by the console and UI apps.
// Targets .NET Framework 4.x and the built-in C# 5 compiler: no installs needed to build or run.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
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
        public string OutputDir = "logs";
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
  ""maxAlertGroupsPerFile"": 20,
  ""outputDir"": ""logs"",
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
