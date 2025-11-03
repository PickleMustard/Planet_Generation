using System;
using System.IO;
using System.Reflection;
using Godot;
using Godot.Collections;
using PlanetGeneration;
using Tommy;
using FileAccess = Godot.FileAccess;

namespace UI;

public partial class PlanetSystemGenerator : Control
{
    Vector2 BASE_SIZE = new Vector2(400, 650);
    Vector2 EXPANDED_SIZE = new Vector2(600, 650);

    [Signal]
    public delegate void GeneratePressedEventHandler(Array<Dictionary> bodies);

    [Signal]
    public delegate void ValidatePressedEventHandler(Array<Dictionary> bodies);

    private VBoxContainer _bodiesList;
    private Label _countLabel;
    private Button _addBtn;
    private Button _removeBtn;
    private Button _generateBtn;
    private Button _validateBtn;
    private Button _saveBtn;
    private PackedScene _bodyItemScene;
    private TabContainer _tabContainer;
    private VBoxContainer _templatesList;
    private TextureRect _stabilityIndicator;

    private Texture2D _checkMark;
    private Texture2D _xMark;
    private Dictionary<String, bool> _toggledSubcontainers;

    public override void _Ready()
    {
        this.AddToGroup("GenerationMenu");
        var parent = GetParent() as Control;
        parent.Size = BASE_SIZE;
        _toggledSubcontainers = new Dictionary<String, bool>();
        _bodyItemScene = GD.Load<PackedScene>("res://UI/BodyItem.tscn");
        _tabContainer = GetNode<TabContainer>("MarginContainer/TabContainer");
        _bodiesList = GetNode<VBoxContainer>(
            "MarginContainer/TabContainer/BodiesTab/Scroll/BodiesList"
        );
        _countLabel = GetNode<Label>(
            "MarginContainer/TabContainer/BodiesTab/ControlsRow/CountLabel"
        );
        _addBtn = GetNode<Button>("MarginContainer/TabContainer/BodiesTab/ControlsRow/AddBody");
        _removeBtn = GetNode<Button>(
            "MarginContainer/TabContainer/BodiesTab/ControlsRow/RemoveBody"
        );
        _generateBtn = GetNode<Button>(
            "MarginContainer/TabContainer/BodiesTab/GenerationMargin/GenerationRow/GenerateButton"
        );
        _validateBtn = GetNode<Button>(
            "MarginContainer/TabContainer/BodiesTab/GenerationMargin/GenerationRow/ValidateButton"
        );
        _saveBtn = GetNode<Button>(
            "MarginContainer/TabContainer/BodiesTab/GenerationMargin/GenerationRow/SaveFileButton"
        );
        _stabilityIndicator = GetNode<TextureRect>(
            "MarginContainer/TabContainer/BodiesTab/GenerationMargin/GenerationRow/StabilityIndicator"
        );
        _templatesList = GetNode<VBoxContainer>(
            "MarginContainer/TabContainer/TemplatesTab/TemplatesScroll/TemplatesList"
        );

        _addBtn.Pressed += AddBodyItem;
        _removeBtn.Pressed += RemoveLastBodyItem;
        _generateBtn.Pressed += OnGeneratePressed;
        _validateBtn.Pressed += OnValidatePressed;
        _saveBtn.Pressed += OnSavePressed;

        _checkMark = GD.Load<Texture2D>("res://UI/checkmark.svg");
        _xMark = GD.Load<Texture2D>("res://UI/xmark.svg");
        _stabilityIndicator.Texture = _checkMark;

        // Start with one body by default
        AddBodyItem();
        UpdateCountLabel();
        LoadTemplates();
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateCountLabel();
    }

    public void ExpandMenu(String sender, bool toggle)
    {
        var parent = GetParent() as Control;
        if (_toggledSubcontainers.ContainsKey(sender) && !toggle)
        {
            _toggledSubcontainers.Remove(sender);
            if (_toggledSubcontainers.Count == 0)
            {
                parent.Size = BASE_SIZE;
            }
        }
        else
        {
            parent.Size = EXPANDED_SIZE;
            if (!_toggledSubcontainers.ContainsKey(sender)) _toggledSubcontainers.Add(sender, toggle);
        }
    }

    private void AddBodyItem()
    {
        if (_bodyItemScene == null)
            return;
        var node = _bodyItemScene.Instantiate<BodyItem>();
        // Wire per-item remove
        node.OnRemoveRequested += HandleItemRemove;
        // Wire position change
        node.ItemUpdate += OnBodyItemUpdate;
        node.ExpandMenu += ExpandMenu;
        _bodiesList.AddChild(node);
        UpdateCountLabel();
        RedistributeOrbitalRings();
    }

    private void RemoveLastBodyItem()
    {
        if (_bodiesList.GetChildCount() == 0)
            return;
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
            RedistributeOrbitalRings();
        }
    }

    private void UpdateCountLabel()
    {
        int count = _bodiesList.GetChildCount();
        _countLabel.Text = count == 1 ? "1 body" : $"{count} bodies";
    }

    private void RedistributeOrbitalRings()
    {
        var bodiesByRing = new System.Collections.Generic.Dictionary<
            float,
            System.Collections.Generic.List<BodyItem>
        >();
        foreach (Node child in _bodiesList.GetChildren())
        {
            if (child is BodyItem bi)
            {
                //var pos = ((Godot.Collections.Dictionary)bi.ToParams()["Template"])["position"]
                //    .AsVector3();
                var pos = bi.GetBodyPosition();
                float radius = pos.Length();
                if (!bodiesByRing.ContainsKey(radius))
                    bodiesByRing[radius] = new System.Collections.Generic.List<BodyItem>();
                bodiesByRing[radius].Add(bi);
            }
        }

        foreach (var kvp in bodiesByRing)
        {
            var bodies = kvp.Value;
            if (bodies.Count > 1)
            {
                RedistributeBodiesInRing(bodies, kvp.Key);
            }
        }
    }

    private void RedistributeBodiesInRing(
        System.Collections.Generic.List<BodyItem> bodies,
        float radius
    )
    {
        int n = bodies.Count;
        for (int i = 0; i < n; i++)
        {
            float angle = (i * 2 * Mathf.Pi) / n;
            var newPos = new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), 0); // Assuming Z=0 for rings
            var body = bodies[i];
            var originalVel = body.GetVelocity();
            float speed = originalVel.Length();
            var newVel = new Vector3(-newPos.Y, newPos.X, 0).Normalized() * speed;
            body.SetPosition(newPos);
            body.SetVelocity(newVel);
        }
    }

    private void OnBodyItemUpdate()
    {
        RedistributeOrbitalRings();
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

    private void OnValidatePressed()
    {
        CheckSystemStability();
    }

    private void LoadTemplates()
    {
        var templateFiles = DirAccess.GetFilesAt("res://Configuration/SystemTemplate/");
        foreach (var file in templateFiles)
        {
            if (file.EndsWith(".toml"))
            {
                var button = new Button();
                button.Text = file.Replace(".toml", "");
                button.Pressed += () => LoadTemplate(file);
                _templatesList.AddChild(button);
            }
        }
    }

    private void LoadTemplate(string fileName)
    {
        // Clear existing bodies
        foreach (Node child in _bodiesList.GetChildren())
        {
            child.RemoveFromGroup("CelestialBody");
            child.QueueFree();
        }

        // Load bodies from utility library
        var bodies = UtilityLibrary.SystemGenTemplates.LoadSolarSystemTemplate(fileName);
        GD.Print($"Table: {bodies}");
        foreach (var bodyDict in bodies)
        {
            var typeStr = (string)bodyDict["type"];
            var template = (Godot.Collections.Dictionary)bodyDict["template"];
            var position = (Vector3)template["position"];
            var velocity = (Vector3)template["velocity"];
            var mass = (float)template["mass"];
            var size = (int)template["size"];

            var bodyItem = _bodyItemScene.Instantiate<BodyItem>();
            _bodiesList.AddChild(bodyItem);
            GD.Print($"Type: {typeStr}");
            if (Enum.TryParse<CelestialBodyType>(typeStr, out var type))
            {
                // Set the option button to the correct type
                if (bodyItem.OptionButton != null)
                {
                    for (int i = 0; i < bodyItem.OptionButton.ItemCount; i++)
                    {
                        if (bodyItem.OptionButton.GetItemText(i) == typeStr)
                        {
                            bodyItem.OptionButton.Select(i);
                            bodyItem.UpdateHeaderFromBodyType(typeStr);
                            bodyItem.SetTemplate(bodyDict);
                            break;
                        }
                    }
                }
            }
            bodyItem.SetPosition(position);
            bodyItem.SetVelocity(velocity);
            bodyItem.SetSize(size);
            // Set mass
            if (bodyItem.mass != null)
                bodyItem.mass.Value = Mathf.Clamp(mass, 0f, 100000000f);
            // Set templateDict directly from loaded data to preserve custom settings
            bodyItem.ItemUpdate += OnBodyItemUpdate;
        }

        UpdateCountLabel();
        RedistributeOrbitalRings();
    }

    private string ReadString(TomlTable table, string key, string fallback)
    {
        if (table.HasKey(key) && table[key] is TomlNode node)
        {
            return node.ToString().Trim('"');
        }
        return fallback;
    }

    private Vector3 ReadVector3(TomlTable table, string key, Vector3 fallback)
    {
        if (table.HasKey(key) && table[key] is TomlArray arr && arr.ChildrenCount >= 3)
        {
            float x = NodeToFloat(arr[0], 0f);
            float y = NodeToFloat(arr[1], 0f);
            float z = NodeToFloat(arr[2], 0f);
            return new Vector3(x, y, z);
        }
        return fallback;
    }

    private float ReadFloat(TomlTable table, string key, float fallback)
    {
        if (table.HasKey(key) && table[key] is TomlNode node)
        {
            return NodeToFloat(node, fallback);
        }
        return fallback;
    }

    private float NodeToFloat(TomlNode node, float fallback)
    {
        try
        {
            if (node is Tommy.TomlInteger ti)
                return (float)ti.Value;
            if (node is Tommy.TomlFloat tf)
                return (float)tf.Value;

            var s = node.ToString();
            if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
                s = s.Substring(1, s.Length - 2);
            if (float.TryParse(s, out var v))
                return v;
        }
        catch { }
        return fallback;
    }

    private int ReadInt(TomlTable table, string key, int fallback)
    {
        if (!table.HasKey(key))
            return fallback;
        var node = table[key];
        if (node is Tommy.TomlInteger ti)
            return (int)ti.Value;
        if (node is Tommy.TomlFloat tf)
            return (int)tf.Value;
        var s = node.ToString();
        if (int.TryParse(s, out var v))
            return v;
        if (float.TryParse(s, out var vf))
            return (int)vf;
        return fallback;
    }

    private int[] ReadIntArray(TomlTable table, string key, int[] fallback)
    {
        if (!table.HasKey(key))
            return fallback;
        if (table[key] is TomlArray arr && arr.ChildrenCount > 0)
        {
            int[] result = new int[arr.ChildrenCount];
            for (int i = 0; i < arr.ChildrenCount; i++)
            {
                result[i] = (int)NodeToFloat(arr[i], fallback[i % fallback.Length]);
            }
            return result;
        }
        return fallback;
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

    private void CheckSystemStability()
    {
        var list = new Array<Dictionary>();
        foreach (Node child in _bodiesList.GetChildren())
        {
            if (child is BodyItem bi)
                list.Add(bi.ToParams());
        }

        if (list.Count < 2)
        {
            UpdateStabilityIndicator(true);
            return;
        }

        // Use the advanced gravitational stability check
        var currentScene = GetTree().CurrentScene;
        var systemGenerator = GetNode<SystemGenerator>(
            $"{currentScene.GetPath()}/system_generator"
        );
        if (systemGenerator != null)
        {
            bool isStable = systemGenerator.CheckGravitationalStability(list);
            UpdateStabilityIndicator(isStable);
        }
        else
        {
            // Fallback to simple distance check if SystemGenerator is not found
            bool isStable = SimpleStabilityCheck(list);
            UpdateStabilityIndicator(isStable);
        }
    }

    private bool SimpleStabilityCheck(Array<Dictionary> bodies)
    {
        for (int i = 0; i < bodies.Count; i++)
        {
            for (int j = i + 1; j < bodies.Count; j++)
            {
                var body1 = bodies[i];
                var body2 = bodies[j];

                Vector3 pos1 = body1["Position"].AsVector3();
                Vector3 pos2 = body2["Position"].AsVector3();

                // Get sizes (radii) from mesh parameters
                float size1 = 1.0f; // Default size
                float size2 = 1.0f; // Default size

                if (
                    body1.ContainsKey("mesh")
                    && body1["mesh"].AsGodotDictionary().ContainsKey("size")
                )
                {
                    size1 = body1["mesh"].AsGodotDictionary()["size"].AsSingle();
                }

                if (
                    body2.ContainsKey("mesh")
                    && body2["mesh"].AsGodotDictionary().ContainsKey("size")
                )
                {
                    size2 = body2["mesh"].AsGodotDictionary()["size"].AsSingle();
                }

                float distance = pos1.DistanceTo(pos2);
                float minDistance = (size1 + size2) * 0.5f; // Use half sizes as approximate radii

                if (distance < minDistance)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private void UpdateStabilityIndicator(bool isStable)
    {
        if (_stabilityIndicator != null)
        {
            if (isStable)
            {
                _stabilityIndicator.Texture = _checkMark;
            }
            else
            {
                _stabilityIndicator.Texture = _xMark;
            }
        }
    }

    private void OnSavePressed()
    {
        ShowFileNameDialog();
    }

    private void ShowFileNameDialog()
    {
        var dialog = new AcceptDialog();
        dialog.Title = "Save System Configuration";

        var vbox = new VBoxContainer();
        var label = new Label();
        label.Text = "Enter filename for the system configuration:";
        vbox.AddChild(label);

        var lineEdit = new LineEdit();
        lineEdit.PlaceholderText = "MySystem";
        lineEdit.CustomMinimumSize = new Vector2(300, 0);
        vbox.AddChild(lineEdit);

        dialog.AddChild(vbox);

        dialog.Connect(AcceptDialog.SignalName.Confirmed, Callable.From(() => OnSaveDialogConfirmed(dialog, lineEdit)));
        dialog.Connect(AcceptDialog.SignalName.Canceled, Callable.From(() => OnSaveDialogCanceled(dialog)));

        AddChild(dialog);
        dialog.PopupCentered();

        // Focus the line edit and select all text
        lineEdit.GrabFocus();
        lineEdit.CallDeferred(LineEdit.MethodName.SelectAll);
    }

    private void OnSaveDialogConfirmed(AcceptDialog dialog, LineEdit lineEdit)
    {
        string fileName = lineEdit.Text.Trim();
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = "UntitledSystem";
        }

        // Ensure .toml extension
        if (!fileName.EndsWith(".toml", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".toml";
        }

        SaveSystemToFile(fileName);
        dialog.QueueFree();
    }

    private void OnSaveDialogCanceled(AcceptDialog dialog)
    {
        dialog.QueueFree();
    }

    private void SaveSystemToFile(string fileName)
    {
        try
        {
            var bodies = new Array<Dictionary>();
            foreach (Node child in _bodiesList.GetChildren())
            {
                if (child is BodyItem bi)
                    bodies.Add(bi.ToParams());
            }

            string tomlContent = UtilityLibrary.SystemGenTemplates.GenerateTOMLContent(bodies);

            string filePath = $"res://Configuration/SystemTemplate/{fileName}";

            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PrintErr($"Failed to create file: {filePath}");
                ShowErrorDialog($"Failed to save file: {fileName}");
                return;
            }

            file.StoreString(tomlContent);
            file.Close();

            GD.Print($"System configuration saved to: {filePath}");
            ShowSuccessDialog($"System configuration saved as: {fileName}");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"Error saving system: {ex.Message}\n{ex.StackTrace}");
            ShowErrorDialog($"Error saving system: {ex.Message}");
        }
    }



    private void ShowSuccessDialog(string message)
    {
        var dialog = new AcceptDialog();
        dialog.Title = "Success";
        dialog.DialogText = message;
        AddChild(dialog);
        dialog.PopupCentered();
        dialog.Connect(AcceptDialog.SignalName.Confirmed, new Callable(dialog, Node.MethodName.QueueFree));
    }

    private void ShowErrorDialog(string message)
    {
        var dialog = new AcceptDialog();
        dialog.Title = "Error";
        dialog.DialogText = message;
        AddChild(dialog);
        dialog.PopupCentered();
        dialog.Connect(AcceptDialog.SignalName.Confirmed, new Callable(dialog, Node.MethodName.QueueFree));
    }
}
