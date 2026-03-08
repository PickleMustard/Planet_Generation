using System;
using GdUnit4;
using static GdUnit4.Assertions;
using UtilityLibrary;

namespace Tests.Settings;

[TestSuite]
public class ConfigEntryTest
{
    [TestCase]
    public void NumericRangeValidationValidValue()
    {
        var entry = new ConfigEntry
        {
            Key = "TestInt",
            ValueType = typeof(int),
            DefaultValue = 50,
            MinValue = 0,
            MaxValue = 100,
            Description = "Test integer setting"
        };

        AssertThat(entry.IsValid(0)).IsTrue();
        AssertThat(entry.IsValid(50)).IsTrue();
        AssertThat(entry.IsValid(100)).IsTrue();
        AssertThat(entry.IsValid(25)).IsTrue();
    }

    [TestCase]
    public void NumericRangeValidationInvalidValue()
    {
        var entry = new ConfigEntry
        {
            Key = "TestInt",
            ValueType = typeof(int),
            DefaultValue = 50,
            MinValue = 0,
            MaxValue = 100,
            Description = "Test integer setting"
        };

        AssertThat(entry.IsValid(-1)).IsFalse();
        AssertThat(entry.IsValid(101)).IsFalse();
        AssertThat(entry.IsValid(-100)).IsFalse();
        AssertThat(entry.IsValid(200)).IsFalse();
    }

    [TestCase]
    public void FloatNumericRangeValidation()
    {
        var entry = new ConfigEntry
        {
            Key = "TestFloat",
            ValueType = typeof(float),
            DefaultValue = 0.5f,
            MinValue = 0.0f,
            MaxValue = 1.0f,
            Description = "Test float setting"
        };

        AssertThat(entry.IsValid(0.0f)).IsTrue();
        AssertThat(entry.IsValid(0.5f)).IsTrue();
        AssertThat(entry.IsValid(1.0f)).IsTrue();
        AssertThat(entry.IsValid(0.25f)).IsTrue();

        AssertThat(entry.IsValid(-0.1f)).IsFalse();
        AssertThat(entry.IsValid(1.1f)).IsFalse();
        AssertThat(entry.IsValid(-1.0f)).IsFalse();
        AssertThat(entry.IsValid(2.0f)).IsFalse();
    }

    [TestCase]
    public void DoubleNumericRangeValidation()
    {
        var entry = new ConfigEntry
        {
            Key = "TestDouble",
            ValueType = typeof(double),
            DefaultValue = 50.0,
            MinValue = 0.0,
            MaxValue = 100.0,
            Description = "Test double setting"
        };

        AssertThat(entry.IsValid(0.0)).IsTrue();
        AssertThat(entry.IsValid(50.0)).IsTrue();
        AssertThat(entry.IsValid(100.0)).IsTrue();

        AssertThat(entry.IsValid(-0.1)).IsFalse();
        AssertThat(entry.IsValid(100.1)).IsFalse();
    }

    [TestCase]
    public void EnumOptionsValidationValidValue()
    {
        var entry = new ConfigEntry
        {
            Key = "TestEnum",
            ValueType = typeof(string),
            DefaultValue = "OptionA",
            ValidOptions = new[] { "OptionA", "OptionB", "OptionC" },
            Description = "Test enum setting"
        };

        AssertThat(entry.IsValid("OptionA")).IsTrue();
        AssertThat(entry.IsValid("OptionB")).IsTrue();
        AssertThat(entry.IsValid("OptionC")).IsTrue();
    }

    [TestCase]
    public void EnumOptionsValidationInvalidValue()
    {
        var entry = new ConfigEntry
        {
            Key = "TestEnum",
            ValueType = typeof(string),
            DefaultValue = "OptionA",
            ValidOptions = new[] { "OptionA", "OptionB", "OptionC" },
            Description = "Test enum setting"
        };

        AssertThat(entry.IsValid("OptionD")).IsFalse();
        AssertThat(entry.IsValid("optiona")).IsFalse();
        AssertThat(entry.IsValid("Option A")).IsFalse();
        AssertThat(entry.IsValid("")).IsFalse();
    }

    [TestCase]
    public void EnumOptionsValidationCaseSensitive()
    {
        var entry = new ConfigEntry
        {
            Key = "TestEnum",
            ValueType = typeof(string),
            DefaultValue = "Low",
            ValidOptions = new[] { "Low", "Medium", "High" },
            Description = "Test case-sensitive enum"
        };

        AssertThat(entry.IsValid("Low")).IsTrue();
        AssertThat(entry.IsValid("low")).IsFalse();
        AssertThat(entry.IsValid("LOW")).IsFalse();
    }

    [TestCase]
    public void InvalidValueNullHandling()
    {
        var entry = new ConfigEntry
        {
            Key = "TestInt",
            ValueType = typeof(int),
            DefaultValue = 50,
            MinValue = 0,
            MaxValue = 100,
            Description = "Test setting"
        };

        AssertThat(entry.IsValid(null)).IsFalse();
    }

    [TestCase]
    public void InvalidValueTypeMismatch()
    {
        var intEntry = new ConfigEntry
        {
            Key = "TestInt",
            ValueType = typeof(int),
            DefaultValue = 50,
            MinValue = 0,
            MaxValue = 100,
            Description = "Test integer setting"
        };

        AssertThat(intEntry.IsValid("not an int")).IsFalse();
        AssertThat(intEntry.IsValid(50.5f)).IsFalse();
        AssertThat(intEntry.IsValid(true)).IsFalse();

        var stringEntry = new ConfigEntry
        {
            Key = "TestString",
            ValueType = typeof(string),
            DefaultValue = "default",
            Description = "Test string setting"
        };

        AssertThat(stringEntry.IsValid(123)).IsFalse();
        AssertThat(stringEntry.IsValid(true)).IsFalse();
    }

    [TestCase]
    public void NoValidationConstraints()
    {
        var entry = new ConfigEntry
        {
            Key = "TestString",
            ValueType = typeof(string),
            DefaultValue = "default",
            Description = "Test string setting without constraints"
        };

        AssertThat(entry.IsValid("any value")).IsTrue();
        AssertThat(entry.IsValid("another value")).IsTrue();
        AssertThat(entry.IsValid("")).IsTrue();
    }

    [TestCase]
    public void BooleanValidation()
    {
        var entry = new ConfigEntry
        {
            Key = "TestBool",
            ValueType = typeof(bool),
            DefaultValue = true,
            Description = "Test boolean setting"
        };

        AssertThat(entry.IsValid(true)).IsTrue();
        AssertThat(entry.IsValid(false)).IsTrue();
        AssertThat(entry.IsValid("true")).IsFalse();
        AssertThat(entry.IsValid(1)).IsFalse();
    }

    [TestCase]
    public void EmptyValidOptionsArray()
    {
        var entry = new ConfigEntry
        {
            Key = "TestEnum",
            ValueType = typeof(string),
            DefaultValue = "Any",
            ValidOptions = Array.Empty<string>(),
            Description = "Test with empty options array"
        };

        AssertThat(entry.IsValid("Any")).IsTrue();
        AssertThat(entry.IsValid("Something")).IsTrue();
    }

    [TestCase]
    public void OnlyMinValueSet()
    {
        var entry = new ConfigEntry
        {
            Key = "TestInt",
            ValueType = typeof(int),
            DefaultValue = 50,
            MinValue = 0,
            MaxValue = null,
            Description = "Test with only min value"
        };

        AssertThat(entry.IsValid(0)).IsTrue();
        AssertThat(entry.IsValid(100)).IsTrue();
        AssertThat(entry.IsValid(-1)).IsTrue();
    }

    [TestCase]
    public void OnlyMaxValueSet()
    {
        var entry = new ConfigEntry
        {
            Key = "TestInt",
            ValueType = typeof(int),
            DefaultValue = 50,
            MinValue = null,
            MaxValue = 100,
            Description = "Test with only max value"
        };

        AssertThat(entry.IsValid(100)).IsTrue();
        AssertThat(entry.IsValid(0)).IsTrue();
        AssertThat(entry.IsValid(101)).IsTrue();
    }

    [TestCase]
    public void PropertiesInitializedCorrectly()
    {
        var entry = new ConfigEntry
        {
            Key = "Volume",
            ValueType = typeof(float),
            DefaultValue = 0.8f,
            MinValue = 0.0f,
            MaxValue = 1.0f,
            Description = "Master volume level",
            RequiresRestart = false
        };

        AssertThat(entry.Key).IsEqual("Volume");
        AssertThat(entry.ValueType).IsEqual(typeof(float));
        AssertThat(entry.DefaultValue).IsEqual(0.8f);
        AssertThat(entry.MinValue).IsEqual(0.0f);
        AssertThat(entry.MaxValue).IsEqual(1.0f);
        AssertThat(entry.Description).IsEqual("Master volume level");
        AssertThat(entry.RequiresRestart).IsFalse();
    }

    [TestCase]
    public void RequiresRestartProperty()
    {
        var entryWithRestart = new ConfigEntry
        {
            Key = "Graphics",
            ValueType = typeof(string),
            DefaultValue = "High",
            ValidOptions = new[] { "Low", "Medium", "High" },
            Description = "Graphics quality",
            RequiresRestart = true
        };

        AssertThat(entryWithRestart.RequiresRestart).IsTrue();

        var entryWithoutRestart = new ConfigEntry
        {
            Key = "Volume",
            ValueType = typeof(float),
            DefaultValue = 0.5f,
            MinValue = 0.0f,
            MaxValue = 1.0f,
            Description = "Volume level",
            RequiresRestart = false
        };

        AssertThat(entryWithoutRestart.RequiresRestart).IsFalse();
    }

    [TestCase]
    public void BoundaryValuesInclusive()
    {
        var entry = new ConfigEntry
        {
            Key = "TestRange",
            ValueType = typeof(int),
            DefaultValue = 5,
            MinValue = 0,
            MaxValue = 10,
            Description = "Test boundary inclusive"
        };

        AssertThat(entry.IsValid(0)).IsTrue();
        AssertThat(entry.IsValid(10)).IsTrue();
        AssertThat(entry.IsValid(-1)).IsFalse();
        AssertThat(entry.IsValid(11)).IsFalse();
    }

    [TestCase]
    public void NegativeRangeValidation()
    {
        var entry = new ConfigEntry
        {
            Key = "NegativeRange",
            ValueType = typeof(int),
            DefaultValue = -50,
            MinValue = -100,
            MaxValue = -10,
            Description = "Test negative range"
        };

        AssertThat(entry.IsValid(-100)).IsTrue();
        AssertThat(entry.IsValid(-50)).IsTrue();
        AssertThat(entry.IsValid(-10)).IsTrue();
        AssertThat(entry.IsValid(-101)).IsFalse();
        AssertThat(entry.IsValid(-9)).IsFalse();
        AssertThat(entry.IsValid(0)).IsFalse();
    }
}
