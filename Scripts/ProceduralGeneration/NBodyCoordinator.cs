using System.Collections.Generic;
using Godot;
using UtilityLibrary;
using UtilityLibrary.GameMath.Orbital;

namespace ProceduralGeneration.PlanetGeneration;

/// <summary>
/// Central coordinator for N-body gravitational physics.
/// Replaces per-body _PhysicsProcess with a synchronized Kick-Drift-Kick (KDK)
/// leapfrog integrator. This is symplectic and time-reversible, preventing the
/// secular energy drift caused by unsynchronized per-body updates.
/// After integration, anchors the system barycenter to the origin.
/// </summary>
public partial class NBodyCoordinator : Node
{
    private List<CelestialBody> _bodies = new();
    private Vector3[] _accelerations = [];
    private bool _initialized;

#if DEBUG
    private float _initialEnergy = float.NaN;
    private int _frameCount;
    private const int DIAGNOSTIC_INTERVAL = 600; // Log every N frames
#endif

    public override void _Ready()
    {
        ProcessPriority = -100; // Run before all other nodes
        CelestialBody.CoordinatorActive = true;
        RefreshBodyList();
    }

    public override void _ExitTree()
    {
        CelestialBody.CoordinatorActive = false;
    }

    /// <summary>
    /// Refreshes the cached body list from the scene tree group.
    /// Call this when bodies are added or removed.
    /// </summary>
    public void RefreshBodyList()
    {
        _bodies.Clear();
        var nodes = GetTree().GetNodesInGroup("CelestialBody");
        foreach (var node in nodes)
        {
            if (node is CelestialBody body)
                _bodies.Add(body);
        }

        _accelerations = new Vector3[_bodies.Count];
        _initialized = false;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_bodies.Count == 0)
        {
            RefreshBodyList();
            if (_bodies.Count == 0)
                return;
        }

        float dt = (float)delta;

        if (!_initialized)
        {
            // First frame: compute initial accelerations
            ComputeAllAccelerations();
            _initialized = true;

#if DEBUG
            _initialEnergy = ComputeTotalEnergy();
            GameLogger.Debug(
                $"NBodyCoordinator initialized with {_bodies.Count} bodies, "
                    + $"initial energy: {_initialEnergy:F6}"
            );
#endif
        }

        // KDK Leapfrog:
        // 1. Half-kick: v += 0.5 * a * dt (using accelerations from previous position)
        HalfKick(dt);

        // 2. Drift: x += v * dt
        Drift(dt);

        // 3. Recompute accelerations at new positions
        ComputeAllAccelerations();

        // 4. Half-kick: v += 0.5 * a * dt (using accelerations at new positions)
        HalfKick(dt);

        // 5. Anchor barycenter to origin and zero COM velocity
        AnchorBarycenterToOrigin();

#if DEBUG
        _frameCount++;
        if (_frameCount % DIAGNOSTIC_INTERVAL == 0)
            LogDiagnostics();
#endif
    }

    private void ComputeAllAccelerations()
    {
        int n = _bodies.Count;
        for (int i = 0; i < n; i++)
            _accelerations[i] = Vector3.Zero;

        // Compute pairwise forces using Newton's third law symmetry
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                Vector3 direction = _bodies[j].GlobalPosition - _bodies[i].GlobalPosition;
                float distanceSq = direction.LengthSquared();

                if (distanceSq < 1e-6f)
                    continue;

                float forceMag =
                    OrbitalMath.GRAVITATIONAL_CONSTANT
                    * _bodies[i].Mass
                    * _bodies[j].Mass
                    / distanceSq;

                Vector3 forceDir = direction.Normalized();
                Vector3 force = forceDir * forceMag;

                // Newton's third law: equal and opposite
                _accelerations[i] += force / _bodies[i].Mass;
                _accelerations[j] -= force / _bodies[j].Mass;
            }
        }
    }

    private void HalfKick(float dt)
    {
        float halfDt = 0.5f * dt;
        for (int i = 0; i < _bodies.Count; i++)
        {
            _bodies[i].Velocity += _accelerations[i] * halfDt;
        }
    }

    private void Drift(float dt)
    {
        for (int i = 0; i < _bodies.Count; i++)
        {
            _bodies[i].GlobalPosition += _bodies[i].Velocity * dt;
        }
    }

    private void AnchorBarycenterToOrigin()
    {
        if (_bodies.Count == 0)
            return;

        Vector3 comPosition = Vector3.Zero;
        Vector3 comVelocity = Vector3.Zero;
        float totalMass = 0f;

        for (int i = 0; i < _bodies.Count; i++)
        {
            float mass = _bodies[i].Mass;
            comPosition += _bodies[i].GlobalPosition * mass;
            comVelocity += _bodies[i].Velocity * mass;
            totalMass += mass;
        }

        if (totalMass <= 0f)
            return;

        comPosition /= totalMass;
        comVelocity /= totalMass;

        // Shift all bodies so COM is at origin with zero net velocity
        for (int i = 0; i < _bodies.Count; i++)
        {
            _bodies[i].GlobalPosition -= comPosition;
            _bodies[i].Velocity -= comVelocity;
        }
    }

#if DEBUG
    private float ComputeTotalEnergy()
    {
        float kinetic = 0f;
        float potential = 0f;
        int n = _bodies.Count;

        for (int i = 0; i < n; i++)
        {
            kinetic += 0.5f * _bodies[i].Mass * _bodies[i].Velocity.LengthSquared();

            for (int j = i + 1; j < n; j++)
            {
                float distance = _bodies[i].GlobalPosition.DistanceTo(
                    _bodies[j].GlobalPosition
                );
                if (distance > 1e-6f)
                {
                    potential -=
                        OrbitalMath.GRAVITATIONAL_CONSTANT
                        * _bodies[i].Mass
                        * _bodies[j].Mass
                        / distance;
                }
            }
        }

        return kinetic + potential;
    }

    private void LogDiagnostics()
    {
        float currentEnergy = ComputeTotalEnergy();
        float energyDrift = float.IsNaN(_initialEnergy)
            ? 0f
            : (currentEnergy - _initialEnergy) / Mathf.Abs(_initialEnergy);

        Vector3 totalMomentum = Vector3.Zero;
        for (int i = 0; i < _bodies.Count; i++)
            totalMomentum += _bodies[i].Velocity * _bodies[i].Mass;

        GameLogger.Debug(
            $"NBodyCoordinator [frame {_frameCount}]: "
                + $"energy={currentEnergy:F6}, drift={energyDrift:E3}, "
                + $"|momentum|={totalMomentum.Length():E3}"
        );
    }
#endif
}
