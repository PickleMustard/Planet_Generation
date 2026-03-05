using System;

namespace UtilityLibrary.TaskSystem
{
    public class WorkStep
    {
        public string Name { get; }
        public Action Action { get; }
        public Func<int> ActionWithResult { get; }

        public bool HasResult => ActionWithResult != null;

        public WorkStep(string name, Action action)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Action = action ?? throw new ArgumentNullException(nameof(action));
            ActionWithResult = null;
        }

        public WorkStep(string name, Func<int> actionWithResult)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ActionWithResult = actionWithResult ?? throw new ArgumentNullException(nameof(actionWithResult));
            Action = null;
        }

        public int Execute()
        {
            if (HasResult)
            {
                return ActionWithResult();
            }
            Action?.Invoke();
            return 0;
        }

        public override string ToString() => $"WorkStep: {Name}";
    }
}
