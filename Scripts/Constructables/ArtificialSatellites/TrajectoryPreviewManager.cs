using System.Collections.Generic;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Logistics;
using UtilityLibrary;
using UtilityLibrary.GameMath.Orbital;

namespace Constructables.ArtificialSatellites;

/// <summary>
/// Manages trajectory preview visualization. When a route is selected, pauses the simulation
/// and renders ghost meshes showing predicted celestial body positions at arrival time,
/// the transfer arc, and departure/arrival markers. Cleans up on accept or cancel.
/// </summary>
public partial class TrajectoryPreviewManager : Node
{
    private const int ARC_SAMPLE_COUNT = 128;
    private const float GHOST_BODY_ALPHA = 0.3f;
    private const float ARC_ALPHA = 1.0f;
    private const float MARKER_ALPHA = 1.0f;
    private const float MARKER_SIZE = 5f;

    private static readonly Color ArcColor = new(0.2f, 0.8f, 1.0f, ARC_ALPHA);
    private static readonly Color DepartureMarkerColor = new(0.2f, 1.0f, 0.3f, MARKER_ALPHA);
    private static readonly Color ArrivalMarkerColor = new(1.0f, 0.9f, 0.2f, MARKER_ALPHA);
    private static readonly Color GhostBodyColor = new(0.6f, 0.7f, 0.8f, GHOST_BODY_ALPHA);

    private bool _isPreviewActive;
    private TrajectorySolution? _previewedTrajectory;
    private Node3D? _previewContainer;

    /// <summary>Whether a trajectory preview is currently being displayed.</summary>
    public bool IsPreviewActive => _isPreviewActive;

    /// <summary>
    /// Shows a trajectory preview: pauses the simulation, creates ghost meshes for
    /// predicted body positions at arrival time, draws the transfer arc, and places
    /// departure/arrival markers.
    /// </summary>
    /// <param name="trajectory">The selected trajectory solution to preview.</param>
    /// <param name="unit">The logistics unit that will execute this trajectory.</param>
    public void ShowPreview(TrajectorySolution trajectory, LogisticsUnit unit)
    {
        if (trajectory == null || unit == null)
        {
            GameLogger.Warning(
                "TrajectoryPreviewManager: Cannot show preview - null trajectory or unit"
            );
            return;
        }

        if (_isPreviewActive)
        {
            ClearPreview();
        }

        _previewedTrajectory = trajectory;

        // Pause simulation
        PauseSimulation();

        // Create container for all preview geometry
        _previewContainer = new Node3D { Name = "TrajectoryPreviewContainer" };
        GetTree().Root.AddChild(_previewContainer);

        // Generate preview elements
        CreateGhostBodies(trajectory);
        CreateTransferArc(trajectory);
        CreateShipMarkers(trajectory, unit);

        _isPreviewActive = true;

        GameLogger.Info(
            $"TrajectoryPreviewManager: Preview shown — TOF: {trajectory.TimeOfFlight:F1}s, "
                + $"ΔV: {trajectory.DeltaVRequired:F2} m/s"
        );
    }

    /// <summary>
    /// Clears the trajectory preview, removes all ghost meshes, and resumes the simulation.
    /// </summary>
    public void ClearPreview()
    {
        if (!_isPreviewActive)
            return;

        if (_previewContainer != null && IsInstanceValid(_previewContainer))
        {
            _previewContainer.QueueFree();
            _previewContainer = null;
        }

        _previewedTrajectory = null;

        // Resume simulation
        ResumeSimulation();

        _isPreviewActive = false;

        GameLogger.Info("TrajectoryPreviewManager: Preview cleared");
    }

    public override void _ExitTree()
    {
        if (_isPreviewActive)
        {
            ClearPreview();
        }

        base._ExitTree();
    }

    private void PauseSimulation()
    {
        float previousScale = (float)Engine.TimeScale;
        Engine.TimeScale = 0.0;
        RuntimeSettings.Instance?.SetSetting("Simulation", "TimeScaleBeforePause", previousScale);
        RuntimeSettings.Instance?.SetSetting("Simulation", "IsPaused", true);
        RuntimeSettings.Instance?.SetSetting("Simulation", "TimeScale", 0.0f);

        GameLogger.Info("TrajectoryPreviewManager: Simulation paused for preview");
    }

    private void ResumeSimulation()
    {
        float previousScale =
            RuntimeSettings.Instance?.GetSetting<float>("Simulation", "TimeScaleBeforePause")
            ?? 1.0f;
        Engine.TimeScale = previousScale;
        RuntimeSettings.Instance?.SetSetting("Simulation", "IsPaused", false);
        RuntimeSettings.Instance?.SetSetting("Simulation", "TimeScale", previousScale);

        GameLogger.Info(
            $"TrajectoryPreviewManager: Simulation resumed (TimeScale: {previousScale})"
        );
    }

    /// <summary>
    /// Creates ghost spheres at the predicted positions of all celestial bodies
    /// at the trajectory's arrival time.
    /// </summary>
    private void CreateGhostBodies(TrajectorySolution trajectory)
    {
        if (_previewContainer == null)
            return;

        var sceneTree = GetTree();
        if (sceneTree == null)
            return;

        var celestialBodies = sceneTree.GetNodesInGroup("CelestialBody");
        if (celestialBodies.Count == 0)
            return;

        float tof = trajectory.TimeOfFlight;

        foreach (var node in celestialBodies)
        {
            if (node is not CelestialBody body || body.Mass <= 0)
                continue;

            var centralBody = OrbitalMath.FindCentralBody(body);
            float mu = OrbitalMath.GetGravitationalParameter(centralBody);

            Vector3 currentPos = body.GlobalPosition;
            Vector3 currentVel = body.Velocity;

            try
            {
                (Vector3 predictedPos, Vector3 _) = KeplerianMechanics.PropagateKepler(
                    currentPos,
                    currentVel,
                    mu,
                    tof
                );

                if (
                    float.IsNaN(predictedPos.X)
                    || float.IsNaN(predictedPos.Y)
                    || float.IsNaN(predictedPos.Z)
                )
                    predictedPos = currentPos;

                CreateGhostSphere(predictedPos, body.Radius, GhostBodyColor);

                // Also propagate satellite children
                foreach (var child in body.GetChildren())
                {
                    if (child is SatelliteBody satellite && satellite.Mass > 0)
                    {
                        float satelliteMu = OrbitalMath.GRAVITATIONAL_CONSTANT * body.Mass;
                        Vector3 satPos = satellite.GlobalPosition;
                        Vector3 satVel = satellite.Velocity;

                        (Vector3 predictedSatPos, Vector3 _) = KeplerianMechanics.PropagateKepler(
                            satPos,
                            satVel,
                            satelliteMu,
                            tof
                        );

                        if (
                            float.IsNaN(predictedSatPos.X)
                            || float.IsNaN(predictedSatPos.Y)
                            || float.IsNaN(predictedSatPos.Z)
                        )
                            predictedSatPos = satPos;

                        CreateGhostSphere(predictedSatPos, satellite.Radius, GhostBodyColor);
                    }
                }
            }
            catch (System.Exception e)
            {
                GameLogger.Warning(
                    $"TrajectoryPreviewManager: Failed to propagate {body.Name}: {e.Message}"
                );
            }
        }
    }

    /// <summary>
    /// Creates a ghost sphere mesh at the given position.
    /// </summary>
    private void CreateGhostSphere(Vector3 position, float radius, Color color)
    {
        if (_previewContainer == null)
            return;

        var sphere = new SphereMesh { Radius = radius, Height = radius * 2f };

        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };

        var meshInstance = new MeshInstance3D
        {
            Mesh = sphere,
            MaterialOverride = material,
            GlobalPosition = position,
        };

        _previewContainer.AddChild(meshInstance);
    }

    /// <summary>
    /// Creates the transfer arc visualization by sampling the trajectory's orbital elements
    /// at evenly-spaced time intervals.
    /// </summary>
    private void CreateTransferArc(TrajectorySolution trajectory)
    {
        if (_previewContainer == null)
            return;

        var arcPoints = SampleTransferArc(trajectory);
        if (arcPoints.Length < 2)
            return;

        var immediateMesh = new ImmediateMesh();
        var material = new StandardMaterial3D
        {
            AlbedoColor = ArcColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };

        immediateMesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip, material);
        foreach (var point in arcPoints)
        {
            immediateMesh.SurfaceAddVertex(point);
        }
        immediateMesh.SurfaceEnd();

        var meshInstance = new MeshInstance3D { Mesh = immediateMesh, Name = "TransferArc" };

        _previewContainer.AddChild(meshInstance);
    }

    /// <summary>
    /// Samples the transfer orbit at evenly-spaced time steps to produce an array of
    /// world-space positions along the arc.
    /// </summary>
    private Vector3[] SampleTransferArc(TrajectorySolution trajectory)
    {
        var points = new Vector3[ARC_SAMPLE_COUNT];
        float dt = (trajectory.TimeOfFlight) / (ARC_SAMPLE_COUNT - 1);
        float mu = trajectory.GravitationalParameter;

        for (int i = 0; i < ARC_SAMPLE_COUNT; i++)
        {
            float t = i * dt;
            Vector3 pos = KeplerianMechanics.PropagateFromElements(
                trajectory.SemiMajorAxis,
                trajectory.Eccentricity,
                trajectory.Inclination,
                trajectory.AscendingNodeLongitude,
                trajectory.ArgumentOfPeriapsis,
                trajectory.MeanAnomaly,
                mu,
                t
            );

            points[i] = pos;
        }

        return points;
    }

    /// <summary>
    /// Creates departure and arrival markers for the ship.
    /// </summary>
    private void CreateShipMarkers(TrajectorySolution trajectory, LogisticsUnit unit)
    {
        if (_previewContainer == null)
            return;

        // Departure marker at ship's current position
        CreateGhostSphere(unit.GlobalPosition, MARKER_SIZE, DepartureMarkerColor);

        // Arrival marker at predicted destination position
        if (trajectory.PredictedDestinationPosition != Vector3.Zero)
        {
            CreateGhostSphere(
                trajectory.PredictedDestinationPosition,
                MARKER_SIZE,
                ArrivalMarkerColor
            );
        }
        else if (trajectory.DestinationBody != null)
        {
            // Fallback: propagate destination body and use its position
            var destBody = trajectory.DestinationBody;
            var centralBody = OrbitalMath.FindCentralBody(destBody);
            float mu = OrbitalMath.GetGravitationalParameter(centralBody);

            try
            {
                (Vector3 destPredicted, Vector3 _) = KeplerianMechanics.PropagateKepler(
                    destBody.BodyPosition,
                    destBody.Velocity,
                    mu,
                    trajectory.TimeOfFlight
                );

                if (
                    !float.IsNaN(destPredicted.X)
                    && !float.IsNaN(destPredicted.Y)
                    && !float.IsNaN(destPredicted.Z)
                )
                {
                    CreateGhostSphere(destPredicted, MARKER_SIZE, ArrivalMarkerColor);
                }
            }
            catch (System.Exception e)
            {
                GameLogger.Warning(
                    $"TrajectoryPreviewManager: Failed to compute arrival marker: {e.Message}"
                );
            }
        }
    }
}
