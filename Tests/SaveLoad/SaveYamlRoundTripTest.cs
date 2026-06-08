using System.Collections.Generic;
using GdUnit4;
using UtilityLibrary.SaveLoad;
using UtilityLibrary.SaveLoad.Dto;
using UtilityLibrary.SaveLoad.Migrations;
using static GdUnit4.Assertions;

namespace Tests.SaveLoad;

/// <summary>
/// Verifies the save-file serialization contract: a fully populated SaveFileDto survives a
/// Serialize -> Deserialize round trip with all scalar and nested values intact. Pure YamlDotNet
/// (no Godot runtime / file IO) so it runs fast and deterministically.
/// </summary>
[TestSuite]
public class SaveYamlRoundTripTest
{
    private static SaveFileDto BuildSample()
    {
        return new SaveFileDto
        {
            Meta = new MetaDto
            {
                SaveVersion = SaveYaml.CurrentVersion,
                GameVersion = "test",
                CreatedUtc = "2026-05-25T00:00:00.0000000Z",
                TemplateName = "Sol",
            },
            Session = new SessionDto
            {
                SystemName = "Sol",
                CompanyName = "Acme",
                IsGameStarted = true,
                Barycenter = new BarycenterDto
                {
                    SystemName = "Sol",
                    SectorId = "KP-07",
                    Position = new Vec3Dto { X = 1, Y = 2, Z = 3 },
                    Velocity = new Vec3Dto { X = 0.1f, Y = 0.2f, Z = 0.3f },
                    Mass = 1000f,
                },
            },
            Company = new CompanyDto { Budget = 12345.67, Debt = 100.5, Antagonism = 2.5f, Research = 42.0 },
            Economy = new EconomyDto
            {
                PriceUpdateInterval = 5f,
                Market = new List<MarketEntryDto>
                {
                    new() { Id = "ore_iron", BasePrice = 20, CurrentPrice = 21.4 },
                    new() { Id = "ore_gold", BasePrice = 80, CurrentPrice = 79.2 },
                },
            },
            Bodies = new List<BodyDto>
            {
                new()
                {
                    Kind = "Celestial",
                    Name = "Terra",
                    Classification = "RockyPlanet",
                    Mass = 500f,
                    Radius = 8f,
                    BodySeed = "18446744073709551615", // ulong.MaxValue — verifies string round-trip
                    Atmosphere = 1.0f,
                    Position = new Vec3Dto { X = 10, Y = 0, Z = 0 },
                    Velocity = new Vec3Dto { X = 0, Y = 0, Z = 5 },
                    TotalForce = new Vec3Dto { X = 1, Y = 1, Z = 1 },
                    SavedForce = new Vec3Dto { X = 2, Y = 2, Z = 2 },
                    OrbitalParentName = null,
                    BandSatelliteCounts = new List<KvIntIntDto>
                    {
                        new() { Key = 0, Value = 3 },
                        new() { Key = 1, Value = 1 },
                    },
                    Geometry = new GeometryDto
                    {
                        Size = 8f,
                        MaxHeight = 2.5f,
                        GenerationType = "TectonicsOnly",
                        ProjectToSphere = true,
                        UseCellBiomeForColoring = true,
                        Classification = "RockyPlanet",
                        Points = new List<PointDto>
                        {
                            new()
                            {
                                Index = 0,
                                Position = new Vec3Dto { X = 1, Y = 0, Z = 0 },
                                Height = 0.5f,
                                Biome = "biome_grassland",
                                IsOnContinentBorder = true,
                                Radius = 8f,
                                ContinentIndices = new List<int> { 0, 1 },
                            },
                        },
                        Cells = new List<CellDto>
                        {
                            new()
                            {
                                Index = 7,
                                ContinentIndex = 0,
                                IsBorderTile = true,
                                Interiorness = 1,
                                BoundingContinentIndex = new[] { 1 },
                                Center = new Vec3Dto { X = 1, Y = 0, Z = 0 },
                                Height = 0.5f,
                                NormalizedHeight = 0.75f,
                                Biome = "biome_mountain",
                                HasGeothermalVent = true,
                                Resources = new Dictionary<string, float> { ["ore_iron"] = 0.4f },
                                PointIndices = new[] { 0, 0, 0 },
                            },
                        },
                        Continents = new List<ContinentDto>
                        {
                            new()
                            {
                                StartingIndex = 0,
                                CellIndices = new[] { 7 },
                                BoundaryCellIndices = new[] { 7 },
                                CrustType = "Oceanic",
                                AverageHeight = 0.3f,
                                NeighborStress = new List<KvIntFloatDto> { new() { Key = 1, Value = 0.9f } },
                                BoundaryTypes = new List<KvIntStringDto> { new() { Key = 1, Value = "Convergent" } },
                            },
                        },
                    },
                },
            },
        };
    }

    [TestCase]
    public void RoundTripPreservesAllFields()
    {
        var original = BuildSample();
        string yaml = SaveYaml.Serialize(original);
        AssertThat(yaml).IsNotEmpty();

        var restored = SaveYaml.Deserialize(yaml);
        AssertThat(restored).IsNotNull();

        // Meta / session
        AssertThat(restored.Meta.SaveVersion).IsEqual(SaveYaml.CurrentVersion);
        AssertThat(restored.Session.CompanyName).IsEqual("Acme");
        AssertThat(restored.Session.IsGameStarted).IsTrue();
        AssertThat(restored.Session.Barycenter.Mass).IsEqual(1000f);

        // Company
        AssertThat(restored.Company.Budget).IsEqual(12345.67);
        AssertThat(restored.Company.Research).IsEqual(42.0);

        // Economy (list order + values)
        AssertThat(restored.Economy.Market.Count).IsEqual(2);
        AssertThat(restored.Economy.Market[0].Id).IsEqual("ore_iron");
        AssertThat(restored.Economy.Market[0].CurrentPrice).IsEqual(21.4);

        // Body scalars + ulong-as-string seed
        AssertThat(restored.Bodies.Count).IsEqual(1);
        var body = restored.Bodies[0];
        AssertThat(body.Name).IsEqual("Terra");
        AssertThat(body.Classification).IsEqual("RockyPlanet");
        AssertThat(body.BodySeed).IsEqual("18446744073709551615");
        AssertThat(body.Position.Z).IsEqual(0f);
        AssertThat(body.Velocity.Z).IsEqual(5f);
        AssertThat(body.BandSatelliteCounts.Count).IsEqual(2);
        AssertThat(body.BandSatelliteCounts[0].Value).IsEqual(3);

        // Geometry
        var geo = body.Geometry;
        AssertThat(geo.GenerationType).IsEqual("TectonicsOnly");
        AssertThat(geo.Points.Count).IsEqual(1);
        AssertThat(geo.Points[0].ContinentIndices.Count).IsEqual(2);
        AssertThat(geo.Cells.Count).IsEqual(1);
        AssertThat(geo.Cells[0].Index).IsEqual(7);
        AssertThat(geo.Cells[0].NormalizedHeight).IsEqual(0.75f);
        AssertThat(geo.Cells[0].PointIndices.Length).IsEqual(3);
        AssertThat(geo.Cells[0].Resources["ore_iron"]).IsEqual(0.4f);
        AssertThat(geo.Continents.Count).IsEqual(1);
        AssertThat(geo.Continents[0].CrustType).IsEqual("Oceanic");
        AssertThat(geo.Continents[0].BoundaryTypes[0].Value).IsEqual("Convergent");
        AssertThat(geo.Continents[0].NeighborStress[0].Value).IsEqual(0.9f);
    }

    [TestCase]
    public void OmitNullKeepsOptionalCollectionsAbsent()
    {
        // SatelliteResources is null on a celestial body; OmitNull should drop it from the YAML.
        var dto = BuildSample();
        string yaml = SaveYaml.Serialize(dto);
        AssertThat(yaml.Contains("satellite_resources")).IsFalse();
    }

    // ========================================================================
    // PHASE 2 — LOGISTICS UNITS
    // ========================================================================

    private static LogisticsUnitDto BuildTransitUnit()
    {
        return new LogisticsUnitDto
        {
            Name = "Hauler-1",
            HostBodyName = null, // adrift mid-transfer
            State = "InTransit",
            IsActive = true,
            IsStationary = false,
            BandIndex = 2,
            Fuel = 640.5f,
            MaxFuel = 1000f,
            DryMass = 1200f,
            Position = new Vec3Dto { X = 30, Y = 0, Z = 12 },
            Velocity = new Vec3Dto { X = 1, Y = 0, Z = -2 },
            OrbitalAngle = 0.8f,
            OrbitalRadius = 25f,
            OrbitalSpeed = 0.002f,
            HostMass = 5e24f,
            Cargo = new Dictionary<string, int> { ["ore_iron"] = 40, ["ore_gold"] = 5 },
            Engine = new EngineDto
            {
                BaseSpecificImpulse = 320f,
                BaseThrust = 1500f,
                Modifiers = new List<EngineModifierDto>
                {
                    new() { Type = "Additive", Source = "Upgrade", IspValue = 20f, ThrustValue = 0f },
                    new() { Type = "Multiplicative", Source = "Damage", IspValue = 0.9f, ThrustValue = 0.9f },
                },
            },
            Transit = new TransitStateDto
            {
                DestinationBodyName = "Terra",
                OriginBodyName = "Luna",
                CentralBodyName = "Sol",
                TransferTime = 3600f,
                TimeInTransfer = 1234.5f,
                DeparturePosition = new Vec3Dto { X = 5, Y = 0, Z = 0 },
                TargetPosition = new Vec3Dto { X = 50, Y = 0, Z = 0 },
                DeparturePositionGlobal = new Vec3Dto { X = 6, Y = 0, Z = 1 },
                GravitationalParameter = 1.327e20f,
                CurrentTransitPhase = "Coasting",
                FuelConsumedThisTransfer = 88.2f,
                SimulationMode = "FullKepler",
                Trajectory = new TrajectoryDto
                {
                    InitialVelocity = new Vec3Dto { X = 3, Y = 0, Z = 1 },
                    FinalVelocity = new Vec3Dto { X = -2, Y = 0, Z = 1 },
                    TimeOfFlight = 3600f,
                    DeltaVRequired = 120f,
                    DepartureDeltaV = 70f,
                    ArrivalDeltaV = 50f,
                    SemiMajorAxis = 1.5e10f,
                    Eccentricity = 0.42f,
                    Inclination = 3.1f,
                    AscendingNodeLongitude = 12f,
                    ArgumentOfPeriapsis = 88f,
                    MeanAnomaly = 200f,
                    Revolutions = 0,
                    TransferType = "Direct",
                    GravitationalParameter = 1.327e20f,
                    DestinationBandIndex = 1,
                    OriginBandIndex = 2,
                    FuelRequired = 95f,
                },
                BurnProfile = new BurnProfileDto
                {
                    AccelBurnDuration = 100f,
                    CoastDuration = 3400f,
                    DecelBurnDuration = 100f,
                    TotalDuration = 3600f,
                    AccelFuelBudget = 50f,
                    DecelFuelBudget = 45f,
                    TotalFuelBudget = 95f,
                    AccelFuelRate = 0.5f,
                    DecelFuelRate = 0.45f,
                    AccelEndTime = 100f,
                    DecelStartTime = 3500f,
                },
            },
        };
    }

    [TestCase]
    public void LogisticsUnitRoundTripPreservesTransitState()
    {
        var dto = BuildSample();
        dto.LogisticsUnits = new List<LogisticsUnitDto> { BuildTransitUnit() };

        string yaml = SaveYaml.Serialize(dto);
        var restored = SaveYaml.Deserialize(yaml);

        AssertThat(restored.LogisticsUnits).IsNotNull();
        AssertThat(restored.LogisticsUnits!.Count).IsEqual(1);

        var u = restored.LogisticsUnits[0];
        AssertThat(u.Name).IsEqual("Hauler-1");
        AssertThat(u.State).IsEqual("InTransit");
        AssertThat(u.HostBodyName).IsNull();
        AssertThat(u.BandIndex).IsEqual(2);
        AssertThat(u.Fuel).IsEqual(640.5f);
        AssertThat(u.Cargo["ore_iron"]).IsEqual(40);

        // Engine + ordered modifiers
        AssertThat(u.Engine).IsNotNull();
        AssertThat(u.Engine!.BaseThrust).IsEqual(1500f);
        AssertThat(u.Engine.Modifiers.Count).IsEqual(2);
        AssertThat(u.Engine.Modifiers[0].Source).IsEqual("Upgrade");
        AssertThat(u.Engine.Modifiers[1].Type).IsEqual("Multiplicative");

        // Transit + trajectory + burn profile
        AssertThat(u.Transit).IsNotNull();
        AssertThat(u.Transit!.DestinationBodyName).IsEqual("Terra");
        AssertThat(u.Transit.TimeInTransfer).IsEqual(1234.5f);
        AssertThat(u.Transit.Trajectory.Eccentricity).IsEqual(0.42f);
        AssertThat(u.Transit.Trajectory.DestinationBandIndex).IsEqual(1);
        AssertThat(u.Transit.BurnProfile).IsNotNull();
        AssertThat(u.Transit.BurnProfile!.TotalFuelBudget).IsEqual(95f);
        AssertThat(u.Transit.BurnProfile.DecelStartTime).IsEqual(3500f);
    }

    [TestCase]
    public void OmitNullDropsTransitAndEngineWhenAbsent()
    {
        var dto = BuildSample();
        dto.LogisticsUnits = new List<LogisticsUnitDto>
        {
            new() { Name = "Idle-1", State = "Idle", HostBodyName = "Terra" },
        };

        string yaml = SaveYaml.Serialize(dto);
        AssertThat(yaml.Contains("transit:")).IsFalse();
        AssertThat(yaml.Contains("engine:")).IsFalse();
    }

    [TestCase]
    public void V1SaveMigratesToCurrentVersion()
    {
        // A v1 save predates logistics persistence: no logistics_units list.
        var dto = BuildSample();
        dto.Meta.SaveVersion = 1;
        dto.LogisticsUnits = null;

        bool ok = SaveMigrator.TryMigrate(dto, out var error);

        AssertThat(ok).IsTrue();
        AssertThat(error).IsNull();
        AssertThat(dto.Meta.SaveVersion).IsEqual(SaveYaml.CurrentVersion);
        AssertThat(dto.LogisticsUnits).IsNull();
    }

    // ========================================================================
    // PHASE 3 — BUILDINGS / STATIONS / TRANSFERS
    // ========================================================================

    private static BuildingDto BuildBuilding()
    {
        return new BuildingDto
        {
            Id = "bld-0001",
            DefinitionId = "smelter_tier1",
            BodyName = "Terra",
            PrimaryCellIndex = 7,
            AdditionalCellIndices = new List<int> { 8, 9 },
            PoweredOn = true,
            Specifier = 3,
            SpeedModifier = 1.5f,
            ActiveRecipeId = "recipe_iron_smelt",
            InputStorage = new Dictionary<string, int> { ["ore_iron"] = 60 },
            OutputStorage = new Dictionary<string, int> { ["metal_iron"] = 12 },
            BulkStorage = new Dictionary<string, int> { ["metal_iron"] = 200 },
            ExtractionSlots = new List<ExtractionSlotDto>
            {
                new() { Kind = "Primary", ResourceId = "iron_ore" },
                new() { Kind = "Primary", ResourceId = "copper_ore" },
                new() { Kind = "Secondary", ResourceId = null }, // explicitly cleared
            },
            Transfer = new TransferBehaviorStateDto
            {
                TotalTime = 512.25,
                ActiveTransfers = new List<TransferOrderDto>
                {
                    new()
                    {
                        OrderId = "ord-1",
                        OriginBuildingId = "bld-0001",
                        Destination = new TransferDestinationDto { StationSatelliteId = "stn-1" },
                        Manifest = new Dictionary<string, int> { ["metal_iron"] = 80 },
                        RequestedManifest = new Dictionary<string, int> { ["metal_iron"] = 100 },
                        State = "InTransit",
                        TravelTimeSeconds = 4f,
                        ElapsedTimeSeconds = 1.5f,
                        DispatchedAtTime = 500.0,
                        SourceScheduleId = "sch-1",
                    },
                },
                Schedules = new List<TransferScheduleDto>
                {
                    new()
                    {
                        ScheduleId = "sch-1",
                        OriginBuildingId = "bld-0001",
                        Destination = new TransferDestinationDto { StationSatelliteId = "stn-1" },
                        ResourceProportions = new Dictionary<string, float> { ["metal_iron"] = 1.0f },
                        DepartureMode = "AllResources",
                        Threshold = "Half",
                        State = "Dispatched",
                        ActiveTransferOrderId = "ord-1",
                        Priority = 1,
                        WaitSeconds = null,
                        LastDispatchTime = 500.0,
                    },
                },
            },
        };
    }

    private static StationDto BuildStation()
    {
        return new StationDto
        {
            Id = "stn-1",
            Name = "Orbital-Depot",
            DefinitionName = "DepotStation",
            BodyName = "Terra",
            BandIndex = 1,
            IsActive = true,
            IsStationary = true,
            OrbitalAngle = 0.4f,
            OrbitalRadius = 40f,
            OrbitalSpeed = 0.001f,
            HostMass = 5e24f,
            Position = new Vec3Dto { X = 40, Y = 0, Z = 0 },
            Velocity = new Vec3Dto { X = 0, Y = 0, Z = 4 },
            BulkStorage = new Dictionary<string, int> { ["metal_iron"] = 320 },
            Transfer = new TransferBehaviorStateDto
            {
                TotalTime = 77.0,
                ActiveTransfers = new List<TransferOrderDto>(),
                Schedules = new List<TransferScheduleDto>
                {
                    new()
                    {
                        ScheduleId = "sch-2",
                        OriginBuildingId = "stn-1",
                        Destination = new TransferDestinationDto { BuildingId = "bld-0001" },
                        ResourceProportions = new Dictionary<string, float> { ["metal_iron"] = 0.5f },
                        DepartureMode = "AnyResource",
                        Threshold = "Full",
                        State = "Accumulating",
                        ActiveTransferOrderId = null,
                        Priority = 2,
                        WaitSeconds = 30f,
                        LastDispatchTime = 0.0,
                    },
                },
            },
        };
    }

    [TestCase]
    public void BuildingRoundTripPreservesPlacementStorageAndTransfers()
    {
        var dto = BuildSample();
        dto.Buildings = new List<BuildingDto> { BuildBuilding() };

        string yaml = SaveYaml.Serialize(dto);
        var restored = SaveYaml.Deserialize(yaml);

        AssertThat(restored.Buildings).IsNotNull();
        AssertThat(restored.Buildings!.Count).IsEqual(1);

        var b = restored.Buildings[0];
        AssertThat(b.Id).IsEqual("bld-0001");
        AssertThat(b.DefinitionId).IsEqual("smelter_tier1");
        AssertThat(b.PrimaryCellIndex).IsEqual(7);
        AssertThat(b.AdditionalCellIndices.Count).IsEqual(2);
        AssertThat(b.Specifier).IsEqual(3);
        AssertThat(b.SpeedModifier).IsEqual(1.5f);
        AssertThat(b.ActiveRecipeId).IsEqual("recipe_iron_smelt");
        AssertThat(b.InputStorage["ore_iron"]).IsEqual(60);
        AssertThat(b.BulkStorage["metal_iron"]).IsEqual(200);

        AssertThat(b.Transfer).IsNotNull();
        AssertThat(b.Transfer!.TotalTime).IsEqual(512.25);
        AssertThat(b.Transfer.ActiveTransfers.Count).IsEqual(1);
        var ord = b.Transfer.ActiveTransfers[0];
        AssertThat(ord.OrderId).IsEqual("ord-1");
        AssertThat(ord.Destination.StationSatelliteId).IsEqual("stn-1");
        AssertThat(ord.Manifest["metal_iron"]).IsEqual(80);
        AssertThat(ord.State).IsEqual("InTransit");
        AssertThat(b.Transfer.Schedules[0].Threshold).IsEqual("Half");
        AssertThat(b.Transfer.Schedules[0].State).IsEqual("Dispatched");

        AssertThat(b.ExtractionSlots).IsNotNull();
        AssertThat(b.ExtractionSlots!.Count).IsEqual(3);
        AssertThat(b.ExtractionSlots[0].Kind).IsEqual("Primary");
        AssertThat(b.ExtractionSlots[0].ResourceId).IsEqual("iron_ore");
        AssertThat(b.ExtractionSlots[1].ResourceId).IsEqual("copper_ore");
        AssertThat(b.ExtractionSlots[2].Kind).IsEqual("Secondary");
        AssertThat(b.ExtractionSlots[2].ResourceId).IsNull();
    }

    [TestCase]
    public void StationRoundTripPreservesOrbitStorageAndSchedules()
    {
        var dto = BuildSample();
        dto.Stations = new List<StationDto> { BuildStation() };

        string yaml = SaveYaml.Serialize(dto);
        var restored = SaveYaml.Deserialize(yaml);

        AssertThat(restored.Stations).IsNotNull();
        AssertThat(restored.Stations!.Count).IsEqual(1);

        var s = restored.Stations[0];
        AssertThat(s.Id).IsEqual("stn-1");
        AssertThat(s.DefinitionName).IsEqual("DepotStation");
        AssertThat(s.BandIndex).IsEqual(1);
        AssertThat(s.OrbitalRadius).IsEqual(40f);
        AssertThat(s.HostMass).IsEqual(5e24f);
        AssertThat(s.BulkStorage["metal_iron"]).IsEqual(320);

        AssertThat(s.Transfer).IsNotNull();
        AssertThat(s.Transfer!.Schedules.Count).IsEqual(1);
        var sch = s.Transfer.Schedules[0];
        AssertThat(sch.ScheduleId).IsEqual("sch-2");
        AssertThat(sch.Destination.BuildingId).IsEqual("bld-0001");
        AssertThat(sch.DepartureMode).IsEqual("AnyResource");
        AssertThat(sch.WaitSeconds).IsEqual(30f);
        AssertThat(sch.ActiveTransferOrderId).IsNull();
    }

    [TestCase]
    public void OmitNullDropsStructureCollectionsWhenAbsent()
    {
        var dto = BuildSample();
        string yaml = SaveYaml.Serialize(dto);
        AssertThat(yaml.Contains("buildings:")).IsFalse();
        AssertThat(yaml.Contains("stations:")).IsFalse();
    }

    [TestCase]
    public void V2SaveMigratesToV3WithoutStructures()
    {
        // A v2 save predates structure persistence: no buildings/stations lists.
        var dto = BuildSample();
        dto.Meta.SaveVersion = 2;
        dto.Buildings = null;
        dto.Stations = null;

        bool ok = SaveMigrator.TryMigrate(dto, out var error);

        AssertThat(ok).IsTrue();
        AssertThat(error).IsNull();
        AssertThat(dto.Meta.SaveVersion).IsEqual(SaveYaml.CurrentVersion);
        AssertThat(dto.Buildings).IsNull();
        AssertThat(dto.Stations).IsNull();
    }
}
