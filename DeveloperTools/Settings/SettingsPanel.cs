#if DEBUG
using System.Collections.Generic;
using Godot;
using UtilityLibrary;
using Debug;

namespace DeveloperTools.Settings;

public partial class SettingsPanel : BaseDebugModule
{
    public override string ModuleName => "Settings";

    private ScrollContainer? _scrollContainer;
    private VBoxContainer? _settingsContainer;
    private HBoxContainer? _buttonContainer;
    private readonly Dictionary<string, CategorySection> _categories = new();

    public override void _Ready()
    {
        base._Ready();
        BuildUI();
        ConnectRuntimeSettingsSignal();
        BuildSettingsFromRegistry();
    }

    private void BuildUI()
    {
        AnchorRight = 1f;
        AnchorBottom = 1f;
        OffsetRight = 0;
        OffsetBottom = 0;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        var mainContainer = new VBoxContainer
        {
            Name = "MainContainer",
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetRight = 0,
            OffsetBottom = 0,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        AddChild(mainContainer);

        var headerPanel = new PanelContainer
        {
            Name = "HeaderPanel"
        };
        var headerStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.14f),
            ContentMarginLeft = 12,
            ContentMarginTop = 8,
            ContentMarginRight = 12,
            ContentMarginBottom = 8
        };
        headerPanel.AddThemeStyleboxOverride("panel", headerStyle);
        mainContainer.AddChild(headerPanel);

        var headerHBox = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        headerPanel.AddChild(headerHBox);

        var iconLabel = new Label
        { ThemeTypeVariation = "LabelHighContrast",
            Text = "⚙",
            VerticalAlignment = VerticalAlignment.Center
        };
        iconLabel.AddThemeFontSizeOverride("font_size", 18);
        headerHBox.AddChild(iconLabel);

        var titleLabel = new Label
        {
            Text = " Runtime Settings",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 16);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
        headerHBox.AddChild(titleLabel);

        _scrollContainer = new ScrollContainer
        {
            Name = "ScrollContainer",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        mainContainer.AddChild(_scrollContainer);

        _settingsContainer = new VBoxContainer
        {
            Name = "SettingsContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _settingsContainer.AddThemeConstantOverride("separation", 8);
        _scrollContainer.AddChild(_settingsContainer);

        _buttonContainer = new HBoxContainer
        {
            Name = "ButtonContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _buttonContainer.AddThemeConstantOverride("separation", 8);

        var buttonPanel = new PanelContainer();
        var buttonStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.14f),
            ContentMarginLeft = 12,
            ContentMarginTop = 8,
            ContentMarginRight = 12,
            ContentMarginBottom = 8
        };
        buttonPanel.AddThemeStyleboxOverride("panel", buttonStyle);
        buttonPanel.AddChild(_buttonContainer);
        mainContainer.AddChild(buttonPanel);

        var resetAllButton = new Button
        {
            Text = "Reset All",
            TooltipText = "Reset all settings to their default values"
        };
        resetAllButton.Pressed += OnResetAllPressed;
        _buttonContainer.AddChild(resetAllButton);

        var reloadButton = new Button
        {
            Text = "Reload",
            TooltipText = "Reload settings from the configuration file"
        };
        reloadButton.Pressed += OnReloadPressed;
        _buttonContainer.AddChild(reloadButton);

        var rebuildManifestsButton = new Button
        {
            Text = "Rebuild Asset Manifests",
            TooltipText = "Rescan asset directories and rewrite the icon/model/audio export manifests"
        };
        rebuildManifestsButton.Pressed += OnRebuildManifestsPressed;
        _buttonContainer.AddChild(rebuildManifestsButton);

        var spacer = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _buttonContainer.AddChild(spacer);

        var saveButton = new Button
        {
            Text = "Save",
            TooltipText = "Save current settings to the configuration file"
        };
        saveButton.Pressed += OnSavePressed;
        _buttonContainer.AddChild(saveButton);
    }

    private void ConnectRuntimeSettingsSignal()
    {
        // SettingsAccess handles provider routing automatically
        // Note: Signal subscription is not supported in editor context
#if !TOOLS
		var runtimeSettings = RuntimeSettings.Instance;
		if (runtimeSettings != null)
		{
			runtimeSettings.SettingChanged += OnSettingChanged;
		}
#endif
    }

    private void BuildSettingsFromRegistry()
    {
        if (RuntimeSettings.Instance == null)
        {
            GameLogger.Warning("Settings provider not available");
            return;
        }

        var entriesByCategory = new Dictionary<string, List<ConfigEntry>>();

        foreach (var entry in RuntimeSettings.Instance.GetAllEntries())
        {
            var configurable = RuntimeSettings.Instance.GetConfigurable(entry.Key!);
            string category = FindCategoryForEntry(entry);

            if (!entriesByCategory.TryGetValue(category, out var list))
            {
                list = new List<ConfigEntry>();
                entriesByCategory[category] = list;
            }
            list.Add(entry);
        }

        foreach (var kvp in entriesByCategory)
        {
            string category = kvp.Key;
            var entries = kvp.Value;

            var section = new CategorySection();
            section.Setup(category);
            _settingsContainer!.AddChild(section);
            _categories[category] = section;

            foreach (var entry in entries)
            {
                var row = new SettingRow();
                row.Setup(category, entry);
                section.AddSettingRow(row);
            }
        }
    }

    private string FindCategoryForEntry(ConfigEntry entry)
    {
        if (RuntimeSettings.Instance == null) return "General";

        foreach (var configurableEntry in RuntimeSettings.Instance.GetAllEntries())
        {
            if (configurableEntry.Key == entry.Key)
            {
                var configurable = RuntimeSettings.Instance.GetConfigurable(entry.Key!);
                if (configurable != null)
                {
                    return configurable.SettingsCategory;
                }
            }
        }

        return "General";
    }

    private void OnSettingChanged(string category, string key, Variant value)
    {
        if (_categories.TryGetValue(category, out var section))
        {
            section.UpdateRowValue(key);
        }
    }

    private void OnResetAllPressed()
    {
        RuntimeSettings.Instance?.ResetAllSettings();

        foreach (var section in _categories.Values)
        {
            section.UpdateAllRows();
        }
    }

    private void OnReloadPressed()
    {
        RuntimeSettings.Instance?.LoadFromFile();

        foreach (var section in _categories.Values)
        {
            section.UpdateAllRows();
        }
    }

    private void OnSavePressed()
    {
        // ProjectSettings auto-saves, no explicit save needed
        GameLogger.Info("Settings auto-saved to ProjectSettings");
    }

    private void OnRebuildManifestsPressed()
    {
        bool ok = DeveloperTools.Common.AssetManifestBuilder.RebuildAll();
        GameLogger.Info(ok
            ? "Asset manifests rebuilt successfully."
            : "Asset manifest rebuild completed with errors — check the log.");
    }

    public override void OnModuleEnabled()
    {
        base.OnModuleEnabled();

        foreach (var section in _categories.Values)
        {
            section.UpdateAllRows();
        }
    }

    public override void _ExitTree()
    {
        if (RuntimeSettings.Instance != null)
        {
            RuntimeSettings.Instance.SettingChanged -= OnSettingChanged;
        }
        base._ExitTree();
    }
}
#endif
