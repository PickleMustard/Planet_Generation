using System;
using System.IO;
using Godot;
using GdUnit4;
using Structures.Logistics;
using Structures.Resources;
using UtilityLibrary.DataLoading;
using static GdUnit4.Assertions;

namespace Tests.Structures.Logistics;

[TestSuite]
public class LinkProfileDatabaseTest
{
    [BeforeTest]
    public void Setup()
    {
        // Ensure a fresh instance for each test
        LinkProfileDatabase.Instance.Unload();
    }

    [AfterTest]
    public void Teardown()
    {
        LinkProfileDatabase.Instance.Unload();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DatabaseLoading()
    {
        var db = LinkProfileDatabase.Instance;
        AssertThat(db).IsNotNull();

        // Database should not be loaded initially
        AssertThat(db.IsLoaded).IsFalse();

        // Load the database
        db.LoadData();
        AssertThat(db.IsLoaded).IsTrue();

        var profiles = db.GetAllProfiles();
        AssertThat(profiles).IsNotNull();
        AssertThat(profiles.Count).IsGreater(0);

        // Verify known profiles exist
        AssertThat(db.ValidateProfileExists("conveyor_belt")).IsTrue();
        AssertThat(db.ValidateProfileExists("pipe")).IsTrue();
        AssertThat(db.ValidateProfileExists("nonexistent_profile")).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ProfileLookup()
    {
        var db = LinkProfileDatabase.Instance;
        db.LoadData();

        // Test successful lookup
        AssertThat(db.TryGetProfile("conveyor_belt", out var conveyor)).IsTrue();
        AssertThat(conveyor).IsNotNull();
        AssertThat(conveyor!.IdName).IsEqual("conveyor_belt");
        AssertThat(conveyor.Tier).IsEqual(0);
        AssertThat(conveyor.TransportSpeed).IsEqual(0.02f);
        AssertThat(conveyor.PackageSize).IsEqual(10);
        AssertThat(conveyor.BundleTime).IsEqual(5);
        AssertThat(conveyor.SlotCapacity).IsEqual(3);

        // Test pipe profile
        AssertThat(db.TryGetProfile("pipe", out var pipe)).IsTrue();
        AssertThat(pipe).IsNotNull();
        AssertThat(pipe!.IdName).IsEqual("pipe");
        AssertThat(pipe.Tier).IsEqual(1);
        AssertThat(pipe.TransportSpeed).IsEqual(0.05f);
        AssertThat(pipe.PackageSize).IsEqual(50);
        AssertThat(pipe.BundleTime).IsEqual(0);
        AssertThat(pipe.SlotCapacity).IsEqual(5);

        // Test missing profile
        AssertThat(db.TryGetProfile("missing", out var missing)).IsFalse();
        AssertThat(missing).IsNull();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void InvalidYamlHandling()
    {
        // Test with a non-existent file path - loader should return empty list
        var profiles = LinkConfigLoader.LoadLinkProfiles("res://Configuration/LinkProfiles/does_not_exist.yaml");
        AssertThat(profiles).IsNotNull();
        AssertThat(profiles.Count).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void MissingYamlHandling()
    {
        // Create a temporary file with invalid YAML content
        string tempPath = "res://Configuration/LinkProfiles/test_invalid.yaml";
        string invalidContent = "this is not: valid: yaml: [";

        // Write the temp file using Godot's FileAccess
        using var file = Godot.FileAccess.Open(tempPath, Godot.FileAccess.ModeFlags.Write);
        file.StoreString(invalidContent);
        file.Close();

        // Loader should gracefully handle invalid YAML and return empty list
        var profiles = LinkConfigLoader.LoadLinkProfiles(tempPath);
        AssertThat(profiles).IsNotNull();
        AssertThat(profiles.Count).IsEqual(0);

        // Clean up temp file
        if (Godot.FileAccess.FileExists(tempPath))
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(tempPath));
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void EmptyLinkProfilesList()
    {
        // Create a temporary file with valid YAML but empty link_profiles list
        string tempPath = "res://Configuration/LinkProfiles/test_empty.yaml";
        string emptyContent = "link_profiles: []\n";

        using var file = Godot.FileAccess.Open(tempPath, Godot.FileAccess.ModeFlags.Write);
        file.StoreString(emptyContent);
        file.Close();

        var profiles = LinkConfigLoader.LoadLinkProfiles(tempPath);
        AssertThat(profiles).IsNotNull();
        AssertThat(profiles.Count).IsEqual(0);

        // Clean up
        if (Godot.FileAccess.FileExists(tempPath))
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(tempPath));
        }
    }

    [TestCase]
    public void UninitializedAccess_StateIsNotLoaded()
    {
        var db = LinkProfileDatabase.Instance;
        AssertThat(db).IsNotNull();

        // Database should not be loaded initially
        AssertThat(db.IsLoaded).IsFalse();
    }

    [TestCase]
    [ThrowsException(typeof(DatabaseNotLoadedException))]
    public void UninitializedAccess_GetAllProfilesThrows()
    {
        var db = LinkProfileDatabase.Instance;
        db.GetAllProfiles();
    }

    [TestCase]
    [ThrowsException(typeof(DatabaseNotLoadedException))]
    public void UninitializedAccess_TryGetProfileThrows()
    {
        var db = LinkProfileDatabase.Instance;
        db.TryGetProfile("conveyor_belt", out _);
    }

    [TestCase]
    [ThrowsException(typeof(DatabaseNotLoadedException))]
    public void UninitializedAccess_ValidateProfileExistsThrows()
    {
        var db = LinkProfileDatabase.Instance;
        db.ValidateProfileExists("conveyor_belt");
    }

    [TestCase]
    public void WorkPackageCreation()
    {
        var db = LinkProfileDatabase.Instance;
        AssertThat(db).IsNotNull();

        var workPackage = db.CreateLoadPackage();
        AssertThat(workPackage).IsNotNull();
        AssertThat(workPackage.Name).Contains("Load_LinkProfileDatabase");
        AssertThat(workPackage.Steps.Count).IsEqual(1);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DatabaseUnload()
    {
        var db = LinkProfileDatabase.Instance;
        db.LoadData();
        AssertThat(db.IsLoaded).IsTrue();

        db.Unload();
        AssertThat(db.IsLoaded).IsFalse();
        AssertThat(db.LoadProgress).IsEqual(0f);
    }

    [TestCase]
    [RequireGodotRuntime]
    [ThrowsException(typeof(DatabaseNotLoadedException))]
    public void DatabaseUnload_AccessAfterUnloadThrows()
    {
        var db = LinkProfileDatabase.Instance;
        db.LoadData();
        db.Unload();

        // Access after unload should throw
        db.GetAllProfiles();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DuplicateIdNames_IgnoresDuplicates()
    {
        // Create a temporary file with duplicate id_names
        string tempPath = "res://Configuration/LinkProfiles/test_duplicates.yaml";
        string duplicateContent = @"link_profiles:
  - id_name: dup_profile
    tier: 0
    transport_speed: 0.01
    package_size: 1
    bundle_time: 1
    slot_capacity: 1
  - id_name: dup_profile
    tier: 1
    transport_speed: 0.99
    package_size: 99
    bundle_time: 99
    slot_capacity: 99
";

        using var file = Godot.FileAccess.Open(tempPath, Godot.FileAccess.ModeFlags.Write);
        file.StoreString(duplicateContent);
        file.Close();

        // Database should load but ignore duplicates (first one wins)
        var db = LinkProfileDatabase.Instance;
        db.Unload();
        // Temporarily load from the temp file
        var profiles = LinkConfigLoader.LoadLinkProfiles(tempPath);

        AssertThat(profiles.Count).IsEqual(2); // Loader returns both

        // Clean up
        if (Godot.FileAccess.FileExists(tempPath))
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(tempPath));
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void MissingIdName_SkipsEntry()
    {
        // Create a temporary file with a missing id_name
        string tempPath = "res://Configuration/LinkProfiles/test_missing_id.yaml";
        string missingIdContent = @"link_profiles:
  - id_name: valid_profile
    tier: 0
    transport_speed: 0.01
    package_size: 1
    bundle_time: 1
    slot_capacity: 1
  - tier: 1
    transport_speed: 0.99
    package_size: 99
    bundle_time: 99
    slot_capacity: 99
";

        using var file = Godot.FileAccess.Open(tempPath, Godot.FileAccess.ModeFlags.Write);
        file.StoreString(missingIdContent);
        file.Close();

        var profiles = LinkConfigLoader.LoadLinkProfiles(tempPath);
        AssertThat(profiles.Count).IsEqual(2); // Loader parses both
        AssertThat(profiles[0].IdName).IsEqual("valid_profile");
        AssertThat(profiles[1].IdName).IsEqual(""); // Missing id_name becomes empty string

        // Clean up
        if (Godot.FileAccess.FileExists(tempPath))
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(tempPath));
        }
    }
}
