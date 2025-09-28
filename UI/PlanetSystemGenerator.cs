using System;
using System.Reflection;
using Godot;
using Godot.Collections;

using PlanetGeneration;

namespace UI;

public partial class PlanetSystemGenerator : Control
{
    [Signal]
    public delegate void GeneratePressedEventHandler(Array<Dictionary> bodies);

    private VBoxContainer _bodiesList;
    private Label _countLabel;
    private Button _addBtn;
    private Button _removeBtn;
    private Button _generateBtn;
    private PackedScene _bodyItemScene;

    public override void _Ready()
    {
        this.AddToGroup("GenerationMenu");
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
        var list = new Array<Dictionary>();
        foreach (Node child in _bodiesList.GetChildren())
        {
            if (child is BodyItem bi)
                list.Add(bi.ToParams());
        }

        // Notify subscribers; if none, log for debugging
        EmitSignal(SignalName.GeneratePressed, list);
    }

    private Dictionary ConvertParamsToDict<T>(T parameters)
    {
        Dictionary dict = new Dictionary();
        Type type = typeof(T);
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (FieldInfo field in fields)
        {
            object value = field.GetValue(parameters);
            if (value != null)
            {
                Type fieldType = field.FieldType;
                if (fieldType.IsPrimitive || fieldType == typeof(string))
                {
                    dict.Add(field.Name, (Godot.Variant)Convert.ChangeType(value, fieldType));
                }
            }
        }
        return dict;
    }
}
