using System;
using System.Collections.Generic;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using UtilityLibrary;

namespace Tests.Settings;

[TestSuite]
[RequireGodotRuntime]
public class RuntimeSettingsTest
{
    private class MockConfigurable : IConfigurable
    {
        public string SettingsCategory => "TestCategory";
        public Dictionary<string, object> AppliedSettings { get; } = new();
        private readonly List<ConfigEntry> _entries = new();

        public MockConfigurable()
        {
            _entries.Add(new ConfigEntry
            {
                Key = "IntSetting",
                ValueType = typeof(int),
                DefaultValue = 42,
                MinValue = 0,
                MaxValue = 100,
                Description = "An integer setting"
            });

            _entries.Add(new ConfigEntry
            {
                Key = "FloatSetting",
                ValueType = typeof(float),
                DefaultValue = 0.5f,
                MinValue = 0.0f,
                MaxValue = 1.0f,
                Description = "A float setting"
            });

            _entries.Add(new ConfigEntry
            {
                Key = "StringSetting",
                ValueType = typeof(string),
                DefaultValue = "default",
                Description = "A string setting"
            });

            _entries.Add(new ConfigEntry
            {
                Key = "EnumSetting",
                ValueType = typeof(string),
                DefaultValue = "OptionA",
                ValidOptions = new[] { "OptionA", "OptionB", "OptionC" },
                Description = "An enum-like setting"
            });

            _entries.Add(new ConfigEntry
            {
                Key = "BoolSetting",
                ValueType = typeof(bool),
                DefaultValue = true,
                Description = "A boolean setting"
            });
        }

        public void ApplySetting(string key, object value)
        {
            AppliedSettings[key] = value;
        }

        public object? GetSettingDefault(string key)
        {
            foreach (var entry in _entries)
            {
                if (entry.Key == key)
                {
                    return entry.DefaultValue;
                }
            }
            return null;
        }

        public IEnumerable<ConfigEntry> GetConfigEntries()
        {
            return _entries;
        }
    }

#pragma warning disable CS8618
    private RuntimeSettings _settings;
    private MockConfigurable _mockConfigurable;
#pragma warning restore CS8618

    [Before]
    public void Setup()
    {
        _settings = new RuntimeSettings();
        _mockConfigurable = new MockConfigurable();
    }

    [TestCase]
    public void SingletonInstanceAccessible()
    {
        AssertThat(RuntimeSettings.Instance).IsNotNull();
    }

    [TestCase]
    public void RegisterConfigurableRegistersCorrectly()
    {
        _settings.RegisterConfigurable(_mockConfigurable);

        var retrieved = _settings.GetConfigurable("TestCategory");
        AssertThat(retrieved).IsNotNull();
        AssertThat(retrieved).IsEqual(_mockConfigurable);
    }

    [TestCase]
    public void RegisterConfigurableNullHandling()
    {
        _settings.RegisterConfigurable(null!);
        var retrieved = _settings.GetConfigurable("NonExistent");
        AssertThat(retrieved).IsNull();
    }

    [TestCase]
    public void GetSettingReturnsDefaultWhenNotSet()
    {
        _settings.RegisterConfigurable(_mockConfigurable);

        int intValue = _settings.GetSetting<int>("TestCategory", "IntSetting");
        AssertThat(intValue).IsEqual(42);

        float floatValue = _settings.GetSetting<float>("TestCategory", "FloatSetting");
        AssertThat(floatValue).IsEqual(0.5f);

        string? stringValue = _settings.GetSetting<string>("TestCategory", "StringSetting");
        AssertThat(stringValue).IsEqual("default");

        bool boolValue = _settings.GetSetting<bool>("TestCategory", "BoolSetting");
        AssertThat(boolValue).IsTrue();
    }

    [TestCase]
    public void GetSettingReturnsDefaultForInvalidCategory()
    {
        int result = _settings.GetSetting<int>("NonExistentCategory", "SomeKey");
        AssertThat(result).IsEqual(0);
    }

    [TestCase]
    public void GetSettingReturnsDefaultForInvalidKey()
    {
        _settings.RegisterConfigurable(_mockConfigurable);

        int result = _settings.GetSetting<int>("TestCategory", "NonExistentKey");
        AssertThat(result).IsEqual(0);
    }

    [TestCase]
    public void GetSettingHandlesNullOrEmptyParameters()
    {
        int result1 = _settings.GetSetting<int>(null!, "SomeKey");
        AssertThat(result1).IsEqual(0);

        int result2 = _settings.GetSetting<int>("TestCategory", null!);
        AssertThat(result2).IsEqual(0);

        int result3 = _settings.GetSetting<int>("", "");
        AssertThat(result3).IsEqual(0);
    }

    [TestCase]
    public void SetSettingAppliesAndPersists()
    {
        _settings.RegisterConfigurable(_mockConfigurable);

        _settings.SetSetting("TestCategory", "IntSetting", 75);

        int result = _settings.GetSetting<int>("TestCategory", "IntSetting");
        AssertThat(result).IsEqual(75);

        AssertThat(_mockConfigurable.AppliedSettings.ContainsKey("IntSetting")).IsTrue();
        AssertThat(_mockConfigurable.AppliedSettings["IntSetting"]).IsEqual(75);
    }

    [TestCase]
    public void SetSettingValidatesNumericRange()
    {
        _settings.RegisterConfigurable(_mockConfigurable);

        _settings.SetSetting("TestCategory", "IntSetting", 150);

        int result = _settings.GetSetting<int>("TestCategory", "IntSetting");
        AssertThat(result).IsEqual(42);
    }

    [TestCase]
    public void SetSettingValidatesEnumOptions()
    {
        _settings.RegisterConfigurable(_mockConfigurable);

        _settings.SetSetting("TestCategory", "EnumSetting", "OptionB");
        string? result1 = _settings.GetSetting<string>("TestCategory", "EnumSetting");
        AssertThat(result1).IsEqual("OptionB");

        _settings.SetSetting("TestCategory", "EnumSetting", "InvalidOption");
        string? result2 = _settings.GetSetting<string>("TestCategory", "EnumSetting");
        AssertThat(result2).IsEqual("OptionB");
    }

    [TestCase]
    public void SetSettingHandlesNullOrEmptyParameters()
    {
        _settings.RegisterConfigurable(_mockConfigurable);

        _settings.SetSetting(null!, "IntSetting", 50);
        _settings.SetSetting("TestCategory", null!, 50);
        _settings.SetSetting("", "", 50);
        _settings.SetSetting("TestCategory", "IntSetting", null!);

        int result = _settings.GetSetting<int>("TestCategory", "IntSetting");
        AssertThat(result).IsEqual(42);
    }

    [TestCase]
    public void ResetSettingRestoresDefault()
    {
        _settings.RegisterConfigurable(_mockConfigurable);

        _settings.SetSetting("TestCategory", "IntSetting", 75);
        int afterSet = _settings.GetSetting<int>("TestCategory", "IntSetting");
        AssertThat(afterSet).IsEqual(75);

        _settings.ResetSetting("TestCategory", "IntSetting");
        int afterReset = _settings.GetSetting<int>("TestCategory", "IntSetting");
        AssertThat(afterReset).IsEqual(42);
    }

    [TestCase]
    public void ResetSettingHandlesInvalidCategory()
    {
        _settings.ResetSetting("NonExistentCategory", "SomeKey");
    }

    [TestCase]
    public void ResetSettingHandlesNullOrEmptyParameters()
    {
        _settings.RegisterConfigurable(_mockConfigurable);

        _settings.ResetSetting(null!, "IntSetting");
        _settings.ResetSetting("TestCategory", null!);
        _settings.ResetSetting("", "");
    }

    [TestCase]
    public void SaveToFileCreatesSettingsFile()
    {
        _settings.RegisterConfigurable(_mockConfigurable);
        _settings.SetSetting("TestCategory", "IntSetting", 80);

        _settings.SaveToFile();

        AssertThat(FileAccess.FileExists("user://settings.cfg")).IsTrue();
    }

    [TestCase]
    public void LoadFromFileReadsSettingsFile()
    {
        _settings.RegisterConfigurable(_mockConfigurable);
        _settings.SetSetting("TestCategory", "IntSetting", 80);
        _settings.SaveToFile();

        var newSettings = new RuntimeSettings();
        newSettings.RegisterConfigurable(_mockConfigurable);
        newSettings.LoadFromFile();

        int result = newSettings.GetSetting<int>("TestCategory", "IntSetting");
        AssertThat(result).IsEqual(80);
    }

    [TestCase]
    public void LoadFromFileHandlesMissingFile()
    {
        if (FileAccess.FileExists("user://settings.cfg"))
        {
            DirAccess.RemoveAbsolute("user://settings.cfg");
        }

        var newSettings = new RuntimeSettings();
        newSettings.LoadFromFile();

        AssertThat(newSettings.IsLoaded()).IsTrue();
    }

    [TestCase]
    public void SettingChangedSignalFiresCorrectly()
    {
        _settings.RegisterConfigurable(_mockConfigurable);

        string? signalCategory = null;
        string? signalKey = null;
        Variant signalValue = default;

        _settings.SettingChanged += (category, key, value) =>
        {
            signalCategory = category;
            signalKey = key;
            signalValue = value;
        };

        _settings.SetSetting("TestCategory", "IntSetting", 55);

        AssertThat(signalCategory).IsEqual("TestCategory");
        AssertThat(signalKey).IsEqual("IntSetting");
        AssertThat(signalValue.AsInt32()).IsEqual(55);
    }

    [TestCase]
    public void HasSettingReturnsCorrectValue()
    {
        _settings.RegisterConfigurable(_mockConfigurable);

        AssertThat(_settings.HasSetting("TestCategory", "IntSetting")).IsTrue();
        AssertThat(_settings.HasSetting("TestCategory", "NonExistentKey")).IsFalse();
        AssertThat(_settings.HasSetting("NonExistentCategory", "IntSetting")).IsFalse();
    }

    [TestCase]
    public void GetAllEntriesReturnsAllConfigEntries()
    {
        _settings.RegisterConfigurable(_mockConfigurable);

        int count = 0;
        foreach (var entry in _settings.GetAllEntries())
        {
            count++;
            AssertThat(entry).IsNotNull();
            AssertThat(entry.Key).IsNotNull();
        }

        AssertThat(count).IsEqual(5);
    }

    [TestCase]
    public void IsLoadedReturnsCorrectState()
    {
        AssertThat(_settings.IsLoaded()).IsFalse();

        _settings.LoadFromFile();

        AssertThat(_settings.IsLoaded()).IsTrue();
    }

    [TestCase]
    public void GetConfigurableReturnsNullForUnknownCategory()
    {
        var result = _settings.GetConfigurable("UnknownCategory");
        AssertThat(result).IsNull();
    }

    [TestCase]
    public void EndToEndSettingPersistence()
    {
        _settings.RegisterConfigurable(_mockConfigurable);

        _settings.SetSetting("TestCategory", "IntSetting", 65);
        _settings.SetSetting("TestCategory", "FloatSetting", 0.75f);
        _settings.SetSetting("TestCategory", "StringSetting", "modified");
        _settings.SetSetting("TestCategory", "BoolSetting", false);
        _settings.SetSetting("TestCategory", "EnumSetting", "OptionC");

        _settings.SaveToFile();

        var newSettings = new RuntimeSettings();
        var newConfigurable = new MockConfigurable();
        newSettings.RegisterConfigurable(newConfigurable);
        newSettings.LoadFromFile();

        AssertThat(newSettings.GetSetting<int>("TestCategory", "IntSetting")).IsEqual(65);
        AssertThat(newSettings.GetSetting<float>("TestCategory", "FloatSetting")).IsEqual(0.75f);
        AssertThat(newSettings.GetSetting<string>("TestCategory", "StringSetting")).IsEqual("modified");
        AssertThat(newSettings.GetSetting<bool>("TestCategory", "BoolSetting")).IsFalse();
        AssertThat(newSettings.GetSetting<string>("TestCategory", "EnumSetting")).IsEqual("OptionC");
    }

    [TestCase]
    public void EndToEndResetAllSettings()
    {
        _settings.RegisterConfigurable(_mockConfigurable);

        _settings.SetSetting("TestCategory", "IntSetting", 65);
        _settings.SetSetting("TestCategory", "FloatSetting", 0.75f);
        _settings.SetSetting("TestCategory", "StringSetting", "modified");

        _settings.ResetAllSettings();

        AssertThat(_settings.GetSetting<int>("TestCategory", "IntSetting")).IsEqual(42);
        AssertThat(_settings.GetSetting<float>("TestCategory", "FloatSetting")).IsEqual(0.5f);
        AssertThat(_settings.GetSetting<string>("TestCategory", "StringSetting")).IsEqual("default");
    }

    [TestCase]
    public void TypeConversionHandlesVariousTypes()
    {
        _settings.RegisterConfigurable(_mockConfigurable);

        _settings.SetSetting("TestCategory", "IntSetting", 50);
        int intResult = _settings.GetSetting<int>("TestCategory", "IntSetting");
        AssertThat(intResult).IsEqual(50);

        _settings.SetSetting("TestCategory", "FloatSetting", 0.25f);
        float floatResult = _settings.GetSetting<float>("TestCategory", "FloatSetting");
        AssertThat(floatResult).IsEqual(0.25f);

        _settings.SetSetting("TestCategory", "StringSetting", "test");
        string? stringResult = _settings.GetSetting<string>("TestCategory", "StringSetting");
        AssertThat(stringResult).IsEqual("test");

        _settings.SetSetting("TestCategory", "BoolSetting", false);
        bool boolResult = _settings.GetSetting<bool>("TestCategory", "BoolSetting");
        AssertThat(boolResult).IsFalse();
    }
}
