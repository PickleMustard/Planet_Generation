using Godot;
using UtilityLibrary;
using UtilityLibrary.SaveLoad;
using UtilityLibrary.SaveLoad.Dto;

namespace Structures.GameState;

/// <summary>
/// System-level owner of company-wide state: budget, debt, antagonism, and research.
/// Future home for the systems an administration building drives (labor, logistics,
/// research, law). A sibling of <see cref="SystemData"/> under system_container;
/// session-scoped and accessed through the static <see cref="Instance"/>.
///
/// This is a framework: state + mutators + SignalBus notifications to plug into later.
/// Currency is represented as <c>double</c> for Godot signal compatibility.
/// </summary>
public partial class CompanyDataTracker : Node, ISaveSerializable, ISaveRestorable
{
    public static CompanyDataTracker? Instance { get; private set; }

    /// <summary>Section discriminator routing this tracker's DTO within the save file.</summary>
    public string SaveKey => "company";

    /// <summary>Available spendable currency.</summary>
    public double Budget { get; private set; }

    /// <summary>Outstanding debt owed by the company.</summary>
    public double Debt { get; private set; }

    /// <summary>
    /// Aggregate hostility from rival companies. Single value for now;
    /// a per-company breakdown can replace this once rivals are modeled.
    /// </summary>
    public float Antagonism { get; private set; }

    /// <summary>Accumulated research points.</summary>
    public double Research { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        AddToGroup("save_serializable");
        base._Ready();
    }

    /// <summary>Snapshots company state into a <see cref="CompanyDto"/>. See <see cref="ISaveSerializable"/>.</summary>
    public object Serialize() => new CompanyDto
    {
        Budget = Budget,
        Debt = Debt,
        Antagonism = Antagonism,
        Research = Research,
    };

    /// <summary>Restores company state from a <see cref="CompanyDto"/>. See <see cref="ISaveRestorable"/>.</summary>
    public void Restore(object dto)
    {
        if (dto is CompanyDto c)
            LoadState(c.Budget, c.Debt, c.Antagonism, c.Research);
    }

    public override void _ExitTree()
    {
        if (ReferenceEquals(Instance, this))
            Instance = null;
        base._ExitTree();
    }

    /// <summary>Adds currency to the budget (sell-order proceeds, income, grants).</summary>
    public void Deposit(double amount)
    {
        if (amount <= 0)
            return;
        Budget += amount;
        SignalBus.Instance?.EmitCompanyBudgetChanged(Budget);
    }

    /// <summary>
    /// Withdraws currency if affordable. Returns false (and changes nothing) when the
    /// budget cannot cover <paramref name="amount"/>. Used by buy orders / spending.
    /// </summary>
    public bool TryWithdraw(double amount)
    {
        if (amount <= 0)
            return true;
        if (amount > Budget)
            return false;
        Budget -= amount;
        SignalBus.Instance?.EmitCompanyBudgetChanged(Budget);
        return true;
    }

    /// <summary>Increases outstanding debt (e.g. taking a loan; loan principal also Deposit-ed separately).</summary>
    public void AddDebt(double amount)
    {
        if (amount <= 0)
            return;
        Debt += amount;
        SignalBus.Instance?.EmitCompanyDebtChanged(Debt);
    }

    /// <summary>
    /// Repays debt by pulling from the budget. Returns false when either the budget
    /// can't cover the payment or there is no debt to repay; nothing changes on failure.
    /// </summary>
    public bool RepayDebt(double amount)
    {
        if (amount <= 0 || Debt <= 0)
            return false;
        double payment = amount < Debt ? amount : Debt;
        if (!TryWithdraw(payment))
            return false;
        Debt -= payment;
        SignalBus.Instance?.EmitCompanyDebtChanged(Debt);
        return true;
    }

    /// <summary>Adjusts antagonism by a signed delta, clamped to a non-negative value.</summary>
    public void ModifyAntagonism(float delta)
    {
        Antagonism = Mathf.Max(0f, Antagonism + delta);
        SignalBus.Instance?.EmitCompanyAntagonismChanged(Antagonism);
    }

    /// <summary>
    /// Overwrites all company state from a loaded save and re-emits the change signals so any
    /// listening UI refreshes. Called by the save loader, not during normal play.
    /// </summary>
    public void LoadState(double budget, double debt, float antagonism, double research)
    {
        Budget = budget;
        Debt = debt;
        Antagonism = antagonism;
        Research = research;
        SignalBus.Instance?.EmitCompanyBudgetChanged(Budget);
        SignalBus.Instance?.EmitCompanyDebtChanged(Debt);
        SignalBus.Instance?.EmitCompanyAntagonismChanged(Antagonism);
        SignalBus.Instance?.EmitCompanyResearchChanged(Research);
    }

    /// <summary>Adds research points.</summary>
    public void AddResearch(double amount)
    {
        if (amount <= 0)
            return;
        Research += amount;
        SignalBus.Instance?.EmitCompanyResearchChanged(Research);
    }
}
