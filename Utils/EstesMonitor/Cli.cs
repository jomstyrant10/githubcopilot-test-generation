// Estes Monitor console app: headless monitoring for servers and scheduled tasks.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace EstesMonitor
{
    internal static class Program
    {
        const string Version = "1.0.0";

        static Settings S;
        static string AlertDir;

        static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
                Console.Error.WriteLine("Run with --help for usage.");
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Fatal: " + ex.Message);
                return 2;
            }
        }

        static string Next(string[] args, ref int i)
        {
            if (i + 1 >= args.Length) throw new ArgumentException("Missing value after " + args[i]);
            i++;
            return args[i];
        }

        static int Run(string[] args)
        {
            string configPath = null;
            string outOverride = null;
            bool once = false, warnings = false, list = false, writeConfig = false, force = false, help = false;
            bool hoursSet = false;
            int hours = 0;
            int pollOverride = 0;
            List<string> areaFilter = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                switch (a)
                {
                    case "--config": configPath = Next(args, ref i); break;
                    case "--once": once = true; break;
                    case "--warnings": warnings = true; break;
                    case "--list": list = true; break;
                    case "--write-config": writeConfig = true; break;
                    case "--force": force = true; break;
                    case "--out": outOverride = Next(args, ref i); break;
                    case "--interval": pollOverride = int.Parse(Next(args, ref i)); break;
                    case "--hours":
                        {
                            string v = Next(args, ref i);
                            hours = v.Equals("all", StringComparison.OrdinalIgnoreCase) ? -1 : int.Parse(v);
                            hoursSet = true;
                            break;
                        }
                    case "--area":
                        foreach (string part in Next(args, ref i).Split(','))
                            if (part.Trim().Length > 0) areaFilter.Add(part.Trim());
                        break;
                    case "--help":
                    case "-h":
                    case "/?":
                        help = true; break;
                    default: throw new ArgumentException("Unknown option: " + args[i]);
                }
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string defaultConfig = Path.Combine(baseDir, "estes-monitor.config.json");

            if (help) { PrintHelp(); return 0; }

            if (writeConfig)
            {
                string target = configPath ?? defaultConfig;
                if (File.Exists(target) && !force)
                {
                    Console.WriteLine("Already exists (use --force to overwrite): " + target);
                    return 1;
                }
                File.WriteAllText(target, Config.DefaultJson, new UTF8Encoding(false));
                Console.WriteLine("Wrote default config: " + target);
                return 0;
            }

            string source;
            S = Config.Load(configPath, out source);
            if (warnings) S.IncludeWarnings = true;
            if (pollOverride > 0) S.PollSeconds = pollOverride;
            if (!hoursSet) hours = S.InitialScanHours;
            if (outOverride != null) S.OutputDir = outOverride;

            List<KeyValuePair<string, List<string>>> areas = S.Areas;
            if (areaFilter.Count > 0)
            {
                areas = S.Areas.Where(x => areaFilter.Any(f => x.Key.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                if (areas.Count == 0)
                    throw new ArgumentException("No area matches the filter. Areas: " + string.Join(", ", S.Areas.Select(x => x.Key).ToArray()));
            }

            MonitorEngine engine = new MonitorEngine(S);
            engine.Log = delegate(string m) { WriteConsole(m, ConsoleColor.DarkGray); };

            if (list) { PrintList(engine, areas, source); return 0; }

            AlertDir = Path.IsPathRooted(S.OutputDir) ? S.OutputDir : Path.Combine(baseDir, S.OutputDir);
            try { Directory.CreateDirectory(AlertDir); }
            catch (Exception)
            {
                AlertDir = Path.Combine(Path.GetTempPath(), "EstesMonitor");
                Directory.CreateDirectory(AlertDir);
            }

            DateTime cutoff = hours < 0 ? DateTime.MinValue : DateTime.Now.AddHours(-hours);
            string window = hours < 0 ? "all history" : (hours == 0 ? "new lines only" : "last " + hours + " h of history, then new lines");

            Banner("Estes Monitor " + Version + " on " + Environment.MachineName);
            Console.WriteLine("Config   : " + source);
            Console.WriteLine("Mode     : " + (once ? "scan once and exit" : "continuous, every " + S.PollSeconds + " s (Ctrl+C to stop)"));
            Console.WriteLine("Start    : " + window);
            Console.WriteLine("Severity : errors" + (S.IncludeWarnings ? " + warnings" : ""));
            Console.WriteLine("Alerts to: " + AlertDir);
            Console.WriteLine("Areas    : " + areas.Count + " (" + areas.Sum(x => x.Value.Count) + " folders / event sources)");
            Console.WriteLine();
            AppendAlertFile("=== Estes Monitor " + Version + " started on " + Environment.MachineName + " at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");

            ManualResetEvent stop = new ManualResetEvent(false);
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; stop.Set(); };

            bool initial = true;
            int cycle = 0;
            long totalErrorLines = 0;
            while (true)
            {
                int files;
                DateTime now = DateTime.Now;
                List<AlertGroup> found = engine.RunCycle(areas, initial, cutoff, out files);
                long errLines = Report(found, now);
                totalErrorLines += errLines;
                initial = false;
                cycle++;

                if (once)
                {
                    Console.WriteLine();
                    Console.WriteLine("Scanned " + files + " files and " + engine.EventSourcesQueried + " Event Viewer sources. Error lines/events found: " + errLines);
                    return errLines > 0 ? 1 : 0;
                }

                if (cycle % 10 == 0)
                    WriteConsole(now.ToString("yyyy-MM-dd HH:mm:ss") + " [OK] watching " + files + " files; error lines so far: " + totalErrorLines, ConsoleColor.DarkGray);

                if (stop.WaitOne(S.PollSeconds * 1000)) break;
            }

            string stopped = "=== Estes Monitor stopped at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===";
            AppendAlertFile(stopped);
            Console.WriteLine(stopped);
            return 0;
        }

        static void PrintHelp()
        {
            Console.WriteLine("Estes Monitor " + Version + " - watches DeltaV log folders for error words.");
            Console.WriteLine("(For the window version, run EstesMonitorUI.exe.)");
            Console.WriteLine();
            Console.WriteLine("Usage: EstesMonitor.exe [options]");
            Console.WriteLine();
            Console.WriteLine("  --once              Scan once, print results, exit (code 1 = errors found, 0 = none)");
            Console.WriteLine("  --hours <N|all>     On start, read files modified in the last N hours from the");
            Console.WriteLine("                      beginning (0 = only new lines, all = whole history)");
            Console.WriteLine("  --area <a,b>        Only areas whose name contains any of these words");
            Console.WriteLine("  --warnings          Also report warning words (WARNING, WARN, CAUTION, ...)");
            Console.WriteLine("  --interval <sec>    Polling interval in continuous mode");
            Console.WriteLine("  --out <dir>         Folder for the alert log files (default: logs next to the exe)");
            Console.WriteLine("  --config <file>     Use this config instead of estes-monitor.config.json");
            Console.WriteLine("  --list              Show areas, folders, and whether each exists, then exit");
            Console.WriteLine("  --write-config      Write the default config next to the exe so you can edit it");
            Console.WriteLine("  --force             Allow --write-config to overwrite an existing file");
            Console.WriteLine("  --help              This help");
            Console.WriteLine();
            Console.WriteLine("Without a config file the built-in Estes folders and DeltaV Event Viewer sources are");
            Console.WriteLine("used. Config entries starting with eventlog: are Event Viewer sources, for example");
            Console.WriteLine("eventlog:DeltaV Security or eventlog:Application!DVBManager,DVBRuntime (log!providers).");
            Console.WriteLine("Event sources report Critical and Error events (plus Warning with --warnings).");
            Console.WriteLine("Folders that do not exist on a machine are skipped and re-checked on every cycle.");
        }

        static void PrintList(MonitorEngine engine, List<KeyValuePair<string, List<string>>> areas, string source)
        {
            Console.WriteLine("Config: " + source);
            foreach (KeyValuePair<string, List<string>> area in areas)
            {
                Console.WriteLine();
                Console.WriteLine(area.Key);
                if (area.Value.Count == 0) { Console.WriteLine("  (no folders)"); continue; }
                foreach (string folder in area.Value)
                {
                    EventSource es;
                    if (EventSource.TryParse(folder, out es))
                    {
                        long events = MonitorEngine.EventLogRecordCount(es.Log);
                        Console.WriteLine(events < 0 ? "  [missing] " + es.Display() : "  [ok, " + events + " events] " + es.Display());
                        continue;
                    }
                    if (!Directory.Exists(folder)) { Console.WriteLine("  [missing] " + folder); continue; }
                    int n = engine.EnumFiles(folder).Count();
                    Console.WriteLine("  [ok, " + n + " files] " + folder);
                }
            }
        }

        // Returns the number of error lines (not warnings) reported.
        static long Report(List<AlertGroup> found, DateTime now)
        {
            if (found.Count == 0) return 0;

            List<AlertGroup> ordered = found
                .OrderBy(g => g.Severity == "ERROR" ? 0 : 1)
                .ThenBy(g => g.Area)
                .ThenBy(g => g.File)
                .ThenByDescending(g => g.Count)
                .ToList();

            Dictionary<string, int> printed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> hidden = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            long errorLines = 0;

            foreach (AlertGroup g in ordered)
            {
                if (g.Severity == "ERROR") errorLines += g.Count;

                int n;
                printed.TryGetValue(g.File, out n);
                if (n >= S.MaxAlertGroupsPerFile)
                {
                    int h;
                    hidden.TryGetValue(g.File, out h);
                    hidden[g.File] = h + 1;
                    continue;
                }
                printed[g.File] = n + 1;

                string line = string.Format("{0:yyyy-MM-dd HH:mm:ss} [{1}] [{2}] {3} line {4}{5} :: {6}",
                    now, g.Severity, g.Area, g.File, g.FirstLine >= 0 ? g.FirstLine.ToString() : "?",
                    g.Count > 1 ? " (x" + g.Count + ")" : "", g.Sample);
                WriteConsole(line, g.Severity == "ERROR" ? ConsoleColor.Red : ConsoleColor.Yellow);
                AppendAlertFile(line);
            }

            foreach (KeyValuePair<string, int> kv in hidden)
            {
                string line = string.Format("{0:yyyy-MM-dd HH:mm:ss} [INFO] {1}: {2} more distinct message(s) not shown (maxAlertGroupsPerFile)", now, kv.Key, kv.Value);
                WriteConsole(line, ConsoleColor.DarkGray);
                AppendAlertFile(line);
            }
            return errorLines;
        }

        static void Banner(string text)
        {
            Console.WriteLine(text);
            Console.WriteLine(new string('-', text.Length));
        }

        static void WriteConsole(string text, ConsoleColor color)
        {
            ConsoleColor old = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = old;
        }

        static void AppendAlertFile(string line)
        {
            try
            {
                string file = Path.Combine(AlertDir, "estes-alerts-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                File.AppendAllText(file, line + Environment.NewLine, new UTF8Encoding(false));
            }
            catch (Exception) { }
        }
    }
}
