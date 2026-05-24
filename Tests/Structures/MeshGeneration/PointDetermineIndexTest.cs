using System.Collections.Generic;
using GdUnit4;
using static GdUnit4.Assertions;
using Structures.MeshGeneration;
using Godot;

namespace Tests.Structures.MeshGeneration;

[TestSuite]
public class PointDetermineIndexTest
{
    [TestCase]
    public void DetermineIndex_AntipodalAxisPoints_DistinctIndices()
    {
        int xp = Point.DetermineIndex(1f, 0f, 0f);
        int xn = Point.DetermineIndex(-1f, 0f, 0f);
        int yp = Point.DetermineIndex(0f, 1f, 0f);
        int yn = Point.DetermineIndex(0f, -1f, 0f);
        int zp = Point.DetermineIndex(0f, 0f, 1f);
        int zn = Point.DetermineIndex(0f, 0f, -1f);

        var set = new HashSet<int> { xp, xn, yp, yn, zp, zn };
        AssertThat(set.Count).IsEqual(6);
    }

    [TestCase]
    public void DetermineIndex_SameCoordinates_SameIndex()
    {
        int a = Point.DetermineIndex(0.123456f, -0.654321f, 0.111111f);
        int b = Point.DetermineIndex(0.123456f, -0.654321f, 0.111111f);
        AssertThat(a).IsEqual(b);
    }

    [TestCase]
    public void DetermineIndex_WithinQuantization_SameIndex()
    {
        // 6-decimal quantization (QUANT_SCALE = 1e6): sub-1e-7 differences collapse.
        int a = Point.DetermineIndex(0.5f, 0.5f, 0.5f);
        int b = Point.DetermineIndex(0.5f + 1e-8f, 0.5f - 1e-8f, 0.5f);
        AssertThat(a).IsEqual(b);
    }

    [TestCase]
    public void DetermineIndex_SignedZero_NormalizedToZero()
    {
        int pos = Point.DetermineIndex(0f, 0f, 0f);
        int neg = Point.DetermineIndex(-0f, -0f, -0f);
        AssertThat(pos).IsEqual(neg);
    }

    [TestCase]
    public void DetermineIndex_StressRandomSphereSurface_NoCollisions()
    {
        // ~100k distinct sphere-surface positions; pre-fix this would intermittently collide
        // via 32-bit HashCode.Combine (birthday paradox + per-process seed randomization).
        var rng = new RandomNumberGenerator();
        rng.Seed = 0xC0FFEEUL;
        const int N = 100_000;
        var seenIndices = new HashSet<int>(N);
        var seenKeys = new HashSet<(int, int, int)>(N);
        int duplicateKeys = 0;
        for (int i = 0; i < N; i++)
        {
            var v = new Vector3(
                rng.RandfRange(-1f, 1f),
                rng.RandfRange(-1f, 1f),
                rng.RandfRange(-1f, 1f)).Normalized();
            var key = (
                (int)Mathf.Round(v.X * 1e6f),
                (int)Mathf.Round(v.Y * 1e6f),
                (int)Mathf.Round(v.Z * 1e6f));
            if (!seenKeys.Add(key))
            {
                duplicateKeys++;
                continue;
            }
            int idx = Point.DetermineIndex(v.X, v.Y, v.Z);
            AssertThat(seenIndices.Add(idx)).IsTrue();
        }
    }
}
