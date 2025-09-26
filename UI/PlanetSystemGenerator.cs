using Godot;

using System.Collections.Generic;
using PlanetGeneration;

[Signal]
public delegate void GeneratePressedEventHandler();

public partial class PlanetSystemGenerator : Control
{
    private VBoxContainer _bodiesList;
    private Label _countLabel;
    private Button _addBtn;
    private Button _removeBtn;
    private Button _generateBtn;
    private PackedScene _bodyItemScene;

    // Consumers can subscribe to be notified when user requests generation
    public event System.Action<System.Collections.Generic.List<CelestialBodyParams>> GenerateRequested;

    public override void _Ready()
    {
        _bodyItemScene = GD.Load<PackedScene>("res://UI/BodyItem.tscn");
        _bodiesList = GetNode<VBoxContainer>("MarginContainer/VBox/Scroll/BodiesList");
        _countLabel = GetNode<Label>("MarginContainer/VBox/ControlsRow/CountLabel");
        _addBtn = GetNode<Button>("MarginContainer/VBox/ControlsRow/AddBody");
        _removeBtn = GetNode<Button>("MarginContainer/VBox/ControlsRow/RemoveBody");
        _generateBtn = GetNode<Button>("MarginContainer/VBox/GenerateButton");

        _addBtn.Pressed += AddBodyItem;
        _removeBtn.Pressed += RemoveLastBodyItem;
        _generateBtn.Pressed += OnGeneratePressed;

        // Start with one body by default
        AddBodyItem();
        UpdateCountLabel();
    }

    private void AddBodyItem()
    {
        if (_bodyItemScene == null) return;
        var node = _bodyItemScene.Instantiate<BodyItem>();
        // Wire per-item remove
        node.OnRemoveRequested += HandleItemRemove;
        _bodiesList.AddChild(node);
        UpdateCountLabel();
    }

    private void RemoveLastBodyItem()
    {
        if (_bodiesList.GetChildCount() == 0) return;
        var last = _bodiesList.GetChild(_bodiesList.GetChildCount() - 1);
        last.QueueFree();
        UpdateCountLabel();
    }

    private void HandleItemRemove(BodyItem item)
    {
        if (IsInstanceValid(item) && item.GetParent() == _bodiesList)
        {
            item.QueueFree();
            UpdateCountLabel();
        }
    }

    private void UpdateCountLabel()
    {
        int count = _bodiesList.GetChildCount();
        _countLabel.Text = count == 1 ? "1 body" : $"{count} bodies";
    }

    private void OnGeneratePressed()
    {
        var list = new System.Collections.Generic.List<CelestialBodyParams>();
        foreach (Node child in _bodiesList.GetChildren())
        {
            if (child is BodyItem bi)
                list.Add(bi.ToParams());
        }

        // Notify subscribers; if none, log for debugging
        if (GenerateRequested != null) GenerateRequested.Invoke(list);
        else GD.Print($"GenerateRequested with {list.Count} bodies");
    }
}
