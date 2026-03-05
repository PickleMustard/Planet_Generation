using System;
using System.Collections.Generic;

namespace UtilityLibrary.TaskSystem
{
    public class WorkPackageBuilder
    {
        private string _name;
        private readonly List<WorkStep> _steps = new();
        private TaskPriority _priority = TaskPriority.Normal;
        private string _batchId;
        private int? _maxRetries;
        private Action<string> _onCompleted;
        private Action<string, int, string> _onStepCompleted;
        private Action<string, int, string, string> _onStepFailed;
        private Action<string, string> _onPackageFailed;

        public WorkPackageBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public WorkPackageBuilder WithPriority(TaskPriority priority)
        {
            _priority = priority;
            return this;
        }

        public WorkPackageBuilder WithBatchId(string batchId)
        {
            _batchId = batchId;
            return this;
        }

        public WorkPackageBuilder AddStep(string name, Action action)
        {
            _steps.Add(new WorkStep(name, action));
            return this;
        }

        public WorkPackageBuilder AddStep(string name, Func<int> actionWithResult)
        {
            _steps.Add(new WorkStep(name, actionWithResult));
            return this;
        }

        public WorkPackageBuilder OnCompleted(Action<string> handler)
        {
            _onCompleted = handler;
            return this;
        }

        public WorkPackageBuilder OnStepCompleted(Action<string, int, string> handler)
        {
            _onStepCompleted = handler;
            return this;
        }

        public WorkPackageBuilder OnStepFailed(Action<string, int, string, string> handler)
        {
            _onStepFailed = handler;
            return this;
        }

        public WorkPackageBuilder OnFailed(Action<string, string> handler)
        {
            _onPackageFailed = handler;
            return this;
        }

        public WorkPackageBuilder WithMaxRetries(int maxRetries)
        {
            if (maxRetries <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRetries), "Max retries must be greater than 0");
            }
            _maxRetries = maxRetries;
            return this;
        }

        public WorkPackage Build()
        {
            if (string.IsNullOrEmpty(_name))
            {
                throw new InvalidOperationException("WorkPackage must have a name. Call WithName() first.");
            }

            if (_steps.Count == 0)
            {
                throw new InvalidOperationException("WorkPackage must have at least one step. Call AddStep() at least once.");
            }

            var package = new WorkPackage(_name, _steps.AsReadOnly(), _priority, _batchId, _maxRetries ?? 3);

            if (_onCompleted != null)
            {
                package.StepCompleted += (name, stepIndex, stepName) =>
                {
                    if (package.IsComplete)
                    {
                        _onCompleted.Invoke(name);
                    }
                };
            }

            if (_onStepCompleted != null)
            {
                package.StepCompleted += (name, stepIndex, stepName) =>
                {
                    _onStepCompleted.Invoke(name, stepIndex, stepName);
                };
            }

            if (_onStepFailed != null)
            {
                package.StepFailed += (name, stepIndex, stepName, error) =>
                {
                    _onStepFailed.Invoke(name, stepIndex, stepName, error);
                };
            }

            if (_onPackageFailed != null)
            {
                package.PackageFailed += (name, errorMessage) =>
                {
                    _onPackageFailed.Invoke(name, errorMessage);
                };
            }

            return package;
        }
    }
}
