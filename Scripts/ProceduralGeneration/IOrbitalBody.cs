using Godot;
using Structures.GameState;
using Constructables.ArtificialSatellites;

public interface IOrbitalBody
{
    // Physical properties
    public float Radius { get; set; }
    public float Mass { get; set; }

    // Orbital Band System - Properties
    public Godot.Collections.Array<OrbitBand> OrbitBands { get; }
    public OrbitConfiguration? OrbitConfig { get; }
    public Node3D SatellitesContainer { get; }

    // Orbital Band System - Methods
    public void InitializeOrbitSystem();
    public int GetBandCount();
    public bool CanAddToBand(int bandIndex);
    public int GetBandSatelliteCount(int bandIndex);
    public StationSatellite? CreateStation(int bandIndex, string name);
    public StationSatellite? CreateShip(string name);
}
