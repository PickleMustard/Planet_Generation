#if DEBUG
using GdUnit4;
using static GdUnit4.Assertions;

using Debug.Console;

namespace Tests;

[TestSuite]
public class CommandParserTest
{
    private CommandParser _parser = null!;

    [Before]
    public void Setup()
    {
        _parser = new CommandParser();
    }

    // ==================== Global Command Parsing ====================

    [TestCase]
    public void Parse_GlobalCommand_NoArgs()
    {
        var result = _parser.Parse("help");

        AssertThat(result.CommandName).IsEqual("help");
        AssertThat(result.HasNamespaces).IsFalse();
        AssertThat(result.Arguments).HasSize(0);
        AssertThat(result.HasWildcard).IsFalse();
    }

    [TestCase]
    public void Parse_GlobalCommand_WithArgs()
    {
        var result = _parser.Parse("help spawn");

        AssertThat(result.CommandName).IsEqual("help");
        AssertThat(result.HasNamespaces).IsFalse();
        AssertThat(result.Arguments).HasSize(1);
        AssertThat(result.Arguments[0]).IsEqual("spawn");
    }

    [TestCase]
    public void Parse_GlobalCommand_MultipleArgs()
    {
        var result = _parser.Parse("spawn RockyPlanet NewPlanet");

        AssertThat(result.CommandName).IsEqual("spawn");
        AssertThat(result.Arguments).HasSize(2);
        AssertThat(result.Arguments[0]).IsEqual("RockyPlanet");
        AssertThat(result.Arguments[1]).IsEqual("NewPlanet");
    }

    [TestCase]
    public void Parse_GlobalCommand_QuotedArgs()
    {
        var result = _parser.Parse("spawn RockyPlanet \"New Planet\"");

        AssertThat(result.CommandName).IsEqual("spawn");
        AssertThat(result.Arguments).HasSize(2);
        AssertThat(result.Arguments[0]).IsEqual("RockyPlanet");
        AssertThat(result.Arguments[1]).IsEqual("New Planet");
    }

    // ==================== Single Namespace Parsing ====================

    [TestCase]
    public void Parse_SingleNamespace_NoArgs()
    {
        var result = _parser.Parse("(CelestialBody.Earth) orbit_bands");

        AssertThat(result.CommandName).IsEqual("orbit_bands");
        AssertThat(result.HasNamespaces).IsTrue();
        AssertThat(result.Namespaces).HasSize(1);
        AssertThat(result.Namespaces[0]).IsEqual("CelestialBody.Earth");
        AssertThat(result.HasWildcard).IsFalse();
        AssertThat(result.Arguments).HasSize(0);
    }

    [TestCase]
    public void Parse_SingleNamespace_WithArgs()
    {
        var result = _parser.Parse("(CelestialBody.Earth) spawn_station 0 MyStation");

        AssertThat(result.CommandName).IsEqual("spawn_station");
        AssertThat(result.Namespaces).HasSize(1);
        AssertThat(result.Namespaces[0]).IsEqual("CelestialBody.Earth");
        AssertThat(result.Arguments).HasSize(2);
        AssertThat(result.Arguments[0]).IsEqual("0");
        AssertThat(result.Arguments[1]).IsEqual("MyStation");
    }

    // ==================== Multi-Namespace Parsing ====================

    [TestCase]
    public void Parse_MultiNamespace_TwoTargets()
    {
        var result = _parser.Parse("(CelestialBody.Earth, CelestialBody.Mars) orbit_bands");

        AssertThat(result.CommandName).IsEqual("orbit_bands");
        AssertThat(result.HasNamespaces).IsTrue();
        AssertThat(result.Namespaces).HasSize(2);
        AssertThat(result.Namespaces[0]).IsEqual("CelestialBody.Earth");
        AssertThat(result.Namespaces[1]).IsEqual("CelestialBody.Mars");
        AssertThat(result.HasWildcard).IsFalse();
    }

    [TestCase]
    public void Parse_MultiNamespace_ThreeTargets()
    {
        var result = _parser.Parse("(CelestialBody.Earth, CelestialBody.Mars, Ships.0) status");

        AssertThat(result.CommandName).IsEqual("status");
        AssertThat(result.Namespaces).HasSize(3);
        AssertThat(result.Namespaces[0]).IsEqual("CelestialBody.Earth");
        AssertThat(result.Namespaces[1]).IsEqual("CelestialBody.Mars");
        AssertThat(result.Namespaces[2]).IsEqual("Ships.0");
    }

    [TestCase]
    public void Parse_MultiNamespace_TrimsWhitespace()
    {
        var result = _parser.Parse("(  CelestialBody.Earth ,  CelestialBody.Mars  ) orbit_bands");

        AssertThat(result.Namespaces).HasSize(2);
        AssertThat(result.Namespaces[0]).IsEqual("CelestialBody.Earth");
        AssertThat(result.Namespaces[1]).IsEqual("CelestialBody.Mars");
    }

    // ==================== Wildcard Parsing ====================

    [TestCase]
    public void Parse_Wildcard_SingleWildcard()
    {
        var result = _parser.Parse("(CelestialBody.*) orbit_bands");

        AssertThat(result.CommandName).IsEqual("orbit_bands");
        AssertThat(result.HasNamespaces).IsTrue();
        AssertThat(result.Namespaces).HasSize(1);
        AssertThat(result.Namespaces[0]).IsEqual("CelestialBody.*");
        AssertThat(result.HasWildcard).IsTrue();
    }

    [TestCase]
    public void Parse_Wildcard_MixedWithExplicit()
    {
        var result = _parser.Parse("(CelestialBody.*, Ships.0) status");

        AssertThat(result.CommandName).IsEqual("status");
        AssertThat(result.Namespaces).HasSize(2);
        AssertThat(result.Namespaces[0]).IsEqual("CelestialBody.*");
        AssertThat(result.Namespaces[1]).IsEqual("Ships.0");
        AssertThat(result.HasWildcard).IsTrue();
    }

    // ==================== Edge Cases ====================

    [TestCase]
    public void Parse_EmptyInput_ReturnsEmptyCommand()
    {
        var result = _parser.Parse("");

        AssertThat(result.CommandName).IsNull();
        AssertThat(result.HasNamespaces).IsFalse();
    }

    [TestCase]
    public void Parse_WhitespaceInput_ReturnsEmptyCommand()
    {
        var result = _parser.Parse("   ");

        AssertThat(result.CommandName).IsNull();
        AssertThat(result.HasNamespaces).IsFalse();
    }

    [TestCase]
    public void Parse_UnclosedParen_ReturnsNullCommand()
    {
        var result = _parser.Parse("(CelestialBody.Earth orbit_bands");

        AssertThat(result.CommandName).IsNull();
        AssertThat(result.HasNamespaces).IsFalse();
    }

    [TestCase]
    public void Parse_EmptyParens_NoNamespaces()
    {
        var result = _parser.Parse("() orbit_bands");

        AssertThat(result.CommandName).IsEqual("orbit_bands");
        AssertThat(result.HasNamespaces).IsFalse();
    }

    [TestCase]
    public void Parse_ParensOnly_NoCommand()
    {
        var result = _parser.Parse("(CelestialBody.Earth)");

        AssertThat(result.CommandName).IsNull();
        AssertThat(result.Namespaces).HasSize(1);
        AssertThat(result.Namespaces[0]).IsEqual("CelestialBody.Earth");
    }

    [TestCase]
    public void Parse_RawInputPreserved()
    {
        string input = "(CelestialBody.Earth) orbit_bands arg1";
        var result = _parser.Parse(input);

        AssertThat(result.RawInput).IsEqual(input);
    }

    [TestCase]
    public void Parse_EscapedCharacters()
    {
        var result = _parser.Parse("spawn \"name with \\\"quotes\\\"\"");

        AssertThat(result.CommandName).IsEqual("spawn");
        AssertThat(result.Arguments).HasSize(1);
        AssertThat(result.Arguments[0]).IsEqual("name with \"quotes\"");
    }

    // ==================== SplitNamespace ====================

    [TestCase]
    public void SplitNamespace_TypeAndIdentifier()
    {
        var (typeName, identifier) = _parser.SplitNamespace("CelestialBody.Earth");

        AssertThat(typeName).IsEqual("CelestialBody");
        AssertThat(identifier).IsEqual("Earth");
    }

    [TestCase]
    public void SplitNamespace_TypeOnly()
    {
        var (typeName, identifier) = _parser.SplitNamespace("CelestialBody");

        AssertThat(typeName).IsEqual("CelestialBody");
        AssertThat(identifier).IsNull();
    }

    [TestCase]
    public void SplitNamespace_Wildcard()
    {
        var (typeName, identifier) = _parser.SplitNamespace("CelestialBody.*");

        AssertThat(typeName).IsEqual("CelestialBody");
        AssertThat(identifier).IsEqual("*");
    }

    [TestCase]
    public void SplitNamespace_Empty()
    {
        var (typeName, identifier) = _parser.SplitNamespace("");

        AssertThat(typeName).IsNull();
        AssertThat(identifier).IsNull();
    }

    // ==================== IsValidCommand ====================

    [TestCase]
    public void IsValidCommand_ValidGlobal()
    {
        AssertThat(_parser.IsValidCommand("help")).IsTrue();
    }

    [TestCase]
    public void IsValidCommand_ValidNamespaced()
    {
        AssertThat(_parser.IsValidCommand("(CelestialBody.Earth) orbit_bands")).IsTrue();
    }

    [TestCase]
    public void IsValidCommand_Empty()
    {
        AssertThat(_parser.IsValidCommand("")).IsFalse();
    }

    [TestCase]
    public void IsValidCommand_UnclosedParen()
    {
        AssertThat(_parser.IsValidCommand("(CelestialBody.Earth orbit_bands")).IsFalse();
    }

    // ==================== Backward Incompatibility ====================

    [TestCase]
    public void Parse_OldDotSyntax_TreatedAsGlobalCommand()
    {
        // The old syntax "CelestialBody.Earth.orbit_bands" should now be treated
        // as a global command named "CelestialBody.Earth.orbit_bands" (no namespace),
        // which will fail lookup — this confirms the breaking change.
        var result = _parser.Parse("CelestialBody.Earth.orbit_bands");

        AssertThat(result.CommandName).IsEqual("CelestialBody.Earth.orbit_bands");
        AssertThat(result.HasNamespaces).IsFalse();
    }
}
#endif
