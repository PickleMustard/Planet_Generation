using System.Collections.Generic;
using Constructables;

namespace UI.PlanetBoard.Testing;

public sealed partial class MockBuildingConstructionManager : BuildingConstructionManager
{
    private readonly List<Building> _mockActive = new();

    public override IReadOnlyList<Building> GetActiveBuildings() => _mockActive;

    public void AddTestBuilding(Building b) => _mockActive.Add(b);

    public void ClearTestBuildings() => _mockActive.Clear();
}
