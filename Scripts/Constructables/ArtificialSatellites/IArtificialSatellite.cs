using Godot;

namespace Constructables.ArtificialSatellites;

public interface IArtificialSatellite
{
    string Id { get; }
    int BandIndex { get; set; }
    bool IsActive { get; set; }
    void Initialize(Node3D parentBody, int bandIndex);
    void OnDestroy();
}
