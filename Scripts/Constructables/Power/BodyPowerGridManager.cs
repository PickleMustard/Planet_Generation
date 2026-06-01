using System.Collections.Generic;
using System.Linq;
using Constructables.Buildings.Behaviors;
using Constructables.Tick;
using Godot;
using ProceduralGeneration;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using UtilityLibrary;

namespace Constructables.Power;

/// <summary>
/// Per-<see cref="CelestialBody"/> registry of <see cref="PowerGrid"/> instances.
/// Translates building placement and removal events into grid create / join / merge /
/// split operations and registers grids with <see cref="ManufactureTickEngine"/>.
/// </summary>
public partial class BodyPowerGridManager : Node
{
    public CelestialBody? Body { get; set; }

    private readonly List<PowerGrid> _grids = new();
    private readonly Dictionary<VoronoiCell, PowerGrid> _gridByCell = new();
    private readonly Dictionary<Building, PowerGrid> _gridByBuilding = new();
    private int _nextGridId;

    public IReadOnlyList<PowerGrid> Grids => _grids;

    public PowerGrid? GetGridForCell(VoronoiCell cell) =>
        _gridByCell.TryGetValue(cell, out var g) ? g : null;

    public PowerGrid? GetGridForBuilding(Building b) =>
        _gridByBuilding.TryGetValue(b, out var g) ? g : null;

    /// <summary>
    /// Called at placement (during construction). Creates / joins / merges the grid for
    /// the building's coverage area so the grid exists from the moment the building is
    /// placed. The contributor's <see cref="PowerProducerBehavior.IsProducing"/> remains
    /// false while <see cref="Building.IsUnderConstruction"/> is true, so capacity stays
    /// at zero until <see cref="OnBuildingCompleted"/> fires.
    /// </summary>
    public void OnBuildingPlaced(Building building)
    {
        if (Body == null || building.PrimaryCell == null)
            return;

        var contributor = GetContributor(building);
        if (contributor != null)
        {
            HandleContributorPlaced(building, contributor);
            return;
        }

        var consumer = building.GetBehavior<PowerConsumerBehavior>();
        if (consumer != null)
        {
            HandleConsumerPlaced(building, consumer);
        }
    }

    /// <summary>
    /// Called when construction finishes. Forces an immediate supply/demand recompute on
    /// the building's grid so a freshly-completed producer flips brownout state and
    /// powers on its consumers without waiting for the next manufacture tick.
    /// </summary>
    public void OnBuildingCompleted(Building building)
    {
        if (!_gridByBuilding.TryGetValue(building, out var grid))
            return;
        grid.OnManufactureTick(0f);
    }

    public void OnBuildingRemoved(Building building)
    {
        if (!_gridByBuilding.TryGetValue(building, out var grid))
            return;

        _gridByBuilding.Remove(building);

        if (grid.Consumers.Remove(building))
        {
            var consumer = building.GetBehavior<PowerConsumerBehavior>();
            if (consumer != null)
                consumer.Grid = null;
            building.PoweredOn = false;
            return;
        }

        if (grid.Contributors.Remove(building))
        {
            HandleContributorRemoved(grid, building);
        }
    }

    private void HandleContributorPlaced(Building building, IGridContributor contributor)
    {
        var coverage = ComputeCoverage(building, contributor);
        var touched = new HashSet<PowerGrid>();
        foreach (var cell in coverage)
        {
            if (_gridByCell.TryGetValue(cell, out var existing))
                touched.Add(existing);
        }

        PowerGrid target;
        if (touched.Count == 0)
        {
            target = CreateGrid();
        }
        else if (touched.Count == 1)
        {
            target = touched.First();
        }
        else
        {
            // Merge: keep the largest by membership; absorb the rest. Stats survive on
            // the largest only — smaller grids' stats discarded per the design spec.
            target = touched
                .OrderByDescending(g => g.Contributors.Count + g.Consumers.Count)
                .First();
            foreach (var other in touched)
            {
                if (other == target)
                    continue;
                AbsorbAndDestroy(target, other);
            }
        }

        var newlyCovered = new HashSet<VoronoiCell>();
        foreach (var cell in coverage)
        {
            if (target.CoveredCells.Add(cell))
                newlyCovered.Add(cell);
            _gridByCell[cell] = target;
        }

        target.Contributors.Add(building);
        _gridByBuilding[building] = target;
        building.PoweredOn = !target.IsBrownedOut;

        AbsorbConsumersOverCells(target, newlyCovered);
    }

    private void HandleConsumerPlaced(Building building, PowerConsumerBehavior consumer)
    {
        if (building.PrimaryCell == null)
            return;
        if (_gridByCell.TryGetValue(building.PrimaryCell, out var grid))
        {
            grid.Consumers.Add(building);
            _gridByBuilding[building] = grid;
            consumer.Grid = grid;
            building.PoweredOn = !grid.IsBrownedOut;
        }
        else
        {
            consumer.Grid = null;
            building.PoweredOn = false;
        }
    }

    private void HandleContributorRemoved(PowerGrid grid, Building removed)
    {
        var newCoverage = new HashSet<VoronoiCell>();
        foreach (var c in grid.Contributors)
        {
            var contributor = GetContributor(c);
            if (contributor == null)
                continue;
            foreach (var cell in ComputeCoverage(c, contributor))
                newCoverage.Add(cell);
        }

        var orphanedCells = new List<VoronoiCell>();
        foreach (var cell in grid.CoveredCells)
        {
            if (!newCoverage.Contains(cell))
                orphanedCells.Add(cell);
        }
        foreach (var cell in orphanedCells)
        {
            grid.CoveredCells.Remove(cell);
            if (_gridByCell.TryGetValue(cell, out var owner) && owner == grid)
                _gridByCell.Remove(cell);
        }

        var orphanedConsumers = new List<Building>();
        foreach (var consumer in grid.Consumers)
        {
            if (consumer.PrimaryCell == null
                || !grid.CoveredCells.Contains(consumer.PrimaryCell))
            {
                orphanedConsumers.Add(consumer);
            }
        }
        foreach (var consumer in orphanedConsumers)
        {
            grid.Consumers.Remove(consumer);
            _gridByBuilding.Remove(consumer);
            var pc = consumer.GetBehavior<PowerConsumerBehavior>();
            if (pc != null)
                pc.Grid = null;
            consumer.PoweredOn = false;
        }

        if (grid.Contributors.Count == 0)
        {
            DestroyGrid(grid);
            return;
        }

        var components = ExtractContributorComponents(new HashSet<Building>(grid.Contributors));
        if (components.Count == 1)
            return;

        components.Sort((a, b) => b.Count.CompareTo(a.Count));
        var keep = components[0];

        // Rebuild kept grid from `keep` only.
        grid.Contributors.Clear();
        grid.CoveredCells.Clear();
        foreach (var cell in new List<VoronoiCell>(_gridByCell.Keys))
        {
            if (_gridByCell[cell] == grid)
                _gridByCell.Remove(cell);
        }
        var keepConsumers = new HashSet<Building>(grid.Consumers);
        grid.Consumers.Clear();

        foreach (var b in keep)
        {
            grid.Contributors.Add(b);
            _gridByBuilding[b] = grid;
            var contributor = GetContributor(b);
            if (contributor == null)
                continue;
            foreach (var cell in ComputeCoverage(b, contributor))
            {
                grid.CoveredCells.Add(cell);
                _gridByCell[cell] = grid;
            }
        }

        foreach (var consumer in keepConsumers)
        {
            if (consumer.PrimaryCell != null
                && grid.CoveredCells.Contains(consumer.PrimaryCell))
            {
                grid.Consumers.Add(consumer);
                _gridByBuilding[consumer] = grid;
                var pc = consumer.GetBehavior<PowerConsumerBehavior>();
                if (pc != null)
                    pc.Grid = grid;
            }
            else
            {
                _gridByBuilding.Remove(consumer);
                var pc = consumer.GetBehavior<PowerConsumerBehavior>();
                if (pc != null)
                    pc.Grid = null;
                consumer.PoweredOn = false;
            }
        }

        for (int i = 1; i < components.Count; i++)
        {
            var comp = components[i];
            var fresh = CreateGrid();
            foreach (var b in comp)
            {
                fresh.Contributors.Add(b);
                _gridByBuilding[b] = fresh;
                var contributor = GetContributor(b);
                if (contributor == null)
                    continue;
                foreach (var cell in ComputeCoverage(b, contributor))
                {
                    fresh.CoveredCells.Add(cell);
                    _gridByCell[cell] = fresh;
                }
            }
            AbsorbConsumersOverCells(fresh, fresh.CoveredCells);
        }
    }

    private List<HashSet<Building>> ExtractContributorComponents(HashSet<Building> pool)
    {
        var components = new List<HashSet<Building>>();
        var contributorCells = new Dictionary<Building, HashSet<VoronoiCell>>();
        foreach (var b in pool)
        {
            var contributor = GetContributor(b);
            if (contributor == null)
                continue;
            contributorCells[b] = ComputeCoverage(b, contributor);
        }

        var unvisited = new HashSet<Building>(pool);
        while (unvisited.Count > 0)
        {
            var seed = unvisited.First();
            var comp = new HashSet<Building> { seed };
            unvisited.Remove(seed);

            var queue = new Queue<Building>();
            queue.Enqueue(seed);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!contributorCells.TryGetValue(current, out var currCells))
                    continue;
                foreach (var other in new List<Building>(unvisited))
                {
                    if (!contributorCells.TryGetValue(other, out var otherCells))
                        continue;
                    if (currCells.Overlaps(otherCells))
                    {
                        comp.Add(other);
                        unvisited.Remove(other);
                        queue.Enqueue(other);
                    }
                }
            }
            components.Add(comp);
        }
        return components;
    }

    private void AbsorbConsumersOverCells(PowerGrid target, IEnumerable<VoronoiCell> cells)
    {
        foreach (var cell in cells)
        {
            var occupant = cell.Building;
            if (occupant == null)
                continue;
            var consumer = occupant.GetBehavior<PowerConsumerBehavior>();
            if (consumer == null)
                continue;
            if (_gridByBuilding.TryGetValue(occupant, out var existing))
            {
                if (existing == target)
                    continue;
                continue; // already in another grid; merge logic should have run first
            }

            target.Consumers.Add(occupant);
            _gridByBuilding[occupant] = target;
            consumer.Grid = target;
            occupant.PoweredOn = !target.IsBrownedOut;
        }
    }

    private HashSet<VoronoiCell> ComputeCoverage(Building building, IGridContributor contributor) =>
        ComputeCoverageCore(building.OccupiedCells, contributor.Radius);

    /// <summary>
    /// BFS over the body's cell adjacency graph that grows outward from
    /// <paramref name="seedCells"/> for <c>radius + 1</c> hops. Used by both the
    /// canonical contributor-placement path and the placement-preview UI.
    /// </summary>
    public HashSet<VoronoiCell> ComputeCoverageCore(
        IEnumerable<VoronoiCell> seedCells,
        int radius
    )
    {
        var coverage = new HashSet<VoronoiCell>();
        if (Body == null)
            return coverage;

        int hops = radius + 1;
        if (hops < 1)
            hops = 1;

        var frontier = new HashSet<VoronoiCell>(seedCells);
        if (frontier.Count == 0)
            return coverage;
        foreach (var c in frontier)
            coverage.Add(c);

        for (int hop = 0; hop < hops; hop++)
        {
            var next = new HashSet<VoronoiCell>();
            foreach (var c in frontier)
            {
                foreach (var n in Body.GetRuntimeCellNeighbors(c, includeSameContinent: true))
                {
                    if (coverage.Add(n))
                        next.Add(n);
                }
            }
            if (next.Count == 0)
                break;
            frontier = next;
        }
        return coverage;
    }

    /// <summary>
    /// Read-only preview of what would happen if a contributor with the given seed
    /// cells and radius were placed. Returns the merged-coverage cells, the
    /// dominant grid (largest by membership) and any grids that would be absorbed.
    /// Does not mutate manager state.
    /// </summary>
    public sealed record GridPreview(
        PowerGrid? Dominant,
        IReadOnlyList<PowerGrid> Absorbed,
        IReadOnlyCollection<VoronoiCell> Coverage
    );

    public GridPreview PreviewPlacement(IEnumerable<VoronoiCell> seedCells, int radius)
    {
        var coverage = ComputeCoverageCore(seedCells, radius);
        var touched = new HashSet<PowerGrid>();
        foreach (var cell in coverage)
        {
            if (_gridByCell.TryGetValue(cell, out var existing))
                touched.Add(existing);
        }

        if (touched.Count == 0)
            return new GridPreview(null, System.Array.Empty<PowerGrid>(), coverage);

        PowerGrid dominant = touched
            .OrderByDescending(g => g.Contributors.Count + g.Consumers.Count)
            .First();
        var absorbed = new List<PowerGrid>(touched.Count - 1);
        foreach (var g in touched)
        {
            if (g != dominant)
                absorbed.Add(g);
        }
        return new GridPreview(dominant, absorbed, coverage);
    }

    private static IGridContributor? GetContributor(Building b)
    {
        var producer = b.GetBehavior<PowerProducerBehavior>();
        if (producer != null)
            return producer;
        var battery = b.GetBehavior<BatteryBehavior>();
        if (battery != null)
            return battery;
        return null;
    }

    private PowerGrid CreateGrid()
    {
        var grid = new PowerGrid(_nextGridId++, Body!);
        _grids.Add(grid);
        ManufactureTickEngine.Instance?.Register(grid);
        return grid;
    }

    private void DestroyGrid(PowerGrid grid)
    {
        ManufactureTickEngine.Instance?.Unregister(grid);
        foreach (var cell in grid.CoveredCells)
        {
            if (_gridByCell.TryGetValue(cell, out var owner) && owner == grid)
                _gridByCell.Remove(cell);
        }
        _grids.Remove(grid);
    }

    private void AbsorbAndDestroy(PowerGrid keeper, PowerGrid victim)
    {
        keeper.AbsorbFrom(victim);
        foreach (var b in keeper.Contributors)
            _gridByBuilding[b] = keeper;
        foreach (var b in keeper.Consumers)
        {
            _gridByBuilding[b] = keeper;
            var pc = b.GetBehavior<PowerConsumerBehavior>();
            if (pc != null)
                pc.Grid = keeper;
        }
        foreach (var cell in keeper.CoveredCells)
            _gridByCell[cell] = keeper;
        DestroyGrid(victim);
    }
}
