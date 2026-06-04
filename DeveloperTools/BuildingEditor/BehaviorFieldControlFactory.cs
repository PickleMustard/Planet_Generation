#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace DeveloperTools.BuildingEditor;

/// <summary>
/// Builds a Godot Control for a single BehaviorFieldSpec, wired so user edits
/// flow through <paramref name="onValueChanged"/>. Shared between BehaviorRow
/// (config grid) and PlacementRequirementsSection (configurable_behavior config).
/// </summary>
public static class BehaviorFieldControlFactory
{
    public static Control Create(BehaviorFieldSpec spec, object? currentValue,
        Action<object?> onValueChanged)
    {
        switch (spec.FieldType)
        {
            case BehaviorFieldType.Float:
                return MakeFloatSpin(spec, currentValue, onValueChanged);
            case BehaviorFieldType.Int:
                return MakeIntSpin(spec, currentValue, onValueChanged);
            case BehaviorFieldType.Bool:
                return MakeBoolCheck(currentValue, onValueChanged);
            case BehaviorFieldType.String:
                return MakeLineEdit(currentValue, onValueChanged);
            case BehaviorFieldType.ResourceId:
                return MakeDropdown(BuildingEditorModel.GetAllResourceIds(),
                    currentValue, onValueChanged);
            case BehaviorFieldType.RecipeId:
                return MakeDropdown(BuildingEditorModel.GetAllRecipeIds(),
                    currentValue, onValueChanged);
            case BehaviorFieldType.StringList:
                return MakeStringListEditor(currentValue, onValueChanged,
                    BuildingEditorModel.GetAllResourceIds(), allowFreeText: true);
            case BehaviorFieldType.RecipeIdList:
                return MakeStringListEditor(currentValue, onValueChanged,
                    BuildingEditorModel.GetAllRecipeIds(), allowFreeText: false);
            case BehaviorFieldType.SlotFiltersDict:
            case BehaviorFieldType.StringIntDict:
                return MakeStringIntDictEditor(currentValue, onValueChanged);
        }
        return new Label { ThemeTypeVariation = "LabelHighContrast", Text = "?" };
    }

    private static SpinBox MakeFloatSpin(BehaviorFieldSpec spec, object? current, Action<object?> set)
    {
        var spin = new SpinBox
        {
            MinValue = spec.Min,
            MaxValue = spec.Max,
            Step = 0.01f,
            Value = ToFloat(current, spec.Default),
            AllowGreater = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        spin.ValueChanged += v => set((float)v);
        return spin;
    }

    private static SpinBox MakeIntSpin(BehaviorFieldSpec spec, object? current, Action<object?> set)
    {
        var spin = new SpinBox
        {
            MinValue = spec.Min,
            MaxValue = spec.Max,
            Step = 1,
            Value = ToInt(current, spec.Default),
            AllowGreater = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        spin.ValueChanged += v => set((int)v);
        return spin;
    }

    private static CheckBox MakeBoolCheck(object? current, Action<object?> set)
    {
        var cb = new CheckBox
        {
            ButtonPressed = ToBool(current),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        cb.Toggled += b => set(b);
        return cb;
    }

    private static LineEdit MakeLineEdit(object? current, Action<object?> set)
    {
        var le = new LineEdit
        {
            Text = current?.ToString() ?? "",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        le.TextChanged += t => set(t);
        return le;
    }

    private static Control MakeDropdown(List<string> options, object? current, Action<object?> set)
    {
        var hb = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var ob = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        ob.AddItem("(none)", 0);
        for (int i = 0; i < options.Count; i++)
            ob.AddItem(options[i], i + 1);

        string currentStr = current?.ToString() ?? "";
        int idx = options.IndexOf(currentStr);
        if (idx < 0 && !string.IsNullOrEmpty(currentStr))
        {
            ob.AddItem($"{currentStr} (unknown)", options.Count + 1);
            ob.Select(options.Count + 1);
        }
        else
        {
            ob.Select(idx + 1);
        }

        ob.ItemSelected += i =>
        {
            string s = ob.GetItemText((int)i);
            if (s == "(none)") set("");
            else if (s.EndsWith(" (unknown)")) set(s[..(s.Length - " (unknown)".Length)]);
            else set(s);
        };
        hb.AddChild(ob);
        return hb;
    }

    private static Control MakeStringListEditor(object? current, Action<object?> set,
        List<string> suggestions, bool allowFreeText)
    {
        var vb = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var currentList = ToStringList(current);
        var flow = new HFlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        vb.AddChild(flow);

        void Refresh()
        {
            foreach (var c in flow.GetChildren()) c.QueueFree();
            for (int i = 0; i < currentList.Count; i++)
            {
                string itemText = currentList[i];
                var pill = new Button { Text = $"{itemText}  ✕" };
                pill.AddThemeFontSizeOverride("font_size", 11);
                int captured = i;
                pill.Pressed += () =>
                {
                    if (captured < currentList.Count)
                    {
                        currentList.RemoveAt(captured);
                        set(new List<string>(currentList));
                        Refresh();
                    }
                };
                flow.AddChild(pill);
            }
        }

        var addRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var picker = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        picker.AddItem("(select to add)", 0);
        for (int i = 0; i < suggestions.Count; i++)
            picker.AddItem(suggestions[i], i + 1);
        picker.ItemSelected += i =>
        {
            string s = picker.GetItemText((int)i);
            if (s == "(select to add)") return;
            if (!currentList.Contains(s))
            {
                currentList.Add(s);
                set(new List<string>(currentList));
                Refresh();
            }
            picker.Select(0);
        };
        addRow.AddChild(picker);

        if (allowFreeText)
        {
            var le = new LineEdit
            {
                PlaceholderText = "or type and press Enter",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            le.TextSubmitted += s =>
            {
                if (string.IsNullOrWhiteSpace(s)) return;
                if (!currentList.Contains(s))
                {
                    currentList.Add(s);
                    set(new List<string>(currentList));
                    Refresh();
                }
                le.Text = "";
            };
            addRow.AddChild(le);
        }
        vb.AddChild(addRow);

        Refresh();
        return vb;
    }

    private static Control MakeStringIntDictEditor(object? current, Action<object?> set)
    {
        var vb = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var dict = ToStringIntDict(current);

        var rowsContainer = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        vb.AddChild(rowsContainer);

        void Rebuild()
        {
            foreach (var c in rowsContainer.GetChildren()) c.QueueFree();
            foreach (var kvp in dict.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                var key = new LineEdit
                {
                    Text = kvp.Key,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                };
                string capturedKey = kvp.Key;
                key.TextSubmitted += newKey =>
                {
                    if (string.IsNullOrWhiteSpace(newKey) || newKey == capturedKey) return;
                    if (dict.ContainsKey(newKey)) return;
                    int v = dict[capturedKey];
                    dict.Remove(capturedKey);
                    dict[newKey] = v;
                    set(new Dictionary<string, int>(dict));
                    Rebuild();
                };
                row.AddChild(key);

                var spin = new SpinBox
                {
                    MinValue = 0,
                    MaxValue = 1000000,
                    Step = 1,
                    Value = kvp.Value,
                    AllowGreater = true,
                    CustomMinimumSize = new Vector2(80, 0)
                };
                spin.ValueChanged += v =>
                {
                    dict[capturedKey] = (int)v;
                    set(new Dictionary<string, int>(dict));
                };
                row.AddChild(spin);

                var del = new Button { Text = "✕" };
                del.Pressed += () =>
                {
                    dict.Remove(capturedKey);
                    set(new Dictionary<string, int>(dict));
                    Rebuild();
                };
                row.AddChild(del);

                rowsContainer.AddChild(row);
            }
        }

        var addRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var addKey = new LineEdit
        {
            PlaceholderText = "key (any | state:solid | category:foo | resource:bar)",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        var addVal = new SpinBox
        {
            MinValue = 1,
            MaxValue = 1000000,
            Step = 1,
            Value = 1,
            AllowGreater = true,
            CustomMinimumSize = new Vector2(80, 0)
        };
        var addBtn = new Button { Text = "+" };
        addBtn.Pressed += () =>
        {
            string k = addKey.Text.Trim();
            if (string.IsNullOrEmpty(k) || dict.ContainsKey(k)) return;
            dict[k] = (int)addVal.Value;
            set(new Dictionary<string, int>(dict));
            addKey.Text = "";
            addVal.Value = 1;
            Rebuild();
        };
        addRow.AddChild(addKey);
        addRow.AddChild(addVal);
        addRow.AddChild(addBtn);
        vb.AddChild(addRow);

        Rebuild();
        return vb;
    }

    // ── Conversions ──────────────────────────────────────────────────────

    private static double ToFloat(object? v, object? fallback)
    {
        if (v is float f) return f;
        if (v is double d) return d;
        if (v is int i) return i;
        if (v is long l) return l;
        if (v is string s
            && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var r))
            return r;
        return ToFloatFallback(fallback);
    }

    private static double ToFloatFallback(object? f)
    {
        if (f is float ff) return ff;
        if (f is double dd) return dd;
        if (f is int ii) return ii;
        if (f is long ll) return ll;
        return 0;
    }

    private static int ToInt(object? v, object? fallback)
    {
        if (v is int i) return i;
        if (v is long l) return (int)l;
        if (v is float f) return (int)f;
        if (v is double d) return (int)d;
        if (v is string s
            && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r))
            return r;
        if (fallback is int fi) return fi;
        if (fallback is long fl) return (int)fl;
        return 0;
    }

    private static bool ToBool(object? v)
    {
        if (v is bool b) return b;
        if (v is string s && bool.TryParse(s, out var r)) return r;
        return false;
    }

    private static List<string> ToStringList(object? v)
    {
        if (v is List<string> ss) return new List<string>(ss);
        if (v is IEnumerable<string> ie) return new List<string>(ie);
        if (v is List<object> lo)
        {
            var list = new List<string>();
            foreach (var item in lo)
                if (item is string s) list.Add(s);
                else if (item != null) list.Add(item.ToString() ?? "");
            return list;
        }
        return new List<string>();
    }

    private static Dictionary<string, int> ToStringIntDict(object? v)
    {
        if (v is Dictionary<string, int> sid)
            return new Dictionary<string, int>(sid);
        if (v is Dictionary<object, object> oo)
        {
            var d = new Dictionary<string, int>();
            foreach (var kvp in oo)
            {
                string k = kvp.Key?.ToString() ?? "";
                int iv = kvp.Value switch
                {
                    int i => i,
                    long l => (int)l,
                    double dd => (int)dd,
                    float ff => (int)ff,
                    string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) => r,
                    _ => 0
                };
                if (!string.IsNullOrEmpty(k) && iv > 0) d[k] = iv;
            }
            return d;
        }
        return new Dictionary<string, int>();
    }
}
#endif
