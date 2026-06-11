#if DEBUG
using System.Collections.Generic;
using Godot;

namespace DeveloperTools.Settings;

/// <summary>
/// Collapsible settings category. Layout lives in <c>CategorySection.tscn</c>;
/// this script wires the header toggle and manages contained <see cref="SettingRow"/>s.
/// Instantiate via <see cref="Create"/>.
/// </summary>
public partial class CategorySection : VBoxContainer
{
    private string? _categoryName;
    [Export] private Button? _headerButton;
    [Export] private VBoxContainer? _contentContainer;
    [Export] private PanelContainer? _contentPanel;
    private readonly Dictionary<string, SettingRow> _rows = new();
    private bool _isExpanded = true;

    public string? CategoryName => _categoryName;
    public bool IsExpanded => _isExpanded;

    private static PackedScene? _scene;

    public static CategorySection Create(string categoryName)
    {
        _scene ??= GD.Load<PackedScene>("res://DeveloperTools/Settings/CategorySection.tscn");
        var section = _scene.Instantiate<CategorySection>();
        section.Setup(categoryName);
        return section;
    }

    public void Setup(string categoryName)
    {
        _categoryName = categoryName;
        if (_headerButton != null)
        {
            _headerButton.Text = $"📁 {FormatCategoryName(_categoryName)}";
            _headerButton.ButtonPressed = _isExpanded;
            _headerButton.Pressed += OnHeaderPressed;
        }
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
