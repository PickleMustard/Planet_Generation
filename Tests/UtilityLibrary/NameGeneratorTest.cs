using GdUnit4;
using Godot;
using Godot.Collections;
using UtilityLibrary.NameGeneration;
using static GdUnit4.Assertions;

namespace Tests.UtilityLibrary;

[TestSuite]
public class NameGeneratorTest
{
    [TestCase]
    public void TestPickNameBasic()
    {
        // Create a simple name dictionary for testing
        var nameDict = new Dictionary
        {
            ["category1"] = new Array { "Name1", "Name2", "Name3" },
            ["category2"] = new Array { "Alpha", "Beta", "Gamma" }
        };

        // Test multiple times to ensure random selection works
        for (int i = 0; i < 10; i++)
        {
            var name = NameGenerator.PickName(nameDict);
            AssertThat(name).IsNotNull();
            AssertThat(name).IsNotEmpty();
            
            // Name should be one of our test names
            bool isValidName = name == "Name1" || name == "Name2" || name == "Name3" ||
                              name == "Alpha" || name == "Beta" || name == "Gamma";
            AssertThat(isValidName).IsTrue();
        }
    }

    [TestCase]
    public void TestPickNameEmptyDictionary()
    {
        var emptyDict = new Dictionary();
        var name = NameGenerator.PickName(emptyDict);
        AssertThat(name).IsEmpty();
    }

    [TestCase]
    public void TestPickNameNullDictionary()
    {
        var name = NameGenerator.PickName(null);
        AssertThat(name).IsEmpty();
    }

    [TestCase]
    public void TestPickNameEmptyCategories()
    {
        var nameDict = new Dictionary
        {
            ["emptyCategory"] = new Array() // Empty array
        };
        
        var name = NameGenerator.PickName(nameDict);
        AssertThat(name).IsEmpty();
    }

    [TestCase]
    public void TestFormatNameLowerCase()
    {
        var result = NameGenerator.FormatName("Test Name", NameFormat.LowerCase);
        AssertThat(result).IsEqual("test name");
    }

    [TestCase]
    public void TestFormatNameTitleCase()
    {
        var result = NameGenerator.FormatName("test name", NameFormat.TitleCase);
        AssertThat(result).IsEqual("Test Name");
    }

    [TestCase]
    public void TestFormatNameNoSpaces()
    {
        var result = NameGenerator.FormatName("Test Name", NameFormat.NoSpaces);
        AssertThat(result).IsEqual("TestName");
    }

    [TestCase]
    public void TestFormatNameHyphenated()
    {
        var result = NameGenerator.FormatName("Test Name", NameFormat.Hyphenated);
        AssertThat(result).IsEqual("Test-Name");
    }

    [TestCase]
    public void TestFormatNameNoSpacesLowerCase()
    {
        var result = NameGenerator.FormatName("Test Name", NameFormat.NoSpacesLowerCase);
        AssertThat(result).IsEqual("testname");
    }

    [TestCase]
    public void TestFormatNameDefault()
    {
        var result = NameGenerator.FormatName("Test Name", NameFormat.Default);
        AssertThat(result).IsEqual("Test Name");
    }

    [TestCase]
    public void TestToTitleCase()
    {
        var result = NameGenerator.ToTitleCase("hello world");
        AssertThat(result).IsEqual("Hello World");
        
        result = NameGenerator.ToTitleCase("HELLO WORLD");
        AssertThat(result).IsEqual("Hello World");
        
        result = NameGenerator.ToTitleCase("hElLo wOrLd");
        AssertThat(result).IsEqual("Hello World");
    }

    [TestCase]
    public void TestToTitleCaseEmpty()
    {
        var result = NameGenerator.ToTitleCase("");
        AssertThat(result).IsEmpty();
        
        result = NameGenerator.ToTitleCase(null);
        AssertThat(result).IsNull();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TestGenerateShipName()
    {
        // Test that ship names can be generated
        var shipName = NameGenerator.GenerateShipName();
        AssertThat(shipName).IsNotNull();
        AssertThat(shipName).IsNotEmpty();
        
        // Ship name should not be the fallback (which would indicate loading failed)
        AssertThat(shipName.StartsWith("Ship_")).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TestGenerateStationName()
    {
        // Test that station names can be generated
        var stationName = NameGenerator.GenerateStationName();
        AssertThat(stationName).IsNotNull();
        AssertThat(stationName).IsNotEmpty();
    }

    [TestCase]
    public void TestGenerateCombinedName()
    {
        var result = NameGenerator.GenerateCombinedName("iron veined", "Voyager");
        AssertThat(result).IsEqual("Iron-Veined Voyager");
        
        // Test with different adjective format
        result = NameGenerator.GenerateCombinedName("iron veined", "Voyager", NameFormat.TitleCase);
        AssertThat(result).IsEqual("Iron Veined Voyager");
    }

    [TestCase]
    public void TestPickNameWithFormat()
    {
        var nameDict = new Dictionary
        {
            ["test"] = new Array { "Test Name" }
        };

        // Test with NoSpaces format
        var name = NameGenerator.PickName(nameDict, NameFormat.NoSpaces);
        AssertThat(name).IsEqual("TestName");
        
        // Test with LowerCase format
        name = NameGenerator.PickName(nameDict, NameFormat.LowerCase);
        AssertThat(name).IsEqual("test name");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TestClearNameCache()
    {
        // Generate a ship name to ensure cache is populated
        var initialName = NameGenerator.GenerateShipName();
        AssertThat(initialName).IsNotEmpty();
        
        // Clear cache
        NameGenerator.ClearNameCache();
        
        // Generate another name - should still work after cache clear
        var newName = NameGenerator.GenerateShipName();
        AssertThat(newName).IsNotEmpty();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TestPreloadNameData()
    {
        // Clear any existing cache
        NameGenerator.ClearNameCache();
        
        // Preload data
        NameGenerator.PreloadNameData();
        
        // Generate names - should work without hitting file system again
        var shipName = NameGenerator.GenerateShipName();
        AssertThat(shipName).IsNotEmpty();
    }
}