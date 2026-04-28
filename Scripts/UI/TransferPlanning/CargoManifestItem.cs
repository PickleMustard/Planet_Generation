using System;
using Godot;
using Structures.Resources;
using System.Linq;

namespace UI.TransferPlanning;

/// <summary>
/// A single row in the cargo manifest: icon, name, stockpile, production rate, amount slider, weight, remove button.
/// </summary>
public partial class CargoManifestItem : HBoxContainer
{
    public event Action? AmountChanged;
    public event Action<CargoManifestItem>? RemoveRequested;

    private TextureRect? _iconRect;
    private Label? _nameLabel;
    private Label? _stockpileLabel;
    private Label? _rateLabel;
    private HSlider? _amountSlider;
    private SpinBox? _amountSpinBox;
    private Label? _weightLabel;
    private Button? _removeButton;

    private string _resourceId = string.Empty;
    private float _transportWeight = 1.0f;
    private bool _isUpdating;

    public string ResourceId => _resourceId;
    public float Amount => (float)(_amountSpinBox?.Value ?? 0);
    public float Weight => Amount * _transportWeight;

    public override void _Ready()
    {
        _iconRect = GetNodeOrNull<TextureRect>("IconRect");
        _nameLabel = GetNodeOrNull<Label>("NameLabel");
        _stockpileLabel = GetNodeOrNull<Label>("StockpileLabel");
        _rateLabel = GetNodeOrNull<Label>("RateLabel");
        _amountSlider = GetNodeOrNull<HSlider>("AmountContainer/AmountSlider");
        _amountSpinBox = GetNodeOrNull<SpinBox>("AmountContainer/AmountSpinBox");
        _weightLabel = GetNodeOrNull<Label>("WeightLabel");
        _removeButton = GetNodeOrNull<Button>("RemoveButton");

        if (_amountSlider != null)
        {
            _amountSlider.ValueChanged += OnSliderChanged;
        }

        if (_amountSpinBox != null)
        {
            _amountSpinBox.ValueChanged += OnSpinBoxChanged;
        }

        if (_removeButton != null)
        {
            _removeButton.Pressed += () => RemoveRequested?.Invoke(this);
        }
    }

    /// <summary>
    /// Initializes this row with resource data.
    /// </summary>
    public void SetResource(string resourceId, float stockpile, float netRate,
        float maxAmount, ResourceDefinition? definition)
    {
        _resourceId = resourceId;
        _transportWeight = definition?.TransportWeight ?? 1.0f;

        string displayName = definition?.IdName != null
            ? FormatResourceName(definition.IdName)
            : FormatResourceName(resourceId);

        if (_nameLabel != null)
            _nameLabel.Text = displayName;

        if (_iconRect != null)
        {
            Texture2D? icon = definition?.Icon?.IsValid == true
                ? (definition.Icon.SmallTexture ?? definition.Icon.MediumTexture)
                : null;

            if (icon != null)
            {
                _iconRect.Texture = icon;
                _iconRect.Modulate = Colors.White;
            }
            else
            {
                _iconRect.Texture = null;
                _iconRect.Modulate = definition?.DisplayColor ?? Colors.Gray;
            }
        }

        UpdateStockpileDisplay(stockpile);
        UpdateRateDisplay(netRate);
        SetMaxAmount(maxAmount);
    }

    /// <summary>
    /// Updates the maximum allowed amount (based on remaining cargo capacity).
    /// </summary>
    public void SetMaxAmount(float maxAmount)
    {
        _isUpdating = true;

        float clampedMax = Mathf.Max(maxAmount, 0f);
        if (_amountSlider != null)
        {
            _amountSlider.MaxValue = clampedMax;
            if (_amountSlider.Value > clampedMax)
                _amountSlider.Value = clampedMax;
        }

        if (_amountSpinBox != null)
        {
            _amountSpinBox.MaxValue = clampedMax;
            if (_amountSpinBox.Value > clampedMax)
                _amountSpinBox.Value = clampedMax;
        }

        UpdateWeightDisplay();
        _isUpdating = false;
    }

    public void UpdateStockpileDisplay(float stockpile)
    {
        if (_stockpileLabel != null)
            _stockpileLabel.Text = FormatAmount(stockpile);
    }

    public void UpdateRateDisplay(float netRate)
    {
        if (_rateLabel != null)
        {
            string sign = netRate >= 0 ? "+" : "";
            _rateLabel.Text = $"{sign}{netRate:F1}/s";
            _rateLabel.Modulate = netRate >= 0
                ? new Color(0.5f, 1f, 0.5f)
                : new Color(1f, 0.5f, 0.5f);
        }
    }

    private void OnSliderChanged(double value)
    {
        if (_isUpdating) return;
        _isUpdating = true;

        if (_amountSpinBox != null)
            _amountSpinBox.Value = value;

        UpdateWeightDisplay();
        _isUpdating = false;
        AmountChanged?.Invoke();
    }

    private void OnSpinBoxChanged(double value)
    {
        if (_isUpdating) return;
        _isUpdating = true;

        if (_amountSlider != null)
            _amountSlider.Value = value;

        UpdateWeightDisplay();
        _isUpdating = false;
        AmountChanged?.Invoke();
    }

    private void UpdateWeightDisplay()
    {
        if (_weightLabel != null)
            _weightLabel.Text = $"{Weight:F1}";
    }

    private static string FormatAmount(float amount)
    {
        if (amount >= 1_000_000f) return $"{amount / 1_000_000f:F1}M";
        if (amount >= 1_000f) return $"{amount / 1_000f:F1}K";
        return $"{amount:F1}";
    }

    private static string FormatResourceName(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId)) return "Unknown";
        var words = resourceId.Split('_');
        return string.Join(" ", words.Select(w =>
            string.IsNullOrEmpty(w) ? w : char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
    }
}
