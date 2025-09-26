using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace UtilityLibrary
{
    public class FunctionTimer
    {
        private class TimerInfo
        {
            public string ObjectName = "Global";
            public string FunctionName = "";
            public Stopwatch Stopwatch = new Stopwatch();
            public IPercent PercentRef; // external progress reference
            public DateTime StartTime;
            public int SpinnerIndex = 0;
        }

        // Active timers keyed by composite key "object::function"
        private static readonly Dictionary<string, TimerInfo> activeTimers = new Dictionary<string, TimerInfo>();
        // Completed runtimes keyed by the same composite key
        private static readonly Dictionary<string, TimeSpan> completedRuntimes = new Dictionary<string, TimeSpan>();

        // Animation / rendering state
        private static Thread animationThread;
        private static volatile bool isAnimating = false;
        private static readonly object lockObject = new object();

        // Fallback pinned dashboard state
        private static int lastDashboardLineCount = 0;
        private static bool printedCompletedHeader = false;

        private static readonly string[] spinnerFrames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };

        // ANSI / Scroll-region state
        private const string CSI = "\x1b[";
        private static bool ansiEnabled = false;              // overall ANSI enabled
        private static bool useScrollRegion = false;          // whether to use DECSTBM path
        private static bool scrollRegionApplied = false;      // whether DECSTBM currently applied
        private static int? cachedRows = null;                // terminal rows cache
        private static int reservedHeightCurrent = 0;         // current reserved height

        // Backward-compatible API (uses Global as object name)
        public static void StartTiming(string functionName, IPercent percent)
        {
            StartTiming("Global", functionName, percent);
        }

        public static TimeSpan StopTiming(string functionName)
        {
            return StopTiming("Global", functionName);
        }

        public static T TimeFunction<T>(string functionName, Func<T> function, IPercent percent)
        {
            return TimeFunction("Global", functionName, function, percent);
        }

        public static void TimeAction(string functionName, Action action, IPercent percent)
        {
            TimeAction("Global", functionName, action, percent);
        }

        // New multi-object API
        public static void StartTiming(string objectName, string functionName, IPercent percent)
        {
            lock (lockObject)
            {
                string key = MakeKey(objectName, functionName);
                if (activeTimers.TryGetValue(key, out var info))
                {
                    info.Stopwatch.Restart();
                    info.PercentRef = percent ?? info.PercentRef; // keep previous ref if null
                    info.StartTime = DateTime.Now;
                }
                else
                {
                    info = new TimerInfo
                    {
                        ObjectName = objectName,
                        FunctionName = functionName,
                        PercentRef = percent,
                        StartTime = DateTime.Now
                    };
                    info.Stopwatch.Start();
                    activeTimers[key] = info;
                }

                // Log start
                GD.Print($"[{info.StartTime:HH:mm:ss.fff}] Starting: [{objectName}] {functionName}");

                // Ensure animation is running while there are active timers
                StartAnimation();
            }
        }

        public static TimeSpan StopTiming(string objectName, string functionName)
        {
            lock (lockObject)
            {
                string key = MakeKey(objectName, functionName);
                if (!activeTimers.TryGetValue(key, out var info))
                {
                    return TimeSpan.Zero;
                }

                info.Stopwatch.Stop();
                var elapsed = info.Stopwatch.Elapsed;
                activeTimers.Remove(key);
                completedRuntimes[key] = elapsed;

                // Print completed section header once
                if (!printedCompletedHeader)
                {
                    printedCompletedHeader = true;
                    GD.Print("\n📗 Completed Timings:");
                    GD.Print(new string('═', 60));
                }

                // Print completed line above the dashboard (in normal scroll area)
                GD.Print($"✅ [{DateTime.Now:HH:mm:ss.fff}] [{objectName}] {functionName} — {FormatTimeSpan(elapsed)}");

                // If no more active timers, stop animation shortly after
                if (activeTimers.Count == 0)
                {
                    StopAnimation();
                }

                return elapsed;
            }
        }

        public static TimeSpan GetRuntime(string objectName, string functionName)
        {
            lock (lockObject)
            {
                string key = MakeKey(objectName, functionName);
                if (completedRuntimes.TryGetValue(key, out var ts)) return ts;
                if (activeTimers.TryGetValue(key, out var info)) return info.Stopwatch.Elapsed;
                return TimeSpan.Zero;
            }
        }

        public static Dictionary<string, TimeSpan> GetAllRuntimes()
        {
            lock (lockObject)
            {
                // Return a copy to avoid external mutation
                return new Dictionary<string, TimeSpan>(completedRuntimes);
            }
        }

        public static void PrintAllRuntimes()
        {
            lock (lockObject)
            {
                GD.Print("\n📊 Function Runtime Summary:");
                GD.Print(new string('═', 60));

                foreach (var kvp in completedRuntimes)
                {
                    var (obj, fn) = ParseKey(kvp.Key);
                    GD.Print($"[{obj}] {fn}".PadRight(40) + " " + FormatTimeSpan(kvp.Value));
                }

                GD.Print(new string('═', 60));
                var total = TimeSpan.Zero;
                foreach (var ts in completedRuntimes.Values) total += ts;
                GD.Print($"{"Total".PadRight(40)} {FormatTimeSpan(total)}");
            }
        }

        // Animation handling
        private static void StartAnimation()
        {
            if (isAnimating) return;

            // Initialize ANSI capability lazily on first start
            InitializeAnsiCapability();

            isAnimating = true;
            animationThread = new Thread(AnimateConsole);
            animationThread.IsBackground = true;
            animationThread.Start();
        }

        private static void StopAnimation()
        {
            isAnimating = false;
            if (animationThread != null && animationThread.IsAlive)
            {
                animationThread.Join(250);
            }

            // After stopping, reset scroll region if we used it; otherwise clear fallback dashboard
            if (useScrollRegion && scrollRegionApplied)
            {
                ResetScrollRegionAndClear();
            }
            else
            {
                ClearPreviousDashboardFallback();
            }
        }

        private static void AnimateConsole()
        {
            while (isAnimating)
            {
                List<string> contentLines;
                int activeCount;
                lock (lockObject)
                {
                    // Build dashboard content from a snapshot
                    contentLines = BuildDashboardContentLines();
                    activeCount = activeTimers.Count;

                    // Advance spinners for next tick
                    foreach (var info in activeTimers.Values)
                    {
                        info.SpinnerIndex = (info.SpinnerIndex + 1) % spinnerFrames.Length;
                    }
                }

                if (useScrollRegion)
                {
                    RenderDashboardWithScrollRegion(contentLines, activeCount);
                }
                else
                {
                    RenderDashboardFallback(contentLines);
                }

                Thread.Sleep(200); // ~5Hz update cadence
            }
        }

        // Build dashboard lines (header + 1 per timer + footer) — content only
        private static List<string> BuildDashboardContentLines()
        {
            var lines = new List<string>();
            if (activeTimers.Count == 0)
            {
                return lines;
            }

            lines.Add("── Active Timers ─────────────────────────────────────────────");

            // Order by object then function for stability
            foreach (var kv in activeTimers.OrderBy(k => k.Value.ObjectName).ThenBy(k => k.Value.FunctionName))
            {
                var info = kv.Value;
                var elapsed = info.Stopwatch.Elapsed;
                string elapsedStr = FormatTimeSpan(elapsed);

                string percentPart = "";
                if (info.PercentRef != null && info.PercentRef.PercentTotal > 0)
                {
                    int pct = (int)(info.PercentRef.Percent * 100.0f);
                    percentPart = $" — {pct}% ({info.PercentRef.PercentCurrent}/{info.PercentRef.PercentTotal})";
                }

                string spinner = spinnerFrames[info.SpinnerIndex];
                // [Object] Function — time — percent
                lines.Add($"{spinner} [{info.ObjectName}] {info.FunctionName} — {elapsedStr}{percentPart}");
            }

            lines.Add("──────────────────────────────────────────────────────────────");
            return lines;
        }

        // ============ DECSTBM scroll-region path ============
        private static void RenderDashboardWithScrollRegion(List<string> contentLines, int activeCount)
        {
            if (!ansiEnabled)
            {
                // Safety: fall back if ANSI got disabled mid-run
                RenderDashboardFallback(contentLines);
                return;
            }

            if (activeCount <= 0)
            {
                return; // nothing to draw
            }

            int reservedHeightNeeded = 2 /*header+footer*/ + activeCount;

            // Ensure scroll region is applied and sized appropriately
            if (!EnsureScrollRegion(reservedHeightNeeded))
            {
                // Could not determine rows / apply region — fall back
                RenderDashboardFallback(contentLines);
                return;
            }

            // We now know rows and have a reserved area: draw into it
            int rows = cachedRows ?? 0;
            int topOfReserved = Math.Max(1, rows - reservedHeightCurrent + 1);

            // Save cursor and hide
            GD.PrintRaw(CSI + "s");
            GD.PrintRaw("\x1b[?25l");

            // Move to top of reserved region
            GD.PrintRaw($"{CSI}{topOfReserved};1H");

            // Write all reserved lines, clearing each first
            int i = 0;
            foreach (var line in contentLines)
            {
                GD.PrintRaw(CSI + "2K"); // clear line
                GD.PrintRaw(line);
                GD.PrintRaw("\r\n");
                i++;
            }
            // Pad remaining reserved lines with blanks if needed (keeps previous text from lingering)
            for (; i < reservedHeightCurrent; i++)
            {
                GD.PrintRaw(CSI + "2K\r\n");
            }

            // Restore cursor and show
            GD.PrintRaw(CSI + "u");
            GD.PrintRaw("\x1b[?25h");
        }

        private static void ResetScrollRegionAndClear()
        {
            if (!scrollRegionApplied) return;

            int rows = cachedRows ?? 0;
            int topOfReserved = Math.Max(1, rows - Math.Max(0, reservedHeightCurrent) + 1);

            // Save cursor, reset region, clear old reserved area, restore
            GD.PrintRaw(CSI + "s");
            GD.PrintRaw("\x1b[?25l");

            // Reset scroll region to full
            GD.PrintRaw(CSI + "r");

            if (rows > 0 && reservedHeightCurrent > 0)
            {
                GD.PrintRaw($"{CSI}{topOfReserved};1H");
                for (int i = 0; i < reservedHeightCurrent; i++)
                {
                    GD.PrintRaw(CSI + "2K\r\n");
                }
            }

            GD.PrintRaw(CSI + "u");
            GD.PrintRaw("\x1b[?25h");

            scrollRegionApplied = false;
            reservedHeightCurrent = 0;
        }

        private static bool EnsureScrollRegion(int reservedHeightNeeded)
        {
            // Determine terminal rows once
            if (cachedRows == null)
            {
                cachedRows = DetectTerminalRows();
            }

            if (cachedRows == null || cachedRows.Value <= 0)
            {
                return false;
            }

            // Clamp reserved height to at most half the screen to reduce disruption
            int rows = cachedRows.Value;
            int reserved = Math.Max(3, Math.Min(reservedHeightNeeded, Math.Max(3, rows / 2)));
            if (!scrollRegionApplied || reserved != reservedHeightCurrent)
            {
                reservedHeightCurrent = reserved;
                int bottomScrollable = Math.Max(1, rows - reservedHeightCurrent);

                // Save cursor, set scroll region, restore
                GD.PrintRaw(CSI + "s");
                GD.PrintRaw("\x1b[?25l");

                // Set region: top=1; bottom=rows - reserved
                GD.PrintRaw($"{CSI}1;{bottomScrollable}r");

                // Restore cursor
                GD.PrintRaw(CSI + "u");
                GD.PrintRaw("\x1b[?25h");

                scrollRegionApplied = true;
            }

            return true;
        }

        private static int? DetectTerminalRows()
        {
            // Priority order:
            // 1) Env override FUNCTION_TIMER_ROWS
            // 2) Console.WindowHeight (if available)
            // 3) DSR query (ESC[6n]) with short timeout when enabled via env
            // 4) Env LINES
            // 5) Unknown => null

            // 1) Explicit override
            try
            {
                var envRows = System.Environment.GetEnvironmentVariable("FUNCTION_TIMER_ROWS");
                if (!string.IsNullOrEmpty(envRows) && int.TryParse(envRows, out var r1) && r1 > 0)
                    return r1;
            }
            catch { }

            // 2) Console API
            try
            {
                int win = Console.WindowHeight;
                if (win > 0) return win;
            }
            catch { }

            // 3) Optional DSR query
            try
            {
                var q = System.Environment.GetEnvironmentVariable("FUNCTION_TIMER_QUERY_ROWS");
                bool doQuery = string.Equals(q, "1") || string.Equals(q, "true", StringComparison.OrdinalIgnoreCase);
                if (doQuery)
                {
                    // Ask for cursor position, then move to a very large row first to clamp at bottom
                    GD.PrintRaw($"{CSI}999;1H");
                    GD.PrintRaw($"{CSI}6n");

                    using (var stdin = Console.OpenStandardInput())
                    {
                        // Read with a very small timeout; abort if not supported
                        try { stdin.ReadTimeout = 50; } catch { }
                        var buf = new byte[64];
                        int total = 0;
                        var start = DateTime.UtcNow;
                        while ((DateTime.UtcNow - start).TotalMilliseconds < 60 && total < buf.Length)
                        {
                            if (stdin.CanRead)
                            {
                                int n = 0;
                                try { n = stdin.Read(buf, total, buf.Length - total); }
                                catch (IOException) { break; }
                                if (n <= 0) break;
                                total += n;
                                if (buf[total - 1] == (byte)'R') break; // end of DSR
                            }
                            else break;
                        }

                        if (total > 0)
                        {
                            var s = Encoding.ASCII.GetString(buf, 0, total);
                            // Expect "\x1b[<row>;<col>R"
                            int startIdx = s.LastIndexOf("\x1b[");
                            if (startIdx >= 0)
                            {
                                int rIdx = s.IndexOf('R', startIdx + 2);
                                if (rIdx > startIdx)
                                {
                                    var body = s.Substring(startIdx + 2, rIdx - (startIdx + 2));
                                    var parts = body.Split(';');
                                    if (parts.Length >= 1 && int.TryParse(parts[0], out var rr) && rr > 0)
                                        return rr; // row number
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // 4) LINES env (common in shells)
            try
            {
                var linesEnv = System.Environment.GetEnvironmentVariable("LINES");
                if (!string.IsNullOrEmpty(linesEnv) && int.TryParse(linesEnv, out var r2) && r2 > 0)
                    return r2;
            }
            catch { }

            // 5) Unknown
            return null;
        }

        private static void InitializeAnsiCapability()
        {
            // Allow explicit disable/force via env
            string disable = System.Environment.GetEnvironmentVariable("FUNCTION_TIMER_DISABLE_ANSI");
            string force = System.Environment.GetEnvironmentVariable("FUNCTION_TIMER_FORCE_ANSI");

            bool forcedOn = !string.IsNullOrEmpty(force) && (force == "1" || force.Equals("true", StringComparison.OrdinalIgnoreCase));
            bool forcedOff = !string.IsNullOrEmpty(disable) && (disable == "1" || disable.Equals("true", StringComparison.OrdinalIgnoreCase));

            // Simple heuristics
            string term = System.Environment.GetEnvironmentVariable("TERM");
            string ci = System.Environment.GetEnvironmentVariable("CI");
            bool likelyTty = !string.IsNullOrEmpty(term) && !term.Equals("dumb", StringComparison.OrdinalIgnoreCase);
            bool inCI = !string.IsNullOrEmpty(ci) && (ci == "1" || ci.Equals("true", StringComparison.OrdinalIgnoreCase));

            ansiEnabled = forcedOn || (!forcedOff && likelyTty && !inCI);
            useScrollRegion = ansiEnabled; // enable scroll region path when ANSI looks available
        }

        // ============ Fallback pinned dashboard path ============
        private static void RenderDashboardFallback(List<string> contentLines)
        {
            // Attempt to keep the dashboard pinned by rewriting the same block using ANSI cursor-up
            // This is the old behavior, kept as a fallback when DECSTBM isn't used.

            // Move cursor up to the start of the previous dashboard block
            if (lastDashboardLineCount > 0)
            {
                GD.PrintRaw($"{CSI}{lastDashboardLineCount}A"); // move up
                // Clear previous lines
                for (int i = 0; i < lastDashboardLineCount; i++)
                {
                    GD.PrintRaw(CSI + "2K\r\n"); // clear line + newline
                }
                GD.PrintRaw($"{CSI}{lastDashboardLineCount}A"); // move back up to start
            }

            // Print new dashboard with temporary cursor hide/show
            GD.PrintRaw("\x1b[?25l");

            foreach (var line in contentLines)
            {
                GD.PrintRaw(line);
                GD.PrintRaw("\r\n");
            }

            GD.PrintRaw("\x1b[?25h");

            lastDashboardLineCount = contentLines.Count;
        }

        private static void ClearPreviousDashboardFallback()
        {
            if (lastDashboardLineCount <= 0) return;
            GD.PrintRaw($"{CSI}{lastDashboardLineCount}A");
            for (int i = 0; i < lastDashboardLineCount; i++)
            {
                GD.PrintRaw(CSI + "2K\r\n");
            }
            lastDashboardLineCount = 0;
        }

        private static string FormatTimeSpan(TimeSpan time)
        {
            if (time.TotalHours >= 1)
                return time.ToString("hh':'mm':'ss'.'fff");
            else if (time.TotalMinutes >= 1)
                return time.ToString("mm':'ss'.'fff");
            else if (time.TotalSeconds >= 1)
                return time.ToString("ss'.'fff's'");
            else
                return $"{time.TotalMilliseconds:F0}ms";
        }

        private static string MakeKey(string objectName, string functionName) => $"{objectName}::{functionName}";
        private static (string obj, string fn) ParseKey(string key)
        {
            int sep = key.IndexOf("::", StringComparison.Ordinal);
            if (sep < 0) return ("Global", key);
            return (key.Substring(0, sep), key.Substring(sep + 2));
        }

        // Helper wrappers with object context
        public static T TimeFunction<T>(string objectName, string functionName, Func<T> function, IPercent percent)
        {
            StartTiming(objectName, functionName, percent);
            try { return function(); }
            finally { StopTiming(objectName, functionName); }
        }

        public static void TimeAction(string objectName, string functionName, Action action, IPercent percent)
        {
            StartTiming(objectName, functionName, percent);
            try { action(); }
            finally { StopTiming(objectName, functionName); }
        }
    }
}
