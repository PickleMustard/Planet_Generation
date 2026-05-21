#if DEBUG
using System;
using Godot;
using UtilityLibrary;

namespace DeveloperTools.Settings;

public partial class SettingRow : HBoxContainer
{
    private ConfigEntry? _entry;
    private string? _category;
    private Control? _valueControl;
    private Label? _valueLabel;
    private Button? _resetButton;
    private Label? _restartWarning;
    private bool _isUpdating;

    public ConfigEntry? Entry => _entry;
    public string? Category => _category;

    public void Setup(string category, ConfigEntry entry)
    {
        _category = category;
        _entry = entry;

        BuildUI();
        ConnectSignals();
        UpdateValueDisplay();
    }

    private void BuildUI()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 8);

        var nameLabel = new Label
        {
            Text = FormatLabel(_entry!.Key!),
            CustomMinimumSize = new Vector2(150, 0),
            SizeFlagsHorizontal = SizeFlags.Fill,
            VerticalAlignment = VerticalAlignment.Center
        };
        nameLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
        AddChild(nameLabel);

        TooltipText = _entry.Description;

        _valueControl = CreateValueControl();
        if (_valueControl != null)
        {
            AddChild(_valueControl);
        }

        if (_entry!.ValueType == typeof(float) && _entry.MinValue != null && _entry.MaxValue != null)
        {
            _valueLabel = new Label
            {
                CustomMinimumSize = new Vector2(60, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            _valueLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.8f, 0.6f));
            AddChild(_valueLabel);
        }

        _resetButton = new Button
        {
            Text = "R",
            TooltipText = "Reset to default",
            CustomMinimumSize = new Vector2(28, 28)
        };
        AddChild(_resetButton);

        if (_entry.RequiresRestart)
        {
            _restartWarning = new Label
            {
                Text = "⚠",
                TooltipText = "Requires restart to take effect",
                VerticalAlignment = VerticalAlignment.Center
            };
            _restartWarning.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.2f));
            AddChild(_restartWarning);
        }
    }

    private Control CreateValueControl()
    {
        if (_entry!.ValueType == typeof(bool))
        {
            var checkBox = new CheckBox
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            return checkBox;
        }

        if (_entry.ValueType == typeof(int))
        {
            if (_entry.MinValue != null && _entry.MaxValue != null)
            {
                var spinBox = new SpinBox
                {
                    MinValue = Convert.ToDouble(_entry.MinValue),
                    MaxValue = Convert.ToDouble(_entry.MaxValue),
                    Step = 1.0,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill
                };
                return spinBox;
            }
            else
            {
                var lineEdit = new LineEdit
                {
                    SizeFlagsHorizontal = SizeFlags.ExpandFill
                };
                return lineEdit;
            }
        }

        if (_entry!.ValueType == typeof(float))
        {
            if (_entry.MinValue != null && _entry.MaxValue != null)
            {
                var slider = new HSlider
                {
                    MinValue = Convert.ToSingle(_entry!.MinValue),
                    MaxValue = Convert.ToSingle(_entry!.MaxValue),
                    Step = 0.01,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill
                };
                return slider;
            }
            else
            {
                var lineEdit = new LineEdit
                {
                    SizeFlagsHorizontal = SizeFlags.ExpandFill
                };
                return lineEdit;
            }
        }

        if (_entry!.ValidOptions != null && _entry!.ValidOptions.Length > 0)
        {
            var optionButton = new OptionButton
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };

            foreach (var option in _entry.ValidOptions)
            {
                optionButton.AddItem(option);
            }

            return optionButton;
        }

        if (_entry!.ValueType == typeof(string))
        {
            var lineEdit = new LineEdit
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            return lineEdit;
        }

        var defaultLabel = new Label
        {
            Text = "Unsupported type",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        defaultLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.4f, 0.4f));
        return defaultLabel;
    }

    private void ConnectSignals()
    {
        _resetButton!.Pressed += OnResetPressed;

        if (_valueControl is CheckBox checkBox)
        {
            checkBox.Toggled += OnBoolChanged;
        }
        else if (_valueControl is SpinBox spinBox)
        {
            spinBox.ValueChanged += OnIntChanged;
        }
        else if (_valueControl is HSlider slider)
        {
            slider.ValueChanged += OnFloatChanged;
        }
        else if (_valueControl is OptionButton optionButton)
        {
            optionButton.ItemSelected += OnEnumChanged;
        }
        else if (_valueControl is LineEdit lineEdit)
        {
            lineEdit.TextSubmitted += OnStringChanged;
            lineEdit.FocusExited += OnLineEditFocusExited;
        }
    }

    private void OnBoolChanged(bool pressed)
    {
        if (_isUpdating) return;
        RuntimeSettings.Instance?.SetSetting(_category!, _entry!.Key!, pressed);
    }

    private void OnIntChanged(double value)
    {
        if (_isUpdating) return;
        RuntimeSettings.Instance?.SetSetting(_category!, _entry!.Key!, (int)value);
    }

    private void OnFloatChanged(double value)
    {
        if (_isUpdating) return;
        RuntimeSettings.Instance?.SetSetting(_category!, _entry!.Key!, (float)value);
        UpdateValueLabel((float)value);
    }

    private void OnEnumChanged(long index)
    {
        if (_isUpdating) return;
        if (_valueControl is OptionButton optionButton && index >= 0 && index < optionButton.ItemCount)
        {
            string selectedOption = optionButton.GetItemText((int)index);
            RuntimeSettings.Instance?.SetSetting(_category!, _entry!.Key!, selectedOption);
        }
    }

    private void OnStringChanged(string text)
    {
        if (_isUpdating) return;

        if (_entry!.ValueType == typeof(int))
        {
            if (int.TryParse(text, out int intValue))
            {
                RuntimeSettings.Instance?.SetSetting(_category!, _entry.Key!, intValue);
            }
        }
        else if (_entry.ValueType == typeof(float))
        {
            if (float.TryParse(text, out float floatValue))
            {
                RuntimeSettings.Instance?.SetSetting(_category!, _entry.Key!, floatValue);
            }
        }
        else
        {
            RuntimeSettings.Instance?.SetSetting(_category!, _entry.Key!, text);
        }
    }

    private void OnLineEditFocusExited()
    {
        if (_valueControl is LineEdit lineEdit)
        {
            OnStringChanged(lineEdit.Text);
        }
    }

    private void OnResetPressed()
    {
        RuntimeSettings.Instance?.ResetSetting(_category!, _entry!.Key!);
        UpdateValueDisplay();
    }

    public void UpdateValueDisplay()
    {
        _isUpdating = true;

        if (RuntimeSettings.Instance == null)
        {
            _isUpdating = false;
            return;
        }

        if (_entry!.ValueType == typeof(bool))
        {
            bool value = RuntimeSettings.Instance.GetSetting<bool>(_category!, _entry.Key!);
            if (_valueControl is CheckBox checkBox)
            {
                checkBox.ButtonPressed = value;
            }
        }
        else if (_entry!.ValueType == typeof(int))
        {
            int value = RuntimeSettings.Instance.GetSetting<int>(_category!, _entry.Key!);
            if (_valueControl is SpinBox spinBox)
            {
                spinBox.Value = value;
            }
            else if (_valueControl is LineEdit lineEdit)
            {
                lineEdit.Text = value.ToString();
            }
        }
        else if (_entry!.ValueType == typeof(float))
        {
            float value = RuntimeSettings.Instance.GetSetting<float>(_category!, _entry.Key!);
            if (_valueControl is HSlider slider)
            {
                slider.Value = value;
            }
            else if (_valueControl is LineEdit lineEdit)
            {
                lineEdit.Text = value.ToString("F2");
            }
            UpdateValueLabel(value);
        }
        else if (_entry.ValidOptions != null && _entry.ValidOptions.Length > 0)
        {
            string? value = RuntimeSettings.Instance.GetSetting<string>(_category!, _entry.Key!);
            if (_valueControl is OptionButton optionButton)
            {
                for (int i = 0; i < optionButton.ItemCount; i++)
                {
                    if (optionButton.GetItemText(i) == value)
                    {
                        optionButton.Selected = i;
                        break;
                    }
                }
            }
        }
        else if (_entry!.ValueType == typeof(string))
        {
            string? value = RuntimeSettings.Instance.GetSetting<string>(_category!, _entry.Key!);
            if (_valueControl is LineEdit lineEdit)
            {
                lineEdit.Text = value ?? string.Empty;
            }
        }

        _isUpdating = false;
    }

    private void UpdateValueLabel(float value)
    {
        if (_valueLabel == null) return;

        if (_entry!.MaxValue != null && _entry.MinValue != null)
        {
            float min = Convert.ToSingle(_entry.MinValue);
            float max = Convert.ToSingle(_entry.MaxValue);
            float percentage = (value - min) / (max - min) * 100f;
            _valueLabel.Text = $"{percentage:F0}%";
        }
        else
        {
            _valueLabel.Text = value.ToString("F2");
        }
    }

    private static string FormatLabel(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;

        var result = new System.Text.StringBuilder();
        foreach (char c in key)
        {
            if (char.IsUpper(c) && result.Length > 0)
            {
                result.Append(' ');
            }
            result.Append(c);
        }
        return result.ToString();
    }
}
#endif
