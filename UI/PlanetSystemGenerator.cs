 using System;
 using System.Reflection;
 using System.IO;
 using Godot;
 using Godot.Collections;
 using Tommy;

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
     private TabContainer _tabContainer;
     private VBoxContainer _templatesList;

     public override void _Ready()
     {
         this.AddToGroup("GenerationMenu");
         _bodyItemScene = GD.Load<PackedScene>("res://UI/BodyItem.tscn");
         _tabContainer = GetNode<TabContainer>("MarginContainer/TabContainer");
         _bodiesList = GetNode<VBoxContainer>("MarginContainer/TabContainer/BodiesTab/Scroll/BodiesList");
         _countLabel = GetNode<Label>("MarginContainer/TabContainer/BodiesTab/ControlsRow/CountLabel");
         _addBtn = GetNode<Button>("MarginContainer/TabContainer/BodiesTab/ControlsRow/AddBody");
         _removeBtn = GetNode<Button>("MarginContainer/TabContainer/BodiesTab/ControlsRow/RemoveBody");
         _generateBtn = GetNode<Button>("MarginContainer/TabContainer/BodiesTab/GenerateButton");
         _templatesList = GetNode<VBoxContainer>("MarginContainer/TabContainer/TemplatesTab/TemplatesScroll/TemplatesList");

         _addBtn.Pressed += AddBodyItem;
         _removeBtn.Pressed += RemoveLastBodyItem;
         _generateBtn.Pressed += OnGeneratePressed;

         // Start with one body by default
         AddBodyItem();
         UpdateCountLabel();
         LoadTemplates();
     }

    private void AddBodyItem()
    {
        if (_bodyItemScene == null) return;
        var node = _bodyItemScene.Instantiate<BodyItem>();
        // Wire per-item remove
        node.OnRemoveRequested += HandleItemRemove;
        // Wire position change
        node.ItemUpdate += OnBodyItemUpdate;
        _bodiesList.AddChild(node);
        UpdateCountLabel();
        RedistributeOrbitalRings();
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
        var bodiesByRing = new System.Collections.Generic.Dictionary<float, System.Collections.Generic.List<BodyItem>>();
        foreach (Node child in _bodiesList.GetChildren())
        {
            if (child is BodyItem bi)
            {
                var pos = bi.ToParams()["Position"].AsVector3();
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

     private void RedistributeBodiesInRing(System.Collections.Generic.List<BodyItem> bodies, float radius)
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
        //GD.Print("Body item updated");
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

     private void LoadTemplates()
     {
         foreach (Node child in _templatesList.GetChildren())
         {
             child.QueueFree();
         }
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
             child.QueueFree();
         }

         // Load bodies from TOML
         var filePath = $"res://Configuration/SystemTemplate/{fileName}";
         if (Godot.FileAccess.FileExists(filePath))
         {
             try
             {
                 using var f = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
                 string text = f.GetAsText();
                 using var reader = new StringReader(text);
                 var table = TOML.Parse(reader);

                 if (table.HasKey("bodies") && table["bodies"] is TomlArray bodiesArray)
                 {
                     foreach (var bodyNode in bodiesArray.Children)
                     {
                         if (bodyNode is TomlTable bodyTable)
                         {
                             var typeStr = ReadString(bodyTable, "type", "Star");
                             var position = ReadVector3(bodyTable, "position", Vector3.Zero);
                             var velocity = ReadVector3(bodyTable, "velocity", Vector3.Zero);
                             var mass = ReadFloat(bodyTable, "mass", 1f);

                             var bodyItem = _bodyItemScene.Instantiate<BodyItem>();
                             bodyItem.SetPosition(position);
                             bodyItem.SetVelocity(velocity);
                             // Set mass
                             if (bodyItem.mass != null) bodyItem.mass.Value = Mathf.Clamp(mass, 0f, 100000000f);
                             // Set type - need to map string to enum
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
                                             break;
                                         }
                                     }
                                 }
                             }
                             bodyItem.ItemUpdate += OnBodyItemUpdate;
                             _bodiesList.AddChild(bodyItem);
                         }
                     }
                 }
             }
             catch (Exception e)
             {
                 GD.PrintErr($"Error loading template {fileName}: {e.Message}");
             }
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
         if (node is Tommy.TomlInteger ti) return (float)ti.Value;
         if (node is Tommy.TomlFloat tf) return (float)tf.Value;
         var s = node.ToString();
         if (float.TryParse(s, out var v)) return v;
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
}
