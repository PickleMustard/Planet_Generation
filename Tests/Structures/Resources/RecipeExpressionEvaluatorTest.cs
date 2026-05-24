using System.Collections.Generic;
using GdUnit4;
using static GdUnit4.Assertions;
using Structures.Resources;

namespace Tests.Structures.Resources;

[TestSuite]
public class RecipeExpressionEvaluatorTest
{
    private static IReadOnlyDictionary<string, float> Ctx(
        float temperature = 0f,
        float moisture = 0f,
        float elevation = 0f,
        float atmosphere = 0f,
        float specifier = 0f)
        => new Dictionary<string, float>
        {
            { "temperature", temperature },
            { "moisture", moisture },
            { "elevation", elevation },
            { "atmosphere", atmosphere },
            { "specifier", specifier },
        };

    private static float Eval(string expr, IReadOnlyDictionary<string, float> ctx)
        => RecipeExpressionEvaluator.Compile(expr).Evaluate(ctx);

    private static bool EvalBool(string expr, IReadOnlyDictionary<string, float> ctx)
        => RecipeExpressionEvaluator.EvaluateBool(RecipeExpressionEvaluator.Compile(expr), ctx);

    [TestCase]
    public void Arithmetic_RespectsStandardPrecedence()
    {
        AssertThat(Eval("1 + 2 * 3", Ctx())).IsEqual(7f);
        AssertThat(Eval("(1 + 2) * 3", Ctx())).IsEqual(9f);
        AssertThat(Eval("10 - 4 - 2", Ctx())).IsEqual(4f);
        AssertThat(Eval("8 / 4 / 2", Ctx())).IsEqual(1f);
        AssertThat(Eval("-3 + 5", Ctx())).IsEqual(2f);
    }

    [TestCase]
    public void Comparisons_ReturnOneOrZero()
    {
        AssertThat(Eval("2 < 3", Ctx())).IsEqual(1f);
        AssertThat(Eval("3 < 2", Ctx())).IsEqual(0f);
        AssertThat(Eval("3 == 3", Ctx())).IsEqual(1f);
        AssertThat(Eval("3 != 3", Ctx())).IsEqual(0f);
        AssertThat(Eval("3 >= 3", Ctx())).IsEqual(1f);
    }

    [TestCase]
    public void BooleanOperators_AndOrNot()
    {
        AssertThat(EvalBool("1 < 2 && 3 < 4", Ctx())).IsTrue();
        AssertThat(EvalBool("1 < 2 && 3 > 4", Ctx())).IsFalse();
        AssertThat(EvalBool("1 > 2 || 3 < 4", Ctx())).IsTrue();
        AssertThat(EvalBool("!(1 < 2)", Ctx())).IsFalse();
        AssertThat(EvalBool("!(1 > 2)", Ctx())).IsTrue();
    }

    [TestCase]
    public void Variables_AllFiveResolve()
    {
        var ctx = Ctx(temperature: 0.7f, moisture: 0.3f, elevation: 0.1f,
                       atmosphere: 1.0f, specifier: 2f);
        AssertThat(Eval("temperature", ctx)).IsEqual(0.7f);
        AssertThat(Eval("moisture", ctx)).IsEqual(0.3f);
        AssertThat(Eval("elevation", ctx)).IsEqual(0.1f);
        AssertThat(Eval("atmosphere", ctx)).IsEqual(1.0f);
        AssertThat(Eval("specifier", ctx)).IsEqual(2f);
    }

    [TestCase]
    public void FarmStyleConditions_GateOnSpecifierAndEnvironment()
    {
        // Crop_rotation style: specifier picks the crop, env may further gate.
        AssertThat(EvalBool("specifier == 2 && moisture > 0.4",
            Ctx(moisture: 0.5f, specifier: 2f))).IsTrue();
        AssertThat(EvalBool("specifier == 2 && moisture > 0.4",
            Ctx(moisture: 0.3f, specifier: 2f))).IsFalse();
        AssertThat(EvalBool("specifier == 4 && elevation < 0.4",
            Ctx(elevation: 0.2f, specifier: 4f))).IsTrue();
    }

    [TestCase]
    public void UnknownIdentifier_Throws()
    {
        AssertThrown(() => RecipeExpressionEvaluator.Compile("unknown_var > 0"))
            .IsInstanceOf<RecipeExpressionException>();
    }

    [TestCase]
    public void SyntaxError_Throws()
    {
        AssertThrown(() => RecipeExpressionEvaluator.Compile("1 + "))
            .IsInstanceOf<RecipeExpressionException>();
        AssertThrown(() => RecipeExpressionEvaluator.Compile("(1 + 2"))
            .IsInstanceOf<RecipeExpressionException>();
        AssertThrown(() => RecipeExpressionEvaluator.Compile("1 == 2 ="))
            .IsInstanceOf<RecipeExpressionException>();
    }

    [TestCase]
    public void DivisionByZero_ReturnsZero_NoThrow()
    {
        AssertThat(Eval("1 / 0", Ctx())).IsEqual(0f);
        AssertThat(Eval("1 % 0", Ctx())).IsEqual(0f);
    }
}
