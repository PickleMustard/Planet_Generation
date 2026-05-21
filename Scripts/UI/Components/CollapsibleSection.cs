using System;
using Godot;

namespace UI.Components;

public partial class CollapsibleSection : VBoxContainer
{
    private readonly string _title;
    private int _count;
    private readonly Button _header;
    private readonly VBoxContainer _body;
    private readonly Action<bool>? _onToggle;

    public VBoxContainer Body => _body;
    public bool IsCollapsed { get; private set; }

    public CollapsibleSection(string title, int count, bool startCollapsed, Action<bool>? onToggle = null)
    {
        _title = title;
        _count = count;
        _onToggle = onToggle;
        IsCollapsed = startCollapsed;

        AddThemeConstantOverride("separation", 2);

        _header = new Button
        {
            ThemeTypeVariation = "TabInactive",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = HorizontalAlignment.Left,
            Text = HeaderText(),
        };
        _header.Pressed += Toggle;
        AddChild(_header);

        _body = new VBoxContainer { Visible = !startCollapsed };
        _body.AddThemeConstantOverride("separation", 2);
        AddChild(_body);
    }

    public void SetCount(int count)
    {
        _count = count;
        _header.Text = HeaderText();
    }

    private void Toggle()
    {
        IsCollapsed = !IsCollapsed;
        _body.Visible = !IsCollapsed;
        _header.Text = HeaderText();
        _onToggle?.Invoke(IsCollapsed);
    }

    private string HeaderText() => (IsCollapsed ? "▸ " : "▾ ") + $"{_title} ({_count})";
}
