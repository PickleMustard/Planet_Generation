#if DEBUG
using System.Collections.Generic;
using Godot;
using DeveloperTools.Common;
using Structures.Resources;

namespace DeveloperTools.RecipeEditor;

/// <summary>
/// Row control for one conditional output slot. The condition is built from a
/// flat list of [variable][operator][value] rules joined by per-row AND/OR;
/// the resource and amount editors sit in a footer alongside Add-rule and
/// delete-row buttons.
///
/// Legacy expressions that the rule builder cannot represent are rendered
/// read-only — the user must delete and rebuild the row to edit them.
/// Built programmatically — no scene file.
/// </summary>
public partial class ConditionalOutputRow : VBoxContainer
{
    [Signal]
    public delegate void SlotChangedEventHandler(int slotIndex);

    [Signal]
    public delegate void SlotDeletedEventHandler(int slotIndex);

    private int _slotIndex;

    // Mutable state mirrored to the parent on every change via SlotChanged.
    private List<ConditionRule> _rules = new();
    private string? _legacyCondition;
    private string _resource = "";
    private float _amount;

    public IReadOnlyList<ConditionRule> Rules => _rules;
    public string? LegacyCondition => _legacyCondition;
    public string Resource => _resource;
    public float Amount => _amount;

    private VBoxContainer _rulesContainer = null!;
    private Button _resourceButton = null!;
    private SpinBox _amountSpin = null!;

    public void Configure(int slotIndex, RecipeEditorModel.ConditionalOutputSlot slot)
    {
        _slotIndex = slotIndex;
        _legacyCondition = slot.LegacyCondition;
        _rules = new List<ConditionRule>();
        foreach (var rule in slot.Rules)
        {
            _rules.Add(new ConditionRule
            {
                Join = rule.Join,
                Variable = rule.Variable,
                Operator = rule.Operator,
                Value = rule.Value,
            });
        }
        _resource = slot.Resource;
        _amount = slot.Amount;
    }

    public override void _Ready()
    {
        base._Ready();
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _rulesContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        AddChild(_rulesContainer);

        var footer = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        AddChild(footer);

        if (string.IsNullOrEmpty(_legacyCondition))
        {
            var addRuleBtn = new Button
            {
                Text = "+ rule",
                TooltipText = "Add a comparison clause to this condition",
            };
            addRuleBtn.Pressed += OnAddRulePressed;
            footer.AddChild(addRuleBtn);
        }

        _resourceButton = new Button
        {
            CustomMinimumSize = new Vector2(180, 0),
            ClipText = true,
            TooltipText = "Resource produced when condition is true",
        };
        _resourceButton.Pressed += OnPickResource;
        footer.AddChild(_resourceButton);

        _amountSpin = new SpinBox
        {
            MinValue = 0,
            MaxValue = 1000000,
            Step = 0.01f,
            Value = _amount,
            CustomMinimumSize = new Vector2(96, 0),
            AllowGreater = true,
        };
        _amountSpin.ValueChanged += OnAmountChanged;
        footer.AddChild(_amountSpin);

        var deleteBtn = new Button { Text = "✕", TooltipText = "Remove this conditional output" };
        deleteBtn.Pressed += () => EmitSignal(SignalName.SlotDeleted, _slotIndex);
        footer.AddChild(deleteBtn);

        UpdateResourceButton();
        RebuildRulesUI();
    }

    private void RebuildRulesUI()
    {
        foreach (var child in _rulesContainer.GetChildren())
            child.QueueFree();

        if (!string.IsNullOrEmpty(_legacyCondition))
        {
            var legacyEdit = new LineEdit
            {
                Text = _legacyCondition,
                Editable = false,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = "Legacy expression — delete the row and re-add it to use the rule builder.",
            };
            _rulesContainer.AddChild(legacyEdit);
            return;
        }

        if (_rules.Count == 0)
        {
            // Seed an empty first rule so the UI is never blank.
            _rules.Add(new ConditionRule
            {
                Join = ConditionJoin.And,
                Variable = RecipeExpressionEvaluator.AllowedVariables[0],
                Operator = ConditionOperator.Eq,
                Value = 0f,
            });
        }

        for (int i = 0; i < _rules.Count; i++)
        {
            int capturedIndex = i;
            var rule = _rules[i];
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };

            // Join dropdown — hidden on row 0.
            var joinButton = new OptionButton
            {
                CustomMinimumSize = new Vector2(72, 0),
                TooltipText = "Combine with previous rule",
            };
            joinButton.AddItem("AND", 0);
            joinButton.AddItem("OR", 1);
            joinButton.Select(rule.Join == ConditionJoin.Or ? 1 : 0);
            joinButton.ItemSelected += idx =>
            {
                _rules[capturedIndex].Join = idx == 1 ? ConditionJoin.Or : ConditionJoin.And;
                EmitChange();
            };
            if (i == 0) joinButton.Visible = false;
            row.AddChild(joinButton);

            // Variable dropdown.
            var varButton = new OptionButton { CustomMinimumSize = new Vector2(120, 0) };
            int varSelectIdx = 0;
            for (int v = 0; v < RecipeExpressionEvaluator.AllowedVariables.Count; v++)
            {
                varButton.AddItem(RecipeExpressionEvaluator.AllowedVariables[v], v);
                if (RecipeExpressionEvaluator.AllowedVariables[v] == rule.Variable)
                    varSelectIdx = v;
            }
            varButton.Select(varSelectIdx);
            row.AddChild(varButton);

            // Operator dropdown.
            var opSymbols = new[] { "==", "!=", "<", "<=", ">", ">=" };
            var opEnums = new[]
            {
                ConditionOperator.Eq, ConditionOperator.NotEq,
                ConditionOperator.Lt, ConditionOperator.Lte,
                ConditionOperator.Gt, ConditionOperator.Gte,
            };
            var opButton = new OptionButton { CustomMinimumSize = new Vector2(64, 0) };
            int opSelectIdx = 0;
            for (int k = 0; k < opSymbols.Length; k++)
            {
                opButton.AddItem(opSymbols[k], k);
                if (opEnums[k] == rule.Operator) opSelectIdx = k;
            }
            opButton.Select(opSelectIdx);
            opButton.ItemSelected += idx =>
            {
                _rules[capturedIndex].Operator = opEnums[idx];
                EmitChange();
            };
            row.AddChild(opButton);

            // Value spinbox — tuned per variable.
            var valueSpin = new SpinBox
            {
                Value = rule.Value,
                CustomMinimumSize = new Vector2(96, 0),
                AllowGreater = true,
                AllowLesser = true,
            };
            ApplyValueSpinRange(valueSpin, rule.Variable);
            valueSpin.ValueChanged += v =>
            {
                _rules[capturedIndex].Value = (float)v;
                EmitChange();
            };
            row.AddChild(valueSpin);

            varButton.ItemSelected += idx =>
            {
                string newVar = RecipeExpressionEvaluator.AllowedVariables[(int)idx];
                _rules[capturedIndex].Variable = newVar;
                ApplyValueSpinRange(valueSpin, newVar);
                EmitChange();
            };

            // Delete-rule button — first rule can be deleted only if more rules exist.
            var deleteRuleBtn = new Button { Text = "✕", TooltipText = "Remove this rule" };
            deleteRuleBtn.Pressed += () =>
            {
                if (_rules.Count <= 1) return; // never leave zero rules
                _rules.RemoveAt(capturedIndex);
                RebuildRulesUI();
                EmitChange();
            };
            deleteRuleBtn.Disabled = _rules.Count <= 1;
            row.AddChild(deleteRuleBtn);

            _rulesContainer.AddChild(row);
        }
    }

    private static void ApplyValueSpinRange(SpinBox spin, string variable)
    {
        if (variable == "specifier")
        {
            spin.Step = 1;
            spin.Rounded = true;
            spin.MinValue = 0;
            spin.MaxValue = 100;
        }
        else
        {
            spin.Step = 0.01;
            spin.Rounded = false;
            spin.MinValue = -1;
            spin.MaxValue = 1;
        }
    }

    private void OnAddRulePressed()
    {
        _rules.Add(new ConditionRule
        {
            Join = ConditionJoin.And,
            Variable = RecipeExpressionEvaluator.AllowedVariables[0],
            Operator = ConditionOperator.Eq,
            Value = 0f,
        });
        RebuildRulesUI();
        EmitChange();
    }

    private void OnPickResource()
    {
        var popup = new ResourcePickerPopup();
        popup.ResourcePicked += id => { _resource = id; UpdateResourceButton(); EmitChange(); };
        GetTree().Root.AddChild(popup);
        popup.PopupCentered();
    }

    private void OnAmountChanged(double value)
    {
        _amount = (float)value;
        EmitChange();
    }

    private void UpdateResourceButton()
    {
        _resourceButton.Text = string.IsNullOrEmpty(_resource) ? "(select resource)" : _resource;
        _resourceButton.TooltipText = string.IsNullOrEmpty(_resource)
            ? "Resource produced when condition is true"
            : _resource;
    }

    private void EmitChange()
    {
        EmitSignal(SignalName.SlotChanged, _slotIndex);
    }
}
#endif
