using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace UtilityLibrary
{
    public partial class TaskTimer : Node
    {
        public static TaskTimer Instance { get; private set; }

        [Signal]
        public delegate void TimerStartedEventHandler(
            string name,
            int totalSteps,
            string[] stepNames
        );

        [Signal]
        public delegate void TimerStepChangedEventHandler(
            string name,
            int newStep,
            string stepName,
            double elapsedSeconds
        );

        [Signal]
        public delegate void TimerCompletedEventHandler(string name, double totalSeconds);

        [Signal]
        public delegate void TimerStepDurationEventHandler(string name, int stepIndex, string stepName, double durationSeconds);

        private readonly Dictionary<string, TimerInfo> _activeTimers = new();
        private readonly List<TimerInfo> _completedTimers = new();
        private readonly object _lock = new();

        public IReadOnlyDictionary<string, TimerInfo> ActiveTimers => _activeTimers;
        public IReadOnlyList<TimerInfo> CompletedTimers => _completedTimers;

        public override void _Ready()
        {
            Instance = this;
            if (SignalBus.Instance != null)
            {
                SignalBus.Instance.AutoConnect(this);
            }
        }

        public override void _ExitTree()
        {
            if (SignalBus.Instance != null)
            {
                SignalBus.Instance.AutoDisconnect(this);
            }
        }

        [SignalHandler("StartTimer")]
        private void OnStartTimer(string name, int totalSteps, int startingStep, string[] stepNames)
        {
            lock (_lock)
            {
                if (_activeTimers.ContainsKey(name))
                {
                    GameLogger.Warning($"Timer '{name}' already exists, overwriting");
                    _activeTimers.Remove(name);
                }

                var timer = new TimerInfo
                {
                    Name = name,
                    TotalSteps = totalSteps,
                    CurrentStep = startingStep,
                    StepNames = stepNames,
                    StartTime = DateTime.Now,
                    IsComplete = false,
                };

                timer.RecordStepStart();

                GameLogger.Info($"Starting timer '{name}' with {totalSteps} steps");

                _activeTimers[name] = timer;
                EmitSignal(SignalName.TimerStarted, name, totalSteps, stepNames);
            }
        }

        [SignalHandler("IncrementTimerStep")]
        private void OnIncrementStep(string name)
        {
            lock (_lock)
            {
                if (!_activeTimers.TryGetValue(name, out var timer))
                {
                    GameLogger.Warning($"Cannot increment step: timer '{name}' not found");
                    return;
                }

                int completedStepIndex = timer.CurrentStep;
                timer.RecordStepComplete(completedStepIndex);

                var stepDuration = timer.GetStepDuration(completedStepIndex);
                if (timer.StepNames != null && completedStepIndex < timer.StepNames.Length)
                {
                    EmitSignal(
                        SignalName.TimerStepDuration,
                        name,
                        completedStepIndex,
                        timer.StepNames[completedStepIndex],
                        stepDuration.TotalSeconds
                    );
                }

                timer.CurrentStep = Math.Min(timer.CurrentStep + 1, timer.TotalSteps);
                timer.Elapsed = DateTime.Now - timer.StartTime;
                timer.RecordStepStart();

                EmitSignal(
                    SignalName.TimerStepChanged,
                    name,
                    timer.CurrentStep,
                    timer.CurrentStepName,
                    timer.Elapsed.TotalSeconds
                );
            }
        }

        [SignalHandler("StopTimer")]
        private void OnStopTimer(string name)
        {
            lock (_lock)
            {
                if (!_activeTimers.TryGetValue(name, out var timer))
                {
                    GameLogger.Warning($"Cannot stop timer: '{name}' not found");
                    return;
                }

                if (timer.CurrentStep < timer.TotalSteps)
                {
                    timer.RecordStepComplete(timer.CurrentStep);
                }

                timer.CurrentStep = timer.TotalSteps;
                timer.Elapsed = DateTime.Now - timer.StartTime;
                timer.IsComplete = true;

                _activeTimers.Remove(name);
                _completedTimers.Add(timer);

                GameLogger.Info($"Timer '{name}' completed in {timer.Elapsed.TotalSeconds:F2}s (avg step: {timer.AverageStepDuration.TotalMilliseconds:F1}ms)");

                EmitSignal(SignalName.TimerCompleted, name, timer.Elapsed.TotalSeconds);
            }
        }

        public TimerInfo GetTimer(string name)
        {
            lock (_lock)
            {
                if (_activeTimers.TryGetValue(name, out var active))
                    return active;
                return _completedTimers.FirstOrDefault(t => t.Name == name);
            }
        }

        public double GetTotalTime(string name)
        {
            var timer = GetTimer(name);
            return timer?.Elapsed.TotalSeconds ?? 0;
        }

        public double GetStepTime(string name, int stepIndex)
        {
            var timer = GetTimer(name);
            return timer?.GetStepDuration(stepIndex).TotalSeconds ?? 0;
        }

        public void ClearCompleted()
        {
            lock (_lock)
            {
                _completedTimers.Clear();
            }
        }

        public void ClearAll()
        {
            lock (_lock)
            {
                _activeTimers.Clear();
                _completedTimers.Clear();
            }
        }

        public Dictionary<string, object> GetTimerStats(string name)
        {
            var timer = GetTimer(name);
            if (timer == null) return null;

            return new Dictionary<string, object>
            {
                { "name", timer.Name },
                { "totalSteps", timer.TotalSteps },
                { "currentStep", timer.CurrentStep },
                { "progress", timer.Progress },
                { "elapsedSeconds", timer.Elapsed.TotalSeconds },
                { "isComplete", timer.IsComplete },
                { "averageStepMs", timer.AverageStepDuration.TotalMilliseconds },
                { "stepDurations", timer.StepDurations.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.TotalMilliseconds
                )}
            };
        }
    }
}
