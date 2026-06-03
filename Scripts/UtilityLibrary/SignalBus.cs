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
    /// <summary>
    /// Global signal dispatcher. Any signal emitted from <c>IManufactureTickable.OnManufactureTick</c>
    /// (or any other code that runs on the <c>ManufactureTickEngine</c> background thread) MUST
    /// use the <c>SafeEmit*</c> companion of the corresponding <c>Emit*</c> method — Godot
    /// rejects signal emission off the main thread. When adding a new signal that may be
    /// emitted from tick code, also add a <c>SafeEmit*</c> companion.
    /// </summary>
    public partial class SignalBus : Node
    {
        public static SignalBus? Instance { get; private set; }

        [System.Diagnostics.Conditional("DEBUG")]
        private static void WarnOffMain(string emitterName)
        {
            if (!SignalMarshal.IsOnMainThread)
                GameLogger.Warning(
                    $"[SignalBus] {emitterName} called off-main; use Safe{emitterName}"
                );
        }

        /// <summary>
        /// Selected system template filename to be loaded by the next GameScene.
        /// Set by MainMenu before scene transition, consumed by GameScene on _Ready().
        /// </summary>
        public string? SelectedTemplate { get; set; }

        /// <summary>
        /// Path to a save file (user://...) the next LoadingScreen should load instead of generating
        /// a fresh system. Set by MainMenu's Load entry, consumed and cleared by LoadingScreen.
        /// </summary>
        public string? SaveToLoadPath { get; set; }

        /// <summary>
        /// C# event for requesting system generation. Used instead of a Godot signal
        /// because the parameters include Barycenter (a Node3D subclass).
        /// </summary>
        [Signal]
        public delegate void GenerateSystemRequestedEventHandler(
            Array<Dictionary> dominantBodies,
            Array<Dictionary> satelliteBelts,
            Array<Dictionary> planetaryBodies,
            Array<Dictionary> satelliteBodies,
            Barycenter barycenter
        );

        public void EmitGenerateSystemRequested(
            Array<Dictionary> dominantBodies,
            Array<Dictionary> satelliteBelts,
            Array<Dictionary> planetaryBodies,
            Array<Dictionary> satelliteBodies,
            Barycenter barycenter
        )
        {
            EmitSignal(
                SignalName.GenerateSystemRequested,
                dominantBodies,
                satelliteBelts,
                planetaryBodies,
                satelliteBodies,
                barycenter
            );
        }

        // ---------------------------------------------------------------------
        // CompanyDataTracker signals — company-wide financial / standing state.
        // All emitted on the main thread (player actions); no SafeEmit needed.
        // ---------------------------------------------------------------------

        [Signal]
        public delegate void CompanyBudgetChangedEventHandler(double newBudget);

        public void EmitCompanyBudgetChanged(double newBudget) =>
            EmitSignal(SignalName.CompanyBudgetChanged, newBudget);

        [Signal]
        public delegate void CompanyDebtChangedEventHandler(double newDebt);

        public void EmitCompanyDebtChanged(double newDebt) =>
            EmitSignal(SignalName.CompanyDebtChanged, newDebt);

        [Signal]
        public delegate void CompanyAntagonismChangedEventHandler(float newAntagonism);

        public void EmitCompanyAntagonismChanged(float newAntagonism) =>
            EmitSignal(SignalName.CompanyAntagonismChanged, newAntagonism);

        [Signal]
        public delegate void CompanyResearchChangedEventHandler(double newResearch);

        public void EmitCompanyResearchChanged(double newResearch) =>
            EmitSignal(SignalName.CompanyResearchChanged, newResearch);

        // ---------------------------------------------------------------------
        // EconomyTracker signals — galactic market orders and price movement.
        // Orders are player-driven and the price loop runs on a main-thread
        // Godot Timer, so all of these are main-thread emits (no SafeEmit).
        // ---------------------------------------------------------------------

        [Signal]
        public delegate void MarketOrderFilledEventHandler(
            string resourceId,
            int quantity,
            double totalAmount,
            bool isBuy
        );

        public void EmitMarketOrderFilled(
            string resourceId,
            int quantity,
            double totalAmount,
            bool isBuy
        ) => EmitSignal(SignalName.MarketOrderFilled, resourceId, quantity, totalAmount, isBuy);

        [Signal]
        public delegate void MarketOrderRejectedEventHandler(
            string resourceId,
            int quantity,
            string reason
        );

        public void EmitMarketOrderRejected(string resourceId, int quantity, string reason) =>
            EmitSignal(SignalName.MarketOrderRejected, resourceId, quantity, reason);

        [Signal]
        public delegate void MarketPriceChangedEventHandler(string resourceId, double newPrice);

        public void EmitMarketPriceChanged(string resourceId, double newPrice) =>
            EmitSignal(SignalName.MarketPriceChanged, resourceId, newPrice);

        [Signal]
        public delegate void MarketPricesUpdatedEventHandler();

        public void EmitMarketPricesUpdated() => EmitSignal(SignalName.MarketPricesUpdated);

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
            SignalMarshal.Initialize();
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
        /// Fired after a developer editor's Save reloads a runtime database in memory.
        /// UI panels showing config-derived lists should subscribe and refresh.
        /// Parameter: databaseName (matches <c>ILoadableDatabase.DatabaseName</c>).
        /// </summary>
        [Signal]
        public delegate void DatabaseReloadedEventHandler(string databaseName);

        public void EmitDatabaseReloaded(string databaseName) =>
            EmitSignal(SignalName.DatabaseReloaded, databaseName);

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
            WarnOffMain(nameof(EmitTransferDispatched));
            EmitSignal(SignalName.TransferDispatched, orderId, originContinentIndex);
        }

        /// <summary>
        /// Thread-safe variant of <see cref="EmitTransferDispatched"/>. Use from any code
        /// reachable from <c>ManufactureTickEngine</c>.
        /// </summary>
        public void SafeEmitTransferDispatched(string orderId, int originContinentIndex)
        {
            if (SignalMarshal.IsOnMainThread)
                EmitSignal(SignalName.TransferDispatched, orderId, originContinentIndex);
            else
                CallDeferred(
                    MethodName.EmitTransferDispatched,
                    orderId,
                    originContinentIndex
                );
        }

        /// <summary>
        /// Fired when a transfer arrives at its destination.
        /// Parameters: orderId, fullyAccepted
        /// </summary>
        [Signal]
        public delegate void TransferArrivedEventHandler(string orderId, bool fullyAccepted);

        public void EmitTransferArrived(string orderId, bool fullyAccepted)
        {
            WarnOffMain(nameof(EmitTransferArrived));
            EmitSignal(SignalName.TransferArrived, orderId, fullyAccepted);
        }

        /// <summary>
        /// Thread-safe variant of <see cref="EmitTransferArrived"/>.
        /// </summary>
        public void SafeEmitTransferArrived(string orderId, bool fullyAccepted)
        {
            if (SignalMarshal.IsOnMainThread)
                EmitSignal(SignalName.TransferArrived, orderId, fullyAccepted);
            else
                CallDeferred(MethodName.EmitTransferArrived, orderId, fullyAccepted);
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
            WarnOffMain(nameof(EmitTransferReverted));
            EmitSignal(SignalName.TransferReverted, orderId, originContinentIndex, revertedAmount);
        }

        /// <summary>
        /// Thread-safe variant of <see cref="EmitTransferReverted"/>.
        /// </summary>
        public void SafeEmitTransferReverted(
            string orderId,
            int originContinentIndex,
            float revertedAmount
        )
        {
            if (SignalMarshal.IsOnMainThread)
                EmitSignal(
                    SignalName.TransferReverted,
                    orderId,
                    originContinentIndex,
                    revertedAmount
                );
            else
                CallDeferred(
                    MethodName.EmitTransferReverted,
                    orderId,
                    originContinentIndex,
                    revertedAmount
                );
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
            WarnOffMain(nameof(EmitTransferScheduleStateChanged));
            EmitSignal(SignalName.TransferScheduleStateChanged, scheduleId, newState);
        }

        /// <summary>
        /// Thread-safe variant of <see cref="EmitTransferScheduleStateChanged"/>.
        /// </summary>
        public void SafeEmitTransferScheduleStateChanged(string scheduleId, int newState)
        {
            if (SignalMarshal.IsOnMainThread)
                EmitSignal(SignalName.TransferScheduleStateChanged, scheduleId, newState);
            else
                CallDeferred(
                    MethodName.EmitTransferScheduleStateChanged,
                    scheduleId,
                    newState
                );
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
        /// Fired after a station finishes construction and registers as a transfer endpoint.
        /// Parameters: StationSatellite (the newly active station).
        /// </summary>
        [Signal]
        public delegate void StationConstructedEventHandler(StationSatellite station);

        public void EmitStationConstructed(StationSatellite station)
        {
            WarnOffMain(nameof(EmitStationConstructed));
            EmitSignal(SignalName.StationConstructed, station);
        }

        /// <summary>
        /// Thread-safe variant of <see cref="EmitStationConstructed"/>.
        /// </summary>
        public void SafeEmitStationConstructed(StationSatellite station)
        {
            if (SignalMarshal.IsOnMainThread)
                EmitSignal(SignalName.StationConstructed, station);
            else
                CallDeferred(MethodName.EmitStationConstructed, station);
        }

        /// <summary>
        /// Fired when a station begins construction (added to its body and placed in orbit,
        /// before construction completes). Parameters: StationSatellite (the new station).
        /// </summary>
        [Signal]
        public delegate void StationConstructionStartedEventHandler(StationSatellite station);

        public void EmitStationConstructionStarted(StationSatellite station)
        {
            WarnOffMain(nameof(EmitStationConstructionStarted));
            EmitSignal(SignalName.StationConstructionStarted, station);
        }

        /// <summary>
        /// Thread-safe variant of <see cref="EmitStationConstructionStarted"/>.
        /// </summary>
        public void SafeEmitStationConstructionStarted(StationSatellite station)
        {
            if (SignalMarshal.IsOnMainThread)
                EmitSignal(SignalName.StationConstructionStarted, station);
            else
                CallDeferred(MethodName.EmitStationConstructionStarted, station);
        }

        /// <summary>
        /// Fired right before a station exits the scene tree and unregisters its transfer endpoint.
        /// Parameters: StationSatellite (the station being removed).
        /// </summary>
        [Signal]
        public delegate void StationRemovedEventHandler(StationSatellite station);

        public void EmitStationRemoved(StationSatellite station) =>
            EmitSignal(SignalName.StationRemoved, station);

        // --- Ship Construction Signals ---
        // These replace ConstructionManager's direct ship signals.  Reachable from
        // ManufactureTickEngine, so every Emit has a SafeEmit companion.

        /// <summary>
        /// Fired when a ship begins construction at a shipyard station.
        /// Parameters: LogisticsUnit (the ship starting construction).
        /// </summary>
        [Signal]
        public delegate void ShipConstructionStartedEventHandler(LogisticsUnit ship);

        public void EmitShipConstructionStarted(LogisticsUnit ship)
        {
            WarnOffMain(nameof(EmitShipConstructionStarted));
            EmitSignal(SignalName.ShipConstructionStarted, ship);
        }

        /// <summary>
        /// Thread-safe variant of <see cref="EmitShipConstructionStarted"/>.
        /// </summary>
        public void SafeEmitShipConstructionStarted(LogisticsUnit ship)
        {
            if (SignalMarshal.IsOnMainThread)
                EmitSignal(SignalName.ShipConstructionStarted, ship);
            else
                CallDeferred(MethodName.EmitShipConstructionStarted, ship);
        }

        /// <summary>
        /// Fired when a ship finishes construction at a shipyard station and is activated.
        /// Parameters: LogisticsUnit (the completed ship).
        /// </summary>
        [Signal]
        public delegate void ShipConstructedEventHandler(LogisticsUnit ship);

        public void EmitShipConstructed(LogisticsUnit ship)
        {
            WarnOffMain(nameof(EmitShipConstructed));
            EmitSignal(SignalName.ShipConstructed, ship);
        }

        /// <summary>
        /// Thread-safe variant of <see cref="EmitShipConstructed"/>.
        /// </summary>
        public void SafeEmitShipConstructed(LogisticsUnit ship)
        {
            if (SignalMarshal.IsOnMainThread)
                EmitSignal(SignalName.ShipConstructed, ship);
            else
                CallDeferred(MethodName.EmitShipConstructed, ship);
        }

        /// <summary>
        /// Fired when a ship's construction is cancelled at a shipyard station.
        /// Parameters: LogisticsUnit (the cancelled ship).
        /// </summary>
        [Signal]
        public delegate void ShipConstructionCancelledEventHandler(LogisticsUnit ship);

        public void EmitShipConstructionCancelled(LogisticsUnit ship)
        {
            WarnOffMain(nameof(EmitShipConstructionCancelled));
            EmitSignal(SignalName.ShipConstructionCancelled, ship);
        }

        /// <summary>
        /// Thread-safe variant of <see cref="EmitShipConstructionCancelled"/>.
        /// </summary>
        public void SafeEmitShipConstructionCancelled(LogisticsUnit ship)
        {
            if (SignalMarshal.IsOnMainThread)
                EmitSignal(SignalName.ShipConstructionCancelled, ship);
            else
                CallDeferred(MethodName.EmitShipConstructionCancelled, ship);
        }

        /// <summary>
        /// Fired periodically during ship construction to report progress.
        /// Parameters: shipName, progress (0–1), status display string.
        /// Reachable from <c>ManufactureTickEngine</c> — always use
        /// <see cref="SafeEmitShipConstructionProgress"/>.
        /// </summary>
        [Signal]
        public delegate void ShipConstructionProgressUpdatedEventHandler(
            string shipName,
            float progress,
            string status
        );

        public void EmitShipConstructionProgressUpdated(string shipName, float progress, string status)
        {
            WarnOffMain(nameof(EmitShipConstructionProgressUpdated));
            EmitSignal(SignalName.ShipConstructionProgressUpdated, shipName, progress, status);
        }

        /// <summary>
        /// Thread-safe variant of <see cref="EmitShipConstructionProgressUpdated"/>.
        /// Use from any code reachable from <c>ManufactureTickEngine</c>.
        /// </summary>
        public void SafeEmitShipConstructionProgress(string shipName, float progress, string status)
        {
            if (SignalMarshal.IsOnMainThread)
                EmitSignal(SignalName.ShipConstructionProgressUpdated, shipName, progress, status);
            else
                CallDeferred(MethodName.EmitShipConstructionProgressUpdated, shipName, progress, status);
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
        /// Fired when a <see cref="Structures.Logistics.ResourceLink"/> is created or
        /// removed. PlanetBoard and any other interested UI rebuild their link views
        /// in response. No payload — listeners refresh from current state.
        /// </summary>
        [Signal]
        public delegate void ResourceLinkChangedEventHandler();

        public void EmitResourceLinkChanged() =>
            EmitSignal(SignalName.ResourceLinkChanged);

        /// <summary>
        /// Fired when a UI component requests the PlanetBoard window be opened on a
        /// specific body in a specific mode (e.g. "ResourceLink", "TransferRoute",
        /// "Overview"). MainGameUI listens and routes the request to the board.
        /// </summary>
        [Signal]
        public delegate void OpenPlanetBoardRequestedEventHandler(
            ProceduralGeneration.PlanetGeneration.CelestialBody body,
            string mode);

        public void EmitOpenPlanetBoardRequested(
            ProceduralGeneration.PlanetGeneration.CelestialBody body,
            string mode) =>
            EmitSignal(SignalName.OpenPlanetBoardRequested, body, mode);

        /// <summary>
        /// Fired when the PlanetBoard close button or backdrop is clicked.
        /// GUIControllerHSM listens and dispatches "window_closed" (→ HUD).
        /// </summary>
        [Signal]
        public delegate void PlanetBoardCloseRequestedEventHandler();

        public void EmitPlanetBoardCloseRequested() =>
            EmitSignal(SignalName.PlanetBoardCloseRequested);

        /// <summary>
        /// Fired when the PlanetBoard back button is clicked.
        /// GUIControllerHSM listens, pops the InteractionStack, and dispatches
        /// the return event to navigate back to the previous panel.
        /// </summary>
        [Signal]
        public delegate void PlanetBoardBackRequestedEventHandler();

        public void EmitPlanetBoardBackRequested() =>
            EmitSignal(SignalName.PlanetBoardBackRequested);

        /// <summary>
        /// Fired exactly once per game instance, when the player places the building
        /// that carries <c>GameStartBehavior</c> (currently the company headquarters).
        /// This is the commit point for the procedurally generated system: subscribers
        /// (e.g. lazy serialization) should treat this as "the player chose to keep
        /// this generated world." Parameters: the placed Building.
        /// </summary>
        [Signal]
        public delegate void GameStartedEventHandler(Building hq);

        public void EmitGameStarted(Building hq) =>
            EmitSignal(SignalName.GameStarted, hq);

        // --- Station Sidebar Signals ---

        /// <summary>
        /// Fired when a station viewer panel requests a sidebar be shown.
        /// Carries a label (sidebar title) and an array of item dictionaries.
        /// Each dictionary should contain at minimum "name" and "id" keys;
        /// additional keys are panel-specific (e.g. "mass", "time", "resources").
        /// </summary>
        [Signal]
        public delegate void StationSidebarRequestedEventHandler(
            string label,
            Godot.Collections.Array<Dictionary> items
        );

        public void EmitStationSidebarRequested(string label, Godot.Collections.Array<Dictionary> items)
        {
            EmitSignal(SignalName.StationSidebarRequested, label, items);
        }

        /// <summary>
        /// Fired when the station sidebar is dismissed (user closes it or panel
        /// deactivates it). No payload — listeners simply hide the sidebar.
        /// </summary>
        [Signal]
        public delegate void StationSidebarDismissedEventHandler();

        public void EmitStationSidebarDismissed()
        {
            EmitSignal(SignalName.StationSidebarDismissed);
        }

        /// <summary>
        /// Fired when the user selects an item in the station sidebar.
        /// Carries the "id" value from the selected item dictionary, allowing
        /// the originating panel to act on the selection.
        /// </summary>
        [Signal]
        public delegate void StationSidebarItemSelectedEventHandler(string itemId);

        public void EmitStationSidebarItemSelected(string itemId)
        {
            EmitSignal(SignalName.StationSidebarItemSelected, itemId);
        }

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
