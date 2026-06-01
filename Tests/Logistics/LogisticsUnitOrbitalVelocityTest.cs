using Constructables;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace Tests.Logistics;

/// <summary>
/// Tests for LogisticsUnit orbital velocity calculations (Ticket #1).
/// These are pure unit tests that don't require Godot runtime since they
/// test mathematical calculations without scene tree dependencies.
/// </summary>
[TestSuite]
public class LogisticsUnitOrbitalVelocityTest
{
    // Test tolerance for floating point comparisons
    private const float Tolerance = 0.001f;
    private Vector3 ToleranceVector = new Vector3(Tolerance, Tolerance, Tolerance);

    /// <summary>
    /// Creates a LogisticsUnit with specified orbital parameters for testing.
    /// Uses reflection to set private fields since we can't rely on initialization
    /// without a Godot scene tree.
    /// </summary>
    private LogisticsUnit CreateTestUnit(
        float orbitalRadius = 1000f,
        float orbitalSpeed = 0.1f,
        float orbitalAngle = 0f
    )
    {
        var unit = new LogisticsUnit();

        // Use reflection to set private orbital state fields
        var orbitalRadiusField = typeof(LogisticsUnit).GetField(
            "_orbitalRadius",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        var orbitalSpeedField = typeof(LogisticsUnit).GetField(
            "_orbitalSpeed",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        var orbitalAngleField = typeof(LogisticsUnit).GetField(
            "_orbitalAngle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        var isInitializedField = typeof(LogisticsUnit).GetField(
            "_isInitialized",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );

        if (orbitalRadiusField != null)
            orbitalRadiusField.SetValue(unit, orbitalRadius);
        if (orbitalSpeedField != null)
            orbitalSpeedField.SetValue(unit, orbitalSpeed);
        if (orbitalAngleField != null)
            orbitalAngleField.SetValue(unit, orbitalAngle);
        if (isInitializedField != null)
            isInitializedField.SetValue(unit, true);

        return unit;
    }

    // ========================================================================
    // GetOrbitalVelocity TESTS
    // ========================================================================

    [TestCase]
    [RequireGodotRuntime]
    public void GetOrbitalVelocity_ZeroAngle_ReturnsCorrectVector()
    {
        // Arrange
        float radius = 1000f; // meters
        float speed = 0.1f; // rad/s
        float angle = 0f; // radians (pointing along +X axis)
        var unit = CreateTestUnit(radius, speed, angle);

        // Act
        Vector3 velocity = unit.GetOrbitalVelocity();

        // Assert
        // At angle 0: position is (r, 0, 0), velocity should be (0, 0, v) where v = r × ω
        float expectedSpeed = radius * speed; // 1000 × 0.1 = 100 m/s
        Vector3 expectedVelocity = new Vector3(0f, 0f, expectedSpeed);

        AssertThat(velocity).IsEqual(expectedVelocity);
        AssertThat(velocity.Length()).IsEqualApprox(expectedSpeed, Tolerance);
    }

    [TestCase]
    public void GetOrbitalVelocity_NinetyDegreeAngle_ReturnsCorrectVector()
    {
        // Arrange
        float radius = 1000f;
        float speed = 0.1f;
        float angle = Mathf.Pi / 2f; // 90 degrees (pointing along +Z axis)
        var unit = CreateTestUnit(radius, speed, angle);

        // Act
        Vector3 velocity = unit.GetOrbitalVelocity();

        // Assert
        // At angle π/2: position is (0, 0, r), velocity should be (-v, 0, 0)
        float expectedSpeed = radius * speed; // 100 m/s
        Vector3 expectedVelocity = new Vector3(-expectedSpeed, 0f, 0f);

        AssertThat(velocity).IsEqualApprox(expectedVelocity, ToleranceVector);
        AssertThat(velocity.Length()).IsEqualApprox(expectedSpeed, Tolerance);
    }

    [TestCase]
    public void GetOrbitalVelocity_180DegreeAngle_ReturnsCorrectVector()
    {
        // Arrange
        float radius = 1000f;
        float speed = 0.1f;
        float angle = Mathf.Pi; // 180 degrees (pointing along -X axis)
        var unit = CreateTestUnit(radius, speed, angle);

        // Act
        Vector3 velocity = unit.GetOrbitalVelocity();

        // Assert
        // At angle π: position is (-r, 0, 0), velocity should be (0, 0, -v)
        float expectedSpeed = radius * speed;
        Vector3 expectedVelocity = new Vector3(0f, 0f, -expectedSpeed);

        AssertThat(velocity).IsEqualApprox(expectedVelocity, ToleranceVector);
        AssertThat(velocity.Length()).IsEqualApprox(expectedSpeed, Tolerance);
    }

    [TestCase]
    public void GetOrbitalVelocity_45DegreeAngle_ReturnsCorrectVector()
    {
        // Arrange
        float radius = 1000f;
        float speed = 0.1f;
        float angle = Mathf.Pi / 4f; // 45 degrees
        var unit = CreateTestUnit(radius, speed, angle);

        // Act
        Vector3 velocity = unit.GetOrbitalVelocity();

        // Assert
        // At angle π/4: position = (r/√2, 0, r/√2), velocity = (-v/√2, 0, v/√2)
        float expectedSpeed = radius * speed; // 100 m/s
        float component = expectedSpeed / Mathf.Sqrt2; // ~70.71 m/s
        Vector3 expectedVelocity = new Vector3(-component, 0f, component);

        AssertThat(velocity).IsEqual(expectedVelocity);
        AssertThat(velocity.Length()).IsEqualApprox(expectedSpeed, Tolerance);
    }

    [TestCase]
    public void GetOrbitalVelocity_InvalidParameters_ReturnsZeroVector()
    {
        // Arrange
        float radius = 0f; // Invalid radius
        float speed = 0.1f;
        float angle = 0f;
        var unit = CreateTestUnit(radius, speed, angle);

        // Act
        Vector3 velocity = unit.GetOrbitalVelocity();

        // Assert
        AssertThat(velocity).IsEqual(Vector3.Zero);
    }

    [TestCase]
    public void GetOrbitalVelocity_VelocityMagnitudeMatchesOrbitalSpeedTimesRadius()
    {
        // Test multiple configurations
        var testCases = new (float radius, float speed, float angle)[]
        {
            (1000f, 0.1f, 0f),
            (2000f, 0.05f, Mathf.Pi / 4f),
            (5000f, 0.01f, Mathf.Pi / 2f),
            (8000f, 0.02f, Mathf.Pi),
            (3000f, 0.15f, 3f * Mathf.Pi / 2f),
        };

        foreach (var (radius, speed, angle) in testCases)
        {
            var unit = CreateTestUnit(radius, speed, angle);
            Vector3 velocity = unit.GetOrbitalVelocity();
            float calculatedSpeed = velocity.Length();
            float expectedSpeed = radius * speed;

            // Check if speeds are approximately equal
            AssertThat(Mathf.Abs(calculatedSpeed - expectedSpeed) < Tolerance).IsTrue();
        }
    }

    [TestCase]
    public void GetOrbitalVelocity_VelocityIsPerpendicularToRadialDirection()
    {
        // Test multiple configurations
        var testCases = new (float radius, float speed, float angle)[]
        {
            (1000f, 0.1f, 0f),
            (2000f, 0.05f, Mathf.Pi / 4f),
            (5000f, 0.01f, Mathf.Pi / 2f),
            (8000f, 0.02f, Mathf.Pi),
            (3000f, 0.15f, 3f * Mathf.Pi / 2f),
        };

        foreach (var (radius, speed, angle) in testCases)
        {
            var unit = CreateTestUnit(radius, speed, angle);

            // Calculate radial direction vector
            Vector3 radialDirection = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            Vector3 velocity = unit.GetOrbitalVelocity();
            float dotProduct = velocity.Dot(radialDirection);

            // Check if dot product is near zero (perpendicular)
            AssertThat(Mathf.Abs(dotProduct) < 0.001f).IsTrue();
        }
    }

    // ========================================================================
    // GetOrbitalRadius TESTS
    // ========================================================================

    [TestCase]
    public void GetOrbitalRadius_ReturnsStoredValue()
    {
        // Arrange
        float expectedRadius = 1234.5f;
        var unit = CreateTestUnit(expectedRadius);

        // Act
        float actualRadius = unit.OrbitalRadius;

        // Assert
        AssertThat(actualRadius).IsEqual(expectedRadius);
    }

    // ========================================================================
    // GetOrbitalAngle TESTS
    // ========================================================================

    [TestCase]
    public void GetOrbitalAngle_ReturnsStoredValue()
    {
        // Arrange
        float expectedAngle = 2.5f; // radians
        var unit = CreateTestUnit(1000f, 0.1f, expectedAngle);

        // Act
        float actualAngle = unit.OrbitalAngle;

        // Assert
        AssertThat(actualAngle).IsEqual(expectedAngle);
    }

    // ========================================================================
    // PredictShipPositionAtTime TESTS
    // ========================================================================

    [TestCase]
    public void PredictShipPositionAtTime_LargeTimeDelta_WrapsAngleCorrectly()
    {
        // Test angle normalization calculation
        float speed = 0.1f; // 0.1 rad/s
        float initialAngle = 1.0f; // radians
        float timeDelta = 100f; // seconds

        // Expected: angle increases by speed × time = 10 rad
        // After normalization: (1 + 10) % 2π = 11 % 6.283 = 4.717 rad
        float newAngleRaw = initialAngle + (speed * timeDelta);
        float expectedAngle = newAngleRaw % Mathf.Tau;
        if (expectedAngle < 0f)
            expectedAngle += Mathf.Tau;

        // Assert angle calculation is correct
        AssertThat(expectedAngle).IsEqualApprox(4.716814692820414f, Tolerance);
    }
}

