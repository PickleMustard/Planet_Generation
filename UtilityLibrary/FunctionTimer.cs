using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace UtilityLibrary
{
    public class FunctionTimer
    {
        private static Dictionary<string, Stopwatch> activeTimers = new Dictionary<string, Stopwatch>();
        private static Dictionary<string, TimeSpan> functionRuntimes = new Dictionary<string, TimeSpan>();
        private static Dictionary<string, IPercent> functionPercents = new Dictionary<string, IPercent>();
        private static string currentFunction = "";
        private static Thread animationThread;
        private static bool isAnimating = false;
        private static object lockObject = new object();

        public static void StartTiming(string functionName, IPercent percent)
        {
            lock (lockObject)
            {
                if (activeTimers.ContainsKey(functionName))
                {
                    activeTimers[functionName].Restart();
                }
                else
                {
                    activeTimers[functionName] = Stopwatch.StartNew();
                }
                if (functionPercents.ContainsKey(functionName))
                {
                    functionPercents[functionName] = percent;
                }
                else if (percent != null && percent.PercentTotal > 0)
                {
                    functionPercents.Add(functionName, percent);
                }

                currentFunction = functionName;

                // Log start time
                string startTime = DateTime.Now.ToString("HH:mm:ss.fff");
                GD.Print($"[{startTime}] Starting: {functionName}");

                // Start animation
                StartAnimation();
            }
        }

        public static TimeSpan StopTiming(string functionName)
        {
            lock (lockObject)
            {
                if (activeTimers.ContainsKey(functionName))
                {
                    var timer = activeTimers[functionName];
                    timer.Stop();

                    var elapsed = timer.Elapsed;
                    functionRuntimes[functionName] = elapsed;

                    // Log finish time and runtime
                    string finishTime = DateTime.Now.ToString("HH:mm:ss.fff");
                    GD.Print($"[{finishTime}] ✅ Finished: {functionName} - Runtime: {FormatTimeSpan(elapsed)}");

                    if (currentFunction == functionName)
                    {
                        currentFunction = "";
                        StopAnimation();
                    }

                    return elapsed;
                }

                return TimeSpan.Zero;
            }
        }

        public static TimeSpan GetRuntime(string functionName)
        {
            lock (lockObject)
            {
                if (functionRuntimes.ContainsKey(functionName))
                {
                    return functionRuntimes[functionName];
                }
                return TimeSpan.Zero;
            }
        }

        public static Dictionary<string, TimeSpan> GetAllRuntimes()
        {
            lock (lockObject)
            {
                return new Dictionary<string, TimeSpan>(functionRuntimes);
            }
        }

        public static void PrintAllRuntimes()
        {
            lock (lockObject)
            {
                GD.Print("\n📊 Function Runtime Summary:");
                GD.Print(new string('═', 50));

                foreach (var kvp in functionRuntimes)
                {
                    GD.Print($"{kvp.Key.PadRight(30)} {FormatTimeSpan(kvp.Value)}");
                }

                GD.Print(new string('═', 50));

                var totalTime = TimeSpan.Zero;
                foreach (var runtime in functionRuntimes.Values)
                {
                    totalTime += runtime;
                }
                GD.Print($"{"Total".PadRight(30)} {FormatTimeSpan(totalTime)}");
            }
        }

        private static void StartAnimation()
        {
            if (!isAnimating)
            {
                isAnimating = true;
                animationThread = new Thread(AnimateConsole);
                animationThread.IsBackground = true;
                animationThread.Start();
            }
        }

        private static void StopAnimation()
        {
            isAnimating = false;
            if (animationThread != null && animationThread.IsAlive)
            {
                animationThread.Join(100);
            }
        }

        private static void AnimateConsole()
        {
            string[] spinner = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
            int spinnerIndex = 0;

            while (isAnimating && !string.IsNullOrEmpty(currentFunction))
            {
                if (activeTimers.ContainsKey(currentFunction))
                {
                    var elapsed = activeTimers[currentFunction].Elapsed;
                    string timeStr = FormatTimeSpan(elapsed);


                    // Use Godot's print for thread safety
                    GD.PrintRaw($"\r{spinner[spinnerIndex]} Processing: {currentFunction} - {timeStr}");
                    if (functionPercents.ContainsKey(currentFunction))
                    {
                        int percent = (int)(functionPercents[currentFunction].Percent * 100.0f);
                        GD.PrintRaw($"| {percent}%  | {functionPercents[currentFunction].PercentCurrent}");
                    }

                    spinnerIndex = (spinnerIndex + 1) % spinner.Length;
                }

                Thread.Sleep(200); // Update every 200ms
            }
        }

        private static string FormatTimeSpan(TimeSpan time)
        {
            if (time.TotalHours >= 1)
                return time.ToString(@"\:hh\:mm\:ss\.fff");
            else if (time.TotalMinutes >= 1)
                return time.ToString(@"\:mm\:ss\.fff");
            else if (time.TotalSeconds >= 1)
                return time.ToString(@"\:ss\.fff");
            else
                return $"{time.TotalMilliseconds:F0}ms";
        }

        // Helper method to wrap functions with timing
        public static T TimeFunction<T>(string functionName, Func<T> function, IPercent percent)
        {
            StartTiming(functionName, percent);
            try
            {
                T result = function();
                return result;
            }
            finally
            {
                StopTiming(functionName);
            }
        }

        public static void TimeAction(string functionName, Action action, IPercent percent)
        {
            StartTiming(functionName, percent);
            try
            {
                action();
            }
            finally
            {
                StopTiming(functionName);
            }
        }
    }
}
