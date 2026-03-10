using System;
using System.Collections.Generic;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using ProceduralGeneration.MeshGeneration.ResourceGeneration;
using Structures.Resources;

namespace Tests.ResourceGeneration;

[TestSuite]
public class ResourceVisualizerTest
{
    [TestCase]
    [RequireGodotRuntime]
    public void ApplyResourceTintBasic()
    {
        var baseColor = Colors.Green;
        var resources = new Dictionary<string, float>
        {
            ["iron_ore"] = 0.8f
        };

        var result = ResourceVisualizer.ApplyResourceTint(baseColor, resources);

        AssertThat(result).IsNotEqual(baseColor);

        var ironColor = ResourceDatabase.Instance.GetResourceColor("iron_ore");
        float expectedR = Mathf.Lerp(baseColor.R, ironColor.R, 0.8f * 0.35f);
        AssertThat(Mathf.IsEqualApprox(result.R, expectedR, 0.01f)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ApplyResourceTintEmpty()
    {
        var baseColor = Colors.Blue;

        var result1 = ResourceVisualizer.ApplyResourceTint(baseColor, null!);
        AssertThat(result1).IsEqual(baseColor);

        var result2 = ResourceVisualizer.ApplyResourceTint(baseColor, new Dictionary<string, float>());
        AssertThat(result2).IsEqual(baseColor);

        var lowAbundance = new Dictionary<string, float> { ["iron_ore"] = 0.05f };
        var result3 = ResourceVisualizer.ApplyResourceTint(baseColor, lowAbundance);
        AssertThat(result3).IsEqual(baseColor);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetBlendedDepositColor()
    {
        var deposits = new Dictionary<string, ResourceDeposit>
        {
            ["iron_ore"] = new ResourceDeposit("iron_ore", 0.8f, 1.0f),
            ["copper_ore"] = new ResourceDeposit("copper_ore", 0.4f, 1.0f)
        };

        var result = ResourceVisualizer.GetBlendedDepositColor(deposits);

        AssertThat(result).IsNotEqual(Colors.White);
        AssertThat(result).IsNotEqual(Colors.Black);

        var ironColor = ResourceDatabase.Instance.GetResourceColor("iron_ore");
        var copperColor = ResourceDatabase.Instance.GetResourceColor("copper_ore");
        AssertThat(result.R >= MathF.Min(ironColor.R, copperColor.R) - 0.01f).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TintStrengthScaling()
    {
        var baseColor = Colors.White;
        var ironColor = ResourceDatabase.Instance.GetResourceColor("iron_ore");

        var lowAbundance = new Dictionary<string, float> { ["iron_ore"] = 0.2f };
        var highAbundance = new Dictionary<string, float> { ["iron_ore"] = 1.0f };

        var lowResult = ResourceVisualizer.ApplyResourceTint(baseColor, lowAbundance);
        var highResult = ResourceVisualizer.ApplyResourceTint(baseColor, highAbundance);

        float lowDiff = Mathf.Abs(lowResult.R - baseColor.R) + Mathf.Abs(lowResult.G - baseColor.G) + Mathf.Abs(lowResult.B - baseColor.B);
        float highDiff = Mathf.Abs(highResult.R - baseColor.R) + Mathf.Abs(highResult.G - baseColor.G) + Mathf.Abs(highResult.B - baseColor.B);

        AssertThat(highDiff > lowDiff).IsTrue();
    }
}
