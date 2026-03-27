#if DEBUG
using System.Collections.Generic;
using Godot;

namespace UI.Debug.Settings;

public partial class CategorySection : VBoxContainer
{
    private string? _categoryName;
    private Button? _headerButton;
    private VBoxContainer? _contentContainer;
    private PanelContainer? _contentPanel;
    private readonly Dictionary<string, SettingRow> _rows = new();
    private bool _isExpanded = true;

    public string? CategoryName => _categoryName;
    public bool IsExpanded => _isExpanded;

    public void Setup(string categoryName)
    {
        _categoryName = categoryName;
        BuildUI();
    }

    private void BuildUI()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 0);

        var headerPanel = new PanelContainer
        {
            Name = "HeaderPanel"
        };
        var headerStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.15f, 0.15f, 0.18f),
            ContentMarginLeft = 8,
            ContentMarginTop = 4,
            ContentMarginRight = 8,
            ContentMarginBottom = 4
        };
        headerPanel.AddThemeStyleboxOverride("panel", headerStyle);
        AddChild(headerPanel);

        _headerButton = new Button
        {
            Text = $"📁 {FormatCategoryName(_categoryName!)}",
            ToggleMode = true,
            ButtonPressed = _isExpanded,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = HorizontalAlignment.Left
        };
        _headerButton.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
        _headerButton.AddThemeColorOverride("font_hover_color", new Color(1f, 1f, 1f));
        _headerButton.Pressed += OnHeaderPressed;
        headerPanel.AddChild(_headerButton);

        _contentPanel = new PanelContainer
        {
            Name = "ContentPanel"
        };
        var contentStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.1f, 0.12f),
            BorderColor = new Color(0.2f, 0.2f, 0.22f),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 12,
            ContentMarginTop = 8,
            ContentMarginRight = 12,
            ContentMarginBottom = 8
        };
        _contentPanel.AddThemeStyleboxOverride("panel", contentStyle);
        AddChild(_contentPanel);

        _contentContainer = new VBoxContainer
        {
            Name = "ContentContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _contentContainer.AddThemeConstantOverride("separation", 6);
        _contentPanel.AddChild(_contentContainer);
    }

    public void AddSettingRow(SettingRow row)
    {
        if (row == null) return;

        string? key = row.Entry?.Key;
        if (!string.IsNullOrEmpty(key))
        {
            _rows[key] = row;
        }

        _contentContainer!.AddChild(row);
    }

    public void RemoveSettingRow(string key)
    {
        if (string.IsNullOrEmpty(key) || !_rows.TryGetValue(key, out var row)) return;

        _rows.Remove(key);
        if (row.IsInsideTree())
        {
            _contentContainer!.RemoveChild(row);
        }
        row.QueueFree();
    }

    public SettingRow? GetSettingRow(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        _rows.TryGetValue(key, out var row);
        return row;
    }

    public void UpdateRowValue(string key)
    {
        if (!string.IsNullOrEmpty(key) && _rows.TryGetValue(key, out var row))
        {
            row.UpdateValueDisplay();
        }
    }

    public void UpdateAllRows()
    {
        foreach (var row in _rows.Values)
        {
            row.UpdateValueDisplay();
        }
    }

    public void Expand()
    {
        _isExpanded = true;
        _headerButton!.ButtonPressed = true;
        _contentPanel!.Show();
    }

    public void Collapse()
    {
        _isExpanded = false;
        _headerButton!.ButtonPressed = false;
        _contentPanel!.Hide();
    }

    public void Toggle()
    {
        if (_isExpanded)
        {
            Collapse();
        }
        else
        {
            Expand();
        }
    }

    private void OnHeaderPressed()
    {
        _isExpanded = _headerButton!.ButtonPressed;
        _contentPanel!.Visible = _isExpanded;
    }

    private string FormatCategoryName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var result = new System.Text.StringBuilder();
        bool capitalizeNext = true;

        foreach (char c in name)
        {
            if (c == '_' || c == '-')
            {
                result.Append(' ');
                capitalizeNext = true;
            }
            else if (capitalizeNext)
            {
                result.Append(char.ToUpper(c));
                capitalizeNext = false;
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }
}
#endif
