using System;
using System.Reflection;
using Godot;
using Godot.Collections;
using ProceduralGeneration.MeshGeneration;
using UtilityLibrary.TaskSystem;

namespace UtilityLibrary
{
    public partial class SignalBus : Node
    {
        public static SignalBus? Instance { get; private set; }

        /// <summary>
        /// Selected system template filename to be loaded by the next GameScene.
        /// Set by MainMenu before scene transition, consumed by GameScene on _Ready().
        /// </summary>
        public string? SelectedTemplate { get; set; }

        /// <summary>
        /// C# event for requesting system generation. Used instead of a Godot signal
        /// because the parameters include Barycenter (a Node3D subclass).
        /// </summary>
        public event Action<Array<Dictionary>, Array<Dictionary>, Array<Dictionary>, Barycenter>? GenerateSystemRequested;

        public void EmitGenerateSystemRequested(
            Array<Dictionary> dominantBodies,
            Array<Dictionary> satelliteBelts,
            Array<Dictionary> planetaryBodies,
            Barycenter barycenter)
        {
            GenerateSystemRequested?.Invoke(dominantBodies, satelliteBelts, planetaryBodies, barycenter);
        }

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

        /// <summary>
        /// Fired when a continent's power state changes (deficit or recovery).
        /// Parameters: continentIndex, isDeficit
        /// </summary>
        public event Action<int, bool>? ContinentPowerStateChanged;

        public void EmitContinentPowerStateChanged(int continentIndex, bool isDeficit)
        {
            ContinentPowerStateChanged?.Invoke(continentIndex, isDeficit);
        }

        /// <summary>
        /// Fired when a continent enters or exits a resource shortage.
        /// Parameters: continentIndex, resourceId, isShortage
        /// </summary>
        public event Action<int, string, bool>? ContinentResourceShortage;

        public void EmitContinentResourceShortage(int continentIndex, string resourceId, bool isShortage)
        {
            ContinentResourceShortage?.Invoke(continentIndex, resourceId, isShortage);
        }

        /// <summary>
        /// Fired when a transfer is dispatched from a continent.
        /// Parameters: orderId, originContinentIndex
        /// </summary>
        public event Action<string, int>? TransferDispatched;

        public void EmitTransferDispatched(string orderId, int originContinentIndex)
        {
            TransferDispatched?.Invoke(orderId, originContinentIndex);
        }

        /// <summary>
        /// Fired when a transfer arrives at its destination.
        /// Parameters: orderId, fullyAccepted
        /// </summary>
        public event Action<string, bool>? TransferArrived;

        public void EmitTransferArrived(string orderId, bool fullyAccepted)
        {
            TransferArrived?.Invoke(orderId, fullyAccepted);
        }

        /// <summary>
        /// Fired when resources are reverted to origin due to destination rejection.
        /// Parameters: orderId, originContinentIndex, revertedAmount
        /// </summary>
        public event Action<string, int, float>? TransferReverted;

        public void EmitTransferReverted(string orderId, int originContinentIndex, float revertedAmount)
        {
            TransferReverted?.Invoke(orderId, originContinentIndex, revertedAmount);
        }

        /// <summary>
        /// Fired when a transfer schedule changes state.
        /// Parameters: scheduleId, newState (as int for Godot compat)
        /// </summary>
        public event Action<string, int>? TransferScheduleStateChanged;

        public void EmitTransferScheduleStateChanged(string scheduleId, int newState)
        {
            TransferScheduleStateChanged?.Invoke(scheduleId, newState);
        }

        /// <summary>
        /// Fired when a continent's total transfer capacity changes (station built/destroyed).
        /// Parameters: continentIndex, newTotalCapacity
        /// </summary>
        public event Action<int, float>? ContinentTransferCapacityChanged;

        public void EmitContinentTransferCapacityChanged(int continentIndex, float newTotalCapacity)
        {
            ContinentTransferCapacityChanged?.Invoke(continentIndex, newTotalCapacity);
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
