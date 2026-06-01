using System.Collections.Generic;
using Godot;

namespace UI.Wireframe;

/// <summary>
/// Horizontal step breadcrumb (1 ▸ 2 ▸ 3 ▸ 4) with circular badges. Each step
/// can be Pending, Active, or Done.
/// </summary>
[GlobalClass]
public partial class StepIndicator : HBoxContainer
{
    public enum StepState { Pending, Active, Done }

    public sealed class Step
    {
        public string Label = string.Empty;
        public StepState State = StepState.Pending;
    }

    private List<Step> _steps = new();
    private readonly List<HBoxContainer> _entries = new();

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 14);
    }

    public void SetSteps(IList<Step> steps)
    {
        _steps = new List<Step>(steps);
        Rebuild();
    }

    public void SetState(int index, StepState state)
    {
        if (index < 0 || index >= _steps.Count) return;
        _steps[index].State = state;
        Rebuild();
    }

    private void Rebuild()
    {
        foreach (var c in GetChildren()) c.QueueFree();
        _entries.Clear();

        for (int i = 0; i < _steps.Count; i++)
        {
            var step = _steps[i];
            var entry = new HBoxContainer();
            entry.AddThemeConstantOverride("separation", 8);
            entry.Modulate = step.State == StepState.Pending ? new Color(1f, 1f, 1f, 0.5f) : Colors.White;
            AddChild(entry);
            _entries.Add(entry);

            var badge = new StepBadge
            {
                CustomMinimumSize = new Vector2(26f, 26f),
                Index = i + 1,
                State = step.State,
            };
            entry.AddChild(badge);

            var label = new Label
            {
                Text = step.Label,
                ThemeTypeVariation = "LabelHand",
            };
            label.AddThemeFontSizeOverride("font_size", 16);
            entry.AddChild(label);

            if (i < _steps.Count - 1)
            {
                var sep = new Label
                {
                    Text = "›",
                    ThemeTypeVariation = "LabelMono",
                };
                sep.AddThemeColorOverride("font_color", WireColors.InkFaint);
                AddChild(sep);
            }
        }
    }

    private partial class StepBadge : Control
    {
        public int Index { get; set; }
        public StepState State { get; set; }

        public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

        public override void _Draw()
        {
            float r = Mathf.Min(Size.X, Size.Y) * 0.5f - 1f;
            var center = Size * 0.5f;
            Color fill;
            Color textColor;
            switch (State)
            {
                case StepState.Active:
                    fill = WireColors.Orange;
                    textColor = WireColors.OnOrange;
                    break;
                case StepState.Done:
                    fill = WireColors.Ink;
                    textColor = WireColors.OnOrange;
                    break;
                default:
                    fill = WireColors.Paper;
                    textColor = WireColors.Ink;
                    break;
            }
            DrawCircle(center, r, fill);
            DrawArc(center, r, 0f, Mathf.Tau, 24, WireColors.Ink, 1.5f);
            string text = State == StepState.Done ? "✓" : Index.ToString();
            var font = ThemeDB.GetProjectTheme()?.DefaultFont;
            if (font != null)
            {
                int fs = 13;
                var size = font.GetStringSize(text, HorizontalAlignment.Center, -1f, fs);
                DrawString(font, center + new Vector2(-size.X * 0.5f, size.Y * 0.3f), text, HorizontalAlignment.Center, -1f, fs, textColor);
            }
        }
    }
}
