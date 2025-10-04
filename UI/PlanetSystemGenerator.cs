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
    [Signal]
    public delegate void ValidatePressedEventHandler(Array<Dictionary> bodies);

    private VBoxContainer _bodiesList;
    private Label _countLabel;
    private Button _addBtn;
    private Button _removeBtn;
    private Button _generateBtn;
    private Button _validateBtn;
    private PackedScene _bodyItemScene;
    private TabContainer _tabContainer;
    private VBoxContainer _templatesList;
    private TextureRect _stabilityIndicator;

    private Texture2D _checkMark;
    private Texture2D _xMark;

    public override void _Ready()
    {
        this.AddToGroup("GenerationMenu");
        _bodyItemScene = GD.Load<PackedScene>("res://UI/BodyItem.tscn");
        _tabContainer = GetNode<TabContainer>("MarginContainer/TabContainer");
        _bodiesList = GetNode<VBoxContainer>("MarginContainer/TabContainer/BodiesTab/Scroll/BodiesList");
        _countLabel = GetNode<Label>("MarginContainer/TabContainer/BodiesTab/ControlsRow/CountLabel");
        _addBtn = GetNode<Button>("MarginContainer/TabContainer/BodiesTab/ControlsRow/AddBody");
        _removeBtn = GetNode<Button>("MarginContainer/TabContainer/BodiesTab/ControlsRow/RemoveBody");
        _generateBtn = GetNode<Button>("MarginContainer/TabContainer/BodiesTab/GenerationRow/GenerateButton");
        _validateBtn = GetNode<Button>("MarginContainer/TabContainer/BodiesTab/GenerationRow/ValidateButton");
        _stabilityIndicator = GetNode<TextureRect>("MarginContainer/TabContainer/BodiesTab/GenerationRow/StabilityIndicator");
        _templatesList = GetNode<VBoxContainer>("MarginContainer/TabContainer/TemplatesTab/TemplatesScroll/TemplatesList");

        _addBtn.Pressed += AddBodyItem;
        _removeBtn.Pressed += RemoveLastBodyItem;
        _generateBtn.Pressed += OnGeneratePressed;
        _validateBtn.Pressed += OnValidatePressed;

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

    private void OnValidatePressed()
    {
        CheckSystemStability();
        //EmitSignal(SignalName.ValidatePressed, list);

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
                GD.Print(table);

                if (table.HasKey("bodies") && table["bodies"] is TomlArray bodiesArray)
                {
                    foreach (var bodyNode in bodiesArray.Children)
                    {
                        if (bodyNode is TomlTable bodyTable)
                        {
                            var typeStr = ReadString(bodyTable, "type", "Star");
                            GD.Print($"Type: {typeStr}");
                            var position = ReadVector3(bodyTable, "position", Vector3.Zero);
                            var velocity = ReadVector3(bodyTable, "velocity", Vector3.Zero);
                            var mass = ReadFloat(bodyTable, "mass", 1f);
                            var size = ReadInt(bodyTable, "size", 5);

                            // Parse mesh parameters
                            int subdivisions = 1;
                            int[] verticesPerEdge = { 2 };
                            int numAbberations = 3;
                            int numDeformationCycles = 3;
                            int numContinents = 5;
                            float stressScale = 4.0f;
                            float shearScale = 1.2f;
                            float maxPropagationDistance = 0.1f;
                            float propagationFalloff = 1.5f;
                            float inactiveStressThreshold = 0.1f;
                            float generalHeightScale = 1.0f;
                            float generalShearScale = 1.2f;
                            float generalCompressionScale = 1.75f;
                            float generalTransformScale = 1.1f;

                            if (bodyTable.HasKey("mesh") && bodyTable["mesh"] is TomlTable meshTable)
                            {
                                if (meshTable.HasKey("base_mesh") && meshTable["base_mesh"] is TomlArray baseMeshArray)
                                {
                                    foreach (var baseMeshNode in baseMeshArray.Children)
                                    {
                                        if (baseMeshNode is TomlTable baseMeshTable)
                                        {
                                            subdivisions = ReadInt(baseMeshTable, "subdivisions", 1);
                                            numAbberations = ReadInt(baseMeshTable, "num_abberations", 3);
                                            numDeformationCycles = ReadInt(baseMeshTable, "num_deformation_cycles", 3);
                                            verticesPerEdge = ReadIntArray(baseMeshTable, "vertices_per_edge", new int[] { 2 });
                                        }
                                    }
                                }
                                if (meshTable.HasKey("tectonic") && meshTable["tectonic"] is TomlTable tectonicArray)
                                {
                                    foreach (var tectonicNode in tectonicArray.Children)
                                    {
                                        if (tectonicNode is TomlTable tectonicTable)
                                        {
                                            numContinents = ReadInt(tectonicTable, "num_continents", 5);
                                            stressScale = ReadFloat(tectonicTable, "stress_scale", 4.0f);
                                            shearScale = ReadFloat(tectonicTable, "shear_scale", 1.2f);
                                            maxPropagationDistance = ReadFloat(tectonicTable, "max_propagation_distance", 0.1f);
                                            propagationFalloff = ReadFloat(tectonicTable, "propagation_falloff", 1.5f);
                                            inactiveStressThreshold = ReadFloat(tectonicTable, "inactive_stress_threshold", 0.1f);
                                            generalHeightScale = ReadFloat(tectonicTable, "general_height_scale", 1.0f);
                                            generalShearScale = ReadFloat(tectonicTable, "general_shear_scale", 1.2f);
                                            generalCompressionScale = ReadFloat(tectonicTable, "general_compression_scale", 1.75f);
                                            generalTransformScale = ReadFloat(tectonicTable, "general_transform_scale", 1.1f);
                                        }
                                    }
                                }
                            }

                            var bodyItem = _bodyItemScene.Instantiate<BodyItem>();
                            _bodiesList.AddChild(bodyItem);
                            if (Enum.TryParse<CelestialBodyType>(typeStr, out var type))
                            {
                                GD.Print($"Type: {typeStr}, Enum Type: {type}, OptionButton: {bodyItem.OptionButton == null}");
                                // Set the option button to the correct type
                                if (bodyItem.OptionButton != null)
                                {
                                    for (int i = 0; i < bodyItem.OptionButton.ItemCount; i++)
                                    {
                                        if (bodyItem.OptionButton.GetItemText(i) == typeStr)
                                        {
                                            bodyItem.OptionButton.Select(i);
                                            bodyItem.UpdateHeaderFromBodyType(typeStr);
                                            bodyItem.ApplyTemplate(type);
                                            break;
                                        }
                                    }
                                }
                            }
                            bodyItem.SetPosition(position);
                            bodyItem.SetVelocity(velocity);
                            bodyItem.SetSize(size);
                            // Set mass
                            if (bodyItem.mass != null) bodyItem.mass.Value = Mathf.Clamp(mass, 0f, 100000000f);
                            // Set mesh parameters
                            bodyItem.Subdivisions = subdivisions;
                            bodyItem.VerticesPerEdge = verticesPerEdge;
                            bodyItem.NumAbberations = numAbberations;
                            bodyItem.NumDeformationCycles = numDeformationCycles;
                            bodyItem.NumContinents = numContinents;
                            bodyItem.StressScale = stressScale;
                            bodyItem.ShearScale = shearScale;
                            bodyItem.MaxPropagationDistance = maxPropagationDistance;
                            bodyItem.PropagationFalloff = propagationFalloff;
                            bodyItem.InactiveStressThreshold = inactiveStressThreshold;
                            bodyItem.GeneralHeightScale = generalHeightScale;
                            bodyItem.GeneralShearScale = generalShearScale;
                            bodyItem.GeneralCompressionScale = generalCompressionScale;
                            bodyItem.GeneralTransformScale = generalTransformScale;
                            // Set type - need to map string to enum
                            GD.Print($"BodyItem: {bodyItem}");
                            bodyItem.ItemUpdate += OnBodyItemUpdate;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"Error loading template {fileName}: {e.Message} | {e.StackTrace}\n");
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
        try
        {
            if (node is Tommy.TomlInteger ti) return (float)ti.Value;
            if (node is Tommy.TomlFloat tf) return (float)tf.Value;

            var s = node.ToString();
            if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
                s = s.Substring(1, s.Length - 2);
            if (float.TryParse(s, out var v)) return v;
        }
        catch
        {
        }
        return fallback;
    }

    private int ReadInt(TomlTable table, string key, int fallback)
    {
        if (!table.HasKey(key)) return fallback;
        var node = table[key];
        if (node is Tommy.TomlInteger ti) return (int)ti.Value;
        if (node is Tommy.TomlFloat tf) return (int)tf.Value;
        var s = node.ToString();
        if (int.TryParse(s, out var v)) return v;
        if (float.TryParse(s, out var vf)) return (int)vf;
        return fallback;
    }

    private int[] ReadIntArray(TomlTable table, string key, int[] fallback)
    {
        if (!table.HasKey(key)) return fallback;
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
        var systemGenerator = GetNode<SystemGenerator>($"{currentScene.GetPath()}/system_generator");
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

                if (body1.ContainsKey("mesh") && body1["mesh"].AsGodotDictionary().ContainsKey("size"))
                {
                    size1 = body1["mesh"].AsGodotDictionary()["size"].AsSingle();
                }

                if (body2.ContainsKey("mesh") && body2["mesh"].AsGodotDictionary().ContainsKey("size"))
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
}
