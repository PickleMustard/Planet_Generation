#if DEBUG
using System;
using System.Globalization;
using Godot;

namespace DeveloperTools.BiomeEditor.Cards;

/// <summary>
/// Per-body-type atmosphere range editor. Reads possible_subtypes for context (read-only).
/// </summary>
public partial class AtmosphereTemplateCard : PanelContainer
{
    private BiomeEditorModel _model = null!;
    private string _typeName = "";
    private LineEdit _minEdit = null!;
    private LineEdit _maxEdit = null!;

    public void Initialize(BiomeEditorModel model, string typeName)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _typeName = typeName;
        BuildLayout();
        Refresh();
    }

    private void BuildLayout()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.14f, 0.14f, 0.18f), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 6 });
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(root);

        var title = new Label { Text = _typeName };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", new Color(0.7f, 0.85f, 1f));
        root.AddChild(title);

        var row = new HBoxContainer();
        row.AddChild(new Label { Text = "atmosphere min:" });
        _minEdit = new LineEdit { CustomMinimumSize = new Vector2(80, 0) };
        _minEdit.TextSubmitted += s => Commit();
        _minEdit.FocusExited += Commit;
        row.AddChild(_minEdit);
        row.AddChild(new Label { Text = "max:" });
        _maxEdit = new LineEdit { CustomMinimumSize = new Vector2(80, 0) };
        _maxEdit.TextSubmitted += s => Commit();
        _maxEdit.FocusExited += Commit;
        row.AddChild(_maxEdit);
        root.AddChild(row);

        if (_model.AtmosphereTemplates.TryGetValue(_typeName, out var t) && t.PossibleSubtypes.Count > 0)
        {
            var subtitle = new Label { Text = "possible_subtypes: " + string.Join(", ", t.PossibleSubtypes) };
            subtitle.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            subtitle.AddThemeFontSizeOverride("font_size", 11);
            root.AddChild(subtitle);
        }
    }

    private void Refresh()
    {
        if (!_model.AtmosphereTemplates.TryGetValue(_typeName, out var t)) return;
        _minEdit.Text = t.AtmosphereMin.ToString("0.##", CultureInfo.InvariantCulture);
        _maxEdit.Text = t.AtmosphereMax.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private void Commit()
    {
        if (!float.TryParse(_minEdit.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var min)) return;
        if (!float.TryParse(_maxEdit.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var max)) return;
        _model.UpdateAtmosphereTemplate(_typeName, min, max);
    }
}
#endif
