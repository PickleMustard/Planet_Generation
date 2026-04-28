using System;
using System.Reflection;
using Constructables;
using Godot;
using Godot.Collections;
using ProceduralGeneration.PlanetGeneration;
using Structures.Enums;
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
        [Signal]
        public delegate void GenerateSystemRequestedEventHandler(
            Array<Dictionary> dominantBodies,
            Array<Dictionary> satelliteBelts,
            Array<Dictionary> planetaryBodies,
            Barycenter barycenter
        );

        public void EmitGenerateSystemRequested(
            Array<Dictionary> dominantBodies,
            Array<Dictionary> satelliteBelts,
            Array<Dictionary> planetaryBodies,
            Barycenter barycenter
        )
        {
            EmitSignal(
                SignalName.GenerateSystemRequested,
                dominantBodies,
                satelliteBelts,
                planetaryBodies,
                barycenter
            );
        }

        [Signal]
        public delegate void StartTimerEventHandler(
            string name,
            int totalSteps,
            int startingStep,
            string[] stepNames
        );

        [Signal]
        public delegate void IncrementTimerStepEventHandler(string name);

        [Signal]
        public delegate void StopTimerEventHandler(string name);

        [Signal]
        public delegate void QueuePackageEventHandler(WorkPackage package);

        [Signal]
        public delegate void SystemGenerationCompleteEventHandler(
            string batchId,
            int totalBodies,
            int successfulBodies
        );

        [Signal]
        public delegate void RequestRayCastEventHandler();

        [Signal]
        public delegate void ExportRaycastResultEventHandler(Dictionary results);

        public override void _Ready()
        {
            Instance = this;
        }

        public void EmitStartTimer(
            string name,
            int totalSteps,
            int startingStep,
            string[] stepNames
        )
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

        public void EmitSystemGenerationComplete(
            string batchId,
            int totalBodies,
            int successfulBodies
        )
        {
            EmitSignal(SignalName.SystemGenerationComplete, batchId, totalBodies, successfulBodies);
        }

        public void EmitRequestRayCast() => EmitSignal(SignalName.RequestRayCast);

        public void EmitExportRaycastResult(Dictionary results) =>
            EmitSignal(SignalName.ExportRaycastResult, results);

        /// <summary>
        /// Fired when a continent's power state changes (deficit or recovery).
        /// Parameters: continentIndex, isDeficit
        /// </summary>
        [Signal]
        public delegate void ContinentPowerStateChangedEventHandler(
            int continentIndex,
            bool isDeficit
        );

        public void EmitContinentPowerStateChanged(int continentIndex, bool isDeficit)
        {
            EmitSignal(SignalName.ContinentPowerStateChanged, continentIndex, isDeficit);
        }

        /// <summary>
        /// Fired when a continent enters or exits a resource shortage.
        /// Parameters: continentIndex, resourceId, isShortage
        /// </summary>
        [Signal]
        public delegate void ContinentResourceShortageEventHandler(
            int continentIndex,
            string resourceID,
            bool isShortage
        );

        public void EmitContinentResourceShortage(
            int continentIndex,
            string resourceId,
            bool isShortage
        )
        {
            EmitSignal(
                SignalName.ContinentResourceShortage,
                continentIndex,
                resourceId,
                isShortage
            );
        }

        /// <summary>
        /// Fired when a continent's economy ticks (production/consumption cycle completes).
        /// Parameters: continentIndex
        /// </summary>
        [Signal]
        public delegate void ContinentEconomyTickedEventHandler(int continentIndex);

        public void EmitContinentEconomyTicked(int continentIndex)
        {
            EmitSignal(SignalName.ContinentEconomyTicked, continentIndex);
        }

        /// <summary>
        /// Fired when a transfer is dispatched from a continent.
        /// Parameters: orderId, originContinentIndex
        /// </summary>
        [Signal]
        public delegate void TransferDispatchedEventHandler(
            string orderId,
            int originContinentIndex
        );

        public void EmitTransferDispatched(string orderId, int originContinentIndex)
        {
            EmitSignal(SignalName.TransferDispatched, orderId, originContinentIndex);
        }

        /// <summary>
        /// Fired when a transfer arrives at its destination.
        /// Parameters: orderId, fullyAccepted
        /// </summary>
        [Signal]
        public delegate void TransferArrivedEventHandler(string orderId, bool fullyAccepted);

        public void EmitTransferArrived(string orderId, bool fullyAccepted)
        {
            EmitSignal(SignalName.TransferArrived, orderId, fullyAccepted);
        }

        /// <summary>
        /// Fired when resources are reverted to origin due to destination rejection.
        /// Parameters: orderId, originContinentIndex, revertedAmount
        /// </summary>
        [Signal]
        public delegate void TransferRevertedEventHandler(
            string orderId,
            int originContinentIndex,
            float revertedAmount
        );

        public void EmitTransferReverted(
            string orderId,
            int originContinentIndex,
            float revertedAmount
        )
        {
            EmitSignal(SignalName.TransferReverted, orderId, originContinentIndex, revertedAmount);
        }

        /// <summary>
        /// Fired when a transfer schedule changes state.
        /// Parameters: scheduleId, newState (as int for Godot compat)
        /// </summary>
        [Signal]
        public delegate void TransferScheduleStateChangedEventHandler(
            string scheduleId,
            int newState
        );

        public void EmitTransferScheduleStateChanged(string scheduleId, int newState)
        {
            EmitSignal(SignalName.TransferScheduleStateChanged, scheduleId, newState);
        }

        /// <summary>
        /// Fired when a continent's total transfer capacity changes (station built/destroyed).
        /// Parameters: continentIndex, newTotalCapacity
        /// </summary>
        [Signal]
        public delegate void ContinentTransferCapacityChangedEventHandler(
            int continentIndex,
            float newTotalCapacity
        );

        public void EmitContinentTransferCapacityChanged(int continentIndex, float newTotalCapacity)
        {
            EmitSignal(
                SignalName.ContinentTransferCapacityChanged,
                continentIndex,
                newTotalCapacity
            );
        }

        // --- Orbital Logistics Signals ---

        /// <summary>
        /// Fired when a logistics unit begins executing a leg in its schedule.
        /// Parameters: unitId, legIndex
        /// </summary>
        [Signal]
        public delegate void OrbitalLegStartedEventHandler(string unitId, int legIndex);

        public void EmitOrbitalLegStarted(string unitId, int legIndex)
        {
            EmitSignal(SignalName.OrbitalLegStarted, unitId, legIndex);
        }

        /// <summary>
        /// Fired when a logistics unit completes a leg in its schedule.
        /// Parameters: unitId, legIndex
        /// </summary>
        [Signal]
        public delegate void OrbitalLegCompletedEventHandler(string unitId, int legIndex);

        public void EmitOrbitalLegCompleted(string unitId, int legIndex)
        {
            EmitSignal(SignalName.OrbitalLegCompleted, unitId, legIndex);
        }

        /// <summary>
        /// Fired when a leg fails (e.g., max trajectory retries exceeded).
        /// Parameters: unitId, legIndex, reason
        /// </summary>
        [Signal]
        public delegate void OrbitalLegFailedEventHandler(
            string unitId,
            int legIndex,
            string reason
        );

        public void EmitOrbitalLegFailed(string unitId, int legIndex, string reason)
        {
            EmitSignal(SignalName.OrbitalLegFailed, unitId, legIndex, reason);
        }

        /// <summary>
        /// Fired when an orbital transfer schedule changes state.
        /// Parameters: unitId, newState
        /// </summary>
        [Signal]
        public delegate void OrbitalScheduleStateChangedEventHandler(
            string unitId,
            string newState
        );

        public void EmitOrbitalScheduleStateChanged(string unitId, string newState)
        {
            EmitSignal(SignalName.OrbitalScheduleStateChanged, unitId, newState);
        }

        /// <summary>
        /// Fired when cargo loading or unloading completes at a station.
        /// Parameters: unitId, stationId, isLoading (true=pickup, false=dropoff)
        /// </summary>
        [Signal]
        public delegate void OrbitalCargoTransferCompletedEventHandler(
            string unitId,
            string stationId,
            bool isLoading
        );

        public void EmitOrbitalCargoTransferCompleted(
            string unitId,
            string stationId,
            bool isLoading
        )
        {
            EmitSignal(SignalName.OrbitalCargoTransferCompleted, unitId, stationId, isLoading);
        }

        /// <summary>
        /// Fired when trajectory planning is retried for a leg.
        /// Parameters: unitId, legIndex, attemptNumber, maxAttempts
        /// </summary>
        [Signal]
        public delegate void OrbitalTrajectoryRetryEventHandler(
            string unitId,
            int legIndex,
            int attemptNumber,
            int maxAttempts
        );

        public void EmitOrbitalTrajectoryRetry(
            string unitId,
            int legIndex,
            int attemptNumber,
            int maxAttempts
        )
        {
            EmitSignal(
                SignalName.OrbitalTrajectoryRetry,
                unitId,
                legIndex,
                attemptNumber,
                maxAttempts
            );
        }

        /// <summary>
        /// Fired when a building upgrade is requested from the Building Info Panel.
        /// Parameters: buildingInstanceId
        /// </summary>
        [Signal]
        public delegate void BuildingUpgradeRequestedEventHandler(int buildingInstanceId);

        /// <summary>
        /// Fired when a building demolish is requested from the Building Info Panel.
        /// Parameters: buildingInstanceId
        /// </summary>
        [Signal]
        public delegate void BuildingDemolishRequestedEventHandler(int buildingInstanceId);

        public void EmitBuildingUpgradeRequested(int buildingInstanceId) =>
            EmitSignal(SignalName.BuildingUpgradeRequested, buildingInstanceId);

        public void EmitBuildingDemolishRequested(int buildingInstanceId) =>
            EmitSignal(SignalName.BuildingDemolishRequested, buildingInstanceId);

        /// <summary>
        /// Fired when a station is upgraded (e.g., Ramshackle Builder).
        /// Parameters: StationSatellite (the upgraded station)
        /// </summary>
        [Signal]
        public delegate void StationUpgradedEventHandler(StationSatellite station);

        public void EmitStationUpgraded(StationSatellite station)
        {
            EmitSignal(SignalName.StationUpgraded, station);
        }

        /// <summary>
        /// Fired after a building finishes construction and is registered with its
        /// continent economy. Parameters: continentIndex.
        /// </summary>
        [Signal]
        public delegate void BuildingConstructedEventHandler(int continentIndex);

        public void EmitBuildingConstructed(int continentIndex) =>
            EmitSignal(SignalName.BuildingConstructed, continentIndex);

        /// <summary>
        /// Fired after a building is unregistered from its continent economy (demolish
        /// or cancel). Parameters: continentIndex.
        /// </summary>
        [Signal]
        public delegate void BuildingRemovedEventHandler(int continentIndex);

        public void EmitBuildingRemoved(int continentIndex) =>
            EmitSignal(SignalName.BuildingRemoved, continentIndex);

        /// <summary>
        /// Fired when an economy is registered with the debug console.
        /// Parameters: economyNamespace, economyType, parentName.
        /// </summary>
        [Signal]
        public delegate void EconomyRegisteredEventHandler(string economyNamespace, string economyType, string parentName);

        /// <summary>
        /// Fired when an economy is unregistered from the debug console.
        /// Parameters: economyNamespace.
        /// </summary>
        [Signal]
        public delegate void EconomyUnregisteredEventHandler(string economyNamespace);

        public void EmitEconomyRegistered(string economyNamespace, string economyType, string parentName)
        {
            EmitSignal(SignalName.EconomyRegistered, economyNamespace, economyType, parentName);
        }

        public void EmitEconomyUnregistered(string economyNamespace)
        {
            EmitSignal(SignalName.EconomyUnregistered, economyNamespace);
        }

        // --- Orbital Body Window Signals ---

        /// <summary>
        /// Fired when the orbital body inspection window opens.
        /// </summary>
        [Signal]
        public delegate void OrbitalBodyWindowOpenedEventHandler(Dictionary bodyInfo);

        public void EmitOrbitalBodyWindowOpened(Dictionary bodyInfo) =>
            EmitSignal(SignalName.OrbitalBodyWindowOpened, bodyInfo);

        /// <summary>
        /// Fired when the orbital body inspection window closes.
        /// </summary>
        [Signal]
        public delegate void OrbitalBodyWindowClosedEventHandler();

        public void EmitOrbitalBodyWindowClosed() => EmitSignal(SignalName.OrbitalBodyWindowClosed);

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
            if (target == null)
                return;

            var type = target.GetType();
            var methods = type.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

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
            if (target == null)
                return;

            var type = target.GetType();
            var methods = type.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

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
