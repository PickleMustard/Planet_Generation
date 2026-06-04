using Godot;
using Structures.GameState;
using UtilityLibrary;

/// <summary>
/// Drives the overview cartouche: company balance, last-quarter revenue, and the profit/loss arrow.
/// Seeds from <see cref="CompanyDataTracker"/> on ready, then tracks it live via SignalBus
/// (<c>CompanyBudgetChanged</c> for the balance, <c>CompanyQuarterlyRevenueUpdated</c> for the revenue
/// figure + arrow, which the tracker recomputes on each quarter rollover).
/// </summary>
public partial class HUD : Control
{
    private const string BalancePath =
        "OverviewCartouche/VBoxContainer/Panel/MarginContainer/DetailsCartouche/BusinessDetails/BalanceBackground/MarginContainer/BalanceLabel";
    private const string RevenuePath =
        "OverviewCartouche/VBoxContainer/Panel/MarginContainer/DetailsCartouche/BusinessDetails/RevenueBackground/MarginContainer/RevenueLabel";
    private const string ArrowPath =
        "OverviewCartouche/VBoxContainer/Panel/MarginContainer/DetailsCartouche/BusinessDetails/ProfitLossArrow";

    private static readonly Color ProfitColor = new(0.22f, 0.70f, 0.30f);
    private static readonly Color LossColor = new(0.82f, 0.22f, 0.20f);

    private RichTextLabel? _balanceLabel;
    private RichTextLabel? _revenueLabel;
    private TextureRect? _profitLossArrow;
    private Texture2D? _arrowTexture;

    public override void _Ready()
    {
        _balanceLabel = GetNodeOrNull<RichTextLabel>(BalancePath);
        _revenueLabel = GetNodeOrNull<RichTextLabel>(RevenuePath);
        _profitLossArrow = GetNodeOrNull<TextureRect>(ArrowPath);
        _arrowTexture = GD.Load<Texture2D>("res://UI/chevron_down.svg");

        if (_profitLossArrow != null)
            _profitLossArrow.Texture = _arrowTexture;

        var company = CompanyDataTracker.Instance;
        OnBudgetChanged(company?.Budget ?? 0);
        OnRevenueUpdated(company?.LastQuarterRevenue ?? 0);

        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.CompanyBudgetChanged += OnBudgetChanged;
            SignalBus.Instance.CompanyQuarterlyRevenueUpdated += OnRevenueUpdated;
        }
        base._Ready();
    }

    public override void _ExitTree()
    {
        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.CompanyBudgetChanged -= OnBudgetChanged;
            SignalBus.Instance.CompanyQuarterlyRevenueUpdated -= OnRevenueUpdated;
        }
        base._ExitTree();
    }

    private void OnBudgetChanged(double newBudget)
    {
        if (_balanceLabel != null)
            _balanceLabel.Text = $"{newBudget:N0} Balance";
    }

    private void OnRevenueUpdated(double lastQuarterRevenue)
    {
        bool profit = lastQuarterRevenue >= 0;

        if (_revenueLabel != null)
        {
            string sign = profit ? "+ " : "- ";
            _revenueLabel.Text = sign + $"{Mathf.Abs(lastQuarterRevenue):N0}";
        }

        if (_profitLossArrow != null)
        {
            // No closed quarter yet — nothing meaningful to point at.
            _profitLossArrow.Visible = lastQuarterRevenue != 0;
            _profitLossArrow.FlipV = profit; // chevron_down points down (loss); flipped = up (profit)
            _profitLossArrow.SelfModulate = profit ? ProfitColor : LossColor;
        }
    }
}
