using Godot;

namespace UI;

public partial class RangeControl : HBoxContainer
{
    [Signal]
    public delegate void ValueChangedEventHandler(float lowerValue, float upperValue);

    [Export]
    public Label ParameterLabel;

    [Export]
    public SpinBox LowerBound;

    [Export]
    public SpinBox UpperBound;

    [Export]
    public Label SeparatorLabel;

    private bool _isUpdating = false;

    public override void _EnterTree()
    {
        // Cache nodes if not set via exported references
        ParameterLabel ??= GetNodeOrNull<Label>("ParameterLabel");
        LowerBound ??= GetNodeOrNull<SpinBox>("LowerBound");
        UpperBound ??= GetNodeOrNull<SpinBox>("UpperBound");
        SeparatorLabel ??= GetNodeOrNull<Label>("SeparatorLabel");

        // Connect value change events
        if (LowerBound != null)
        {
            LowerBound.ValueChanged += OnLowerBoundChanged;
        }
        if (UpperBound != null)
        {
            UpperBound.ValueChanged += OnUpperBoundChanged;
        }
    }

    public void Setup(string parameterName, float minRange, float maxRange, float step = 0.1f, float initialLower = 0f, float initialUpper = 0f)
    {
        if (ParameterLabel != null)
        {
            ParameterLabel.Text = parameterName;
        }

        if (LowerBound != null && UpperBound != null)
        {
            // Set ranges
            LowerBound.MinValue = minRange;
            LowerBound.MaxValue = maxRange;
            LowerBound.Step = step;

            UpperBound.MinValue = minRange;
            UpperBound.MaxValue = maxRange;
            UpperBound.Step = step;

            // Set initial values
            _isUpdating = true;
            LowerBound.Value = Mathf.Clamp(initialLower, minRange, maxRange);
            UpperBound.Value = Mathf.Clamp(initialUpper, minRange, maxRange);
            _isUpdating = false;
        }
    }

    public void SetValues(float lower, float upper)
    {
        if (LowerBound != null && UpperBound != null)
        {
            _isUpdating = true;
            LowerBound.Value = Mathf.Clamp(lower, LowerBound.MinValue, LowerBound.MaxValue);
            UpperBound.Value = Mathf.Clamp(upper, UpperBound.MinValue, UpperBound.MaxValue);
            _isUpdating = false;
        }
    }

    public (float lower, float upper) GetValues()
    {
        if (LowerBound != null && UpperBound != null)
        {
            return ((float)LowerBound.Value, (float)UpperBound.Value);
        }
        return (0f, 0f);
    }

    public bool HasLabel(string label)
    {
        if (ParameterLabel != null)
        {
            return ParameterLabel.Text == label;
        }
        return false;
    }

    private void OnLowerBoundChanged(double value)
    {
        if (_isUpdating) return;

        float lowerValue = (float)value;

        // Ensure lower bound doesn't exceed upper bound
        if (UpperBound != null && lowerValue > UpperBound.Value)
        {
            _isUpdating = true;
            UpperBound.Value = lowerValue;
            _isUpdating = false;
        }

        EmitSignal(SignalName.ValueChanged, lowerValue, UpperBound?.Value ?? 0f);
    }

    private void OnUpperBoundChanged(double value)
    {
        if (_isUpdating) return;

        float upperValue = (float)value;

        // Ensure upper bound doesn't go below lower bound
        if (LowerBound != null && upperValue < LowerBound.Value)
        {
            _isUpdating = true;
            LowerBound.Value = upperValue;
            _isUpdating = false;
        }

        EmitSignal(SignalName.ValueChanged, LowerBound?.Value ?? 0f, upperValue);
    }
}
