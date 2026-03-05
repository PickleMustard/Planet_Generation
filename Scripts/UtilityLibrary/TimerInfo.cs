using System;
using System.Collections.Generic;

namespace UtilityLibrary
{
    public class TimerInfo
    {
        public string Name { get; set; }
        public int TotalSteps { get; set; }
        public int CurrentStep { get; set; }
        public string[] StepNames { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Elapsed { get; set; }
        public bool IsComplete { get; set; }
        public Dictionary<int, TimeSpan> StepDurations { get; set; } = new();
        private DateTime _lastStepTime;

        public string CurrentStepName =>
            StepNames != null && CurrentStep < StepNames.Length
                ? StepNames[CurrentStep]
                : $"Step {CurrentStep}";

        public float Progress => TotalSteps > 0 ? (float)CurrentStep / TotalSteps : 0f;

        public TimeSpan AverageStepDuration
        {
            get
            {
                if (StepDurations.Count == 0) return TimeSpan.Zero;
                var totalTicks = 0L;
                foreach (var duration in StepDurations.Values)
                {
                    totalTicks += duration.Ticks;
                }
                return TimeSpan.FromTicks(totalTicks / StepDurations.Count);
            }
        }

        public void RecordStepStart()
        {
            _lastStepTime = DateTime.Now;
        }

        public void RecordStepComplete(int stepIndex)
        {
            var now = DateTime.Now;
            StepDurations[stepIndex] = now - _lastStepTime;
            _lastStepTime = now;
        }

        public TimeSpan GetStepDuration(int stepIndex)
        {
            return StepDurations.TryGetValue(stepIndex, out var duration) ? duration : TimeSpan.Zero;
        }
    }
}
