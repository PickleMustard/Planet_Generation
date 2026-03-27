using System.Reflection;
using Godot;
using UtilityLibrary.TaskSystem;

namespace UtilityLibrary
{
    public partial class SignalBus : Node
    {
        public static SignalBus? Instance { get; private set; }

        [Signal]
        public delegate void StartTimerEventHandler(string name, int totalSteps, int startingStep, string[] stepNames);

        [Signal]
        public delegate void IncrementTimerStepEventHandler(string name);

        [Signal]
        public delegate void StopTimerEventHandler(string name);

        [Signal]
        public delegate void QueuePackageEventHandler(WorkPackage package);

        [Signal]
        public delegate void SystemGenerationCompleteEventHandler(string batchId, int totalBodies, int successfulBodies);

        public override void _Ready()
        {
            Instance = this;
        }

        public void EmitStartTimer(string name, int totalSteps, int startingStep, string[] stepNames)
        {
            EmitSignal(SignalName.StartTimer, name, totalSteps, startingStep, stepNames);
        }

        public void EmitIncrementTimerStep(string name)
        {
            EmitSignal(SignalName.IncrementTimerStep, name);
        }

        public void EmitStopTimer(string name)
        {
            EmitSignal(SignalName.StopTimer, name);
        }

        public void EmitQueuePackage(WorkPackage package)
        {
            EmitSignal(SignalName.QueuePackage, package);
        }

        public void EmitSystemGenerationComplete(string batchId, int totalBodies, int successfulBodies)
        {
            EmitSignal(SignalName.SystemGenerationComplete, batchId, totalBodies, successfulBodies);
        }

        public void Emit(string signalName, params Variant[] args)
        {
            EmitSignal(signalName, args);
        }

        public void ConnectStartTimer(Callable callable)
        {
            Connect(SignalName.StartTimer, callable);
        }

        public void ConnectIncrementTimerStep(Callable callable)
        {
            Connect(SignalName.IncrementTimerStep, callable);
        }

        public void ConnectStopTimer(Callable callable)
        {
            Connect(SignalName.StopTimer, callable);
        }

        public void ConnectQueuePackage(Callable callable)
        {
            Connect(SignalName.QueuePackage, callable);
        }

        public void ConnectSystemGenerationComplete(Callable callable)
        {
            Connect(SignalName.SystemGenerationComplete, callable);
        }

        public void ConnectToSignal(string signalName, Callable callable)
        {
            Connect(signalName, callable);
        }

        public void AutoConnect(GodotObject target)
        {
            if (target == null) return;

            var type = target.GetType();
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<SignalHandlerAttribute>();
                if (attr != null)
                {
                    var callable = new Callable(target, method.Name);
                    Connect(attr.SignalName, callable);
                }
            }
        }

        public void AutoDisconnect(GodotObject target)
        {
            if (target == null) return;

            var type = target.GetType();
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<SignalHandlerAttribute>();
                if (attr != null)
                {
                    var callable = new Callable(target, method.Name);
                    Disconnect(attr.SignalName, callable);
                }
            }
        }
    }
}
