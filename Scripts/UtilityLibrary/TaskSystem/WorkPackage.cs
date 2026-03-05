using System;
using System.Collections.Generic;
using Godot;

namespace UtilityLibrary.TaskSystem
{
    public partial class WorkPackage : RefCounted
    {
        public string Name { get; }
        public string BatchId { get; }
        public TaskPriority Priority { get; }
        public IReadOnlyList<WorkStep> Steps { get; }
        public string[] StepNames { get; }

        private int _currentStepIndex = 0;
        public int CurrentStepIndex => _currentStepIndex;

        public WorkStep CurrentStep => _currentStepIndex < Steps.Count ? Steps[_currentStepIndex] : null;
        public bool IsComplete => _currentStepIndex >= Steps.Count;
        public float Progress => Steps.Count > 0 ? (float)_currentStepIndex / Steps.Count : 0f;
        public int TotalSteps => Steps.Count;

        private int _maxRetries = 3;
        public int MaxRetries
        {
            get => _maxRetries;
            set => _maxRetries = value > 0 ? value : 1;
        }

        private bool _isFailed = false;
        public bool IsFailed => _isFailed;

        private readonly Dictionary<int, int> _stepRetryCount = new();

        [Signal]
        public delegate void StepCompletedEventHandler(string packageName, int stepIndex, string stepName);

        [Signal]
        public delegate void PackageCompletedEventHandler(string packageName, int resultCode);

        [Signal]
        public delegate void StepFailedEventHandler(string packageName, int stepIndex, string stepName, string error);

        [Signal]
        public delegate void PackageFailedEventHandler(string packageName, string errorMessage);

        public WorkPackage(string name, IReadOnlyList<WorkStep> steps, TaskPriority priority = TaskPriority.Normal, string batchId = null, int maxRetries = 3)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Steps = steps ?? throw new ArgumentNullException(nameof(steps));
            Priority = priority;
            BatchId = batchId;
            _maxRetries = maxRetries > 0 ? maxRetries : 1;

            var stepNames = new string[steps.Count];
            for (int i = 0; i < steps.Count; i++)
            {
                stepNames[i] = steps[i].Name;
            }
            StepNames = stepNames;
        }

        public int ExecuteNextStep()
        {
            if (IsComplete)
            {
                return -1;
            }

            var step = CurrentStep;

            while (true)
            {
                try
                {
                    int result = step.Execute();
                    _currentStepIndex++;
                    _stepRetryCount.Remove(_currentStepIndex - 1);

                    EmitSignal(SignalName.StepCompleted, Name, _currentStepIndex - 1, step.Name);

                    if (IsComplete)
                    {
                        EmitSignal(SignalName.PackageCompleted, Name, result);
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    int currentStep = _currentStepIndex;
                    int retryCount = _stepRetryCount.GetValueOrDefault(currentStep, 0);
                    retryCount++;
                    _stepRetryCount[currentStep] = retryCount;

                    if (retryCount < _maxRetries)
                    {
                        GD.Print($"Warning: Step '{step.Name}' failed (attempt {retryCount}/{_maxRetries}). Retrying...");
                        continue;
                    }

                    _isFailed = true;
                    string errorMessage = $"Step '{step.Name}' failed after {_maxRetries} attempts: {ex.Message}";
                    GD.PrintErr($"Package '{Name}' failed at step '{step.Name}': {errorMessage}");

                    EmitSignal(SignalName.StepFailed, Name, currentStep, step.Name, ex.Message);
                    EmitSignal(SignalName.PackageFailed, Name, errorMessage);

                    throw new PackageFailedException(
                        Name,
                        currentStep,
                        step.Name,
                        retryCount,
                        ex.Message,
                        ex
                    );
                }
            }
        }

        public void ExecuteAll()
        {
            while (!IsComplete)
            {
                ExecuteNextStep();
            }
        }

        public void Reset()
        {
            _currentStepIndex = 0;
            _stepRetryCount.Clear();
            _isFailed = false;
        }

        public override string ToString() => $"WorkPackage: {Name} ({_currentStepIndex}/{Steps.Count} steps, {Priority})";
    }
}
