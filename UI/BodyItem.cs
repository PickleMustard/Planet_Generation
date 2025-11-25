using System;
using Godot;
using UtilityLibrary;
using Structures.Enums;

namespace UI;

public partial class BodyItem : HBoxContainer
{
    [Signal]
    public delegate void ItemUpdateEventHandler();

    [Signal]
    public delegate void ExpandMenuEventHandler(String sender, bool toggle);

    [Signal]
    public delegate void ShouldAutoCalculateEventHandler(bool shouldAutoCalculate, BodyItem item);

    [Signal]
    public delegate void RecalculateVelocityEventHandler(BodyItem item);

    [Signal]
    public delegate void RecalculatePositionEventHandler(BodyItem item);

    [Export]
    public Button Toggle;

    [Export]
    public Button RemoveItem;

    [Export]
    public Button DetailsToggle;

    [Export]
    public CheckButton AutoCalculateToggle;

    [Export]
    public OptionButton OptionButton;

    [Export]
    public SpinBox X;

    [Export]
    public SpinBox Y;

    [Export]
    public SpinBox Z;

    [Export]
    public SpinBox velX;

    [Export]
    public SpinBox velY;

    [Export]
    public SpinBox velZ;

    [Export]
    public SpinBox mass;

    [Export]
    public SpinBox size;

    // Satellites UI
    [Export]
    public Button AddSatellite;

    [Export]
    public Button RemoveSatellite;

    [Export]
    public Label SatellitesCountLabel;

    [Export]
    public VBoxContainer SatellitesList;

    [Export]
    public VBoxContainer DetailsPanel;

    [Export]
    public ScrollContainer SatellitesScroll;

    public Action<BodyItem> OnRemoveRequested;

    private String bodyName;

    //Hidden Values
    //Base Mesh
    private int subdivisions;
    private int[,] verticesPerEdge;
    private int numAbberations;
    private int numDeformationCycles;
    //Tectonics
    private int[] numContinents;
    private float[] stressScale;
    private float[] shearScale;
    private float[] maxPropagationDistance;
    private float[] propagationFalloff;
    private float[] inactiveStressThreshold;
    private float[] generalHeightScale;
    private float[] generalShearScale;
    private float[] generalCompressionScale;
    private float[] generalTransformScale;

    private PackedScene _satelliteItemScene;
    private PackedScene _satelliteBeltItemScene;
    private PackedScene _detailPanelScene;

    private Godot.Collections.Array individualSatelliteHolder;
    private Godot.Collections.Array satelliteBeltHolder;

    private const float Limit = 10000f; // constrain within ±10,000 units (mass 0..10,000)
    private const float MassLimit = 100000000f; // constrain within ±100,000,000,000 units (mass 0..100,000,000,000)
    private const float SizeLimit = 10000f; // constrain within ±10,000 units (size 0..10,000)
    private Vector2 SATELLITE_SCROLL_MIN_SIZE = new Vector2(0, 200);

    public override void _EnterTree()
    {
        // Hook remove
        var remove = GetNodeOrNull<Button>("MainContent/Header/RemoveItem");
        individualSatelliteHolder = new Godot.Collections.Array();
        satelliteBeltHolder = new Godot.Collections.Array();
        if (remove != null)
        {
            remove.Pressed += () => OnRemoveRequested?.Invoke(this);
        }

        // Hook details toggle
        DetailsToggle ??= GetNodeOrNull<Button>("MainContent/Header/DetailsToggle");
        if (DetailsToggle != null)
        {
            DetailsToggle.Pressed += ToggleDetailsPanel;
        }

        // Cache field nodes if not set via exported references
        OptionButton ??= GetNodeOrNull<OptionButton>("MainContent/Content/BodyTypeContent/OptionButton");
        X ??= GetNodeOrNull<SpinBox>("MainContent/Content/PositionContent/X");
        Y ??= GetNodeOrNull<SpinBox>("MainContent/Content/PositionContent/Y");
        Z ??= GetNodeOrNull<SpinBox>("MainContent/Content/PositionContent/Z");
        velX ??= GetNodeOrNull<SpinBox>("MainContent/Content/VelocityContent/velX");
        velY ??= GetNodeOrNull<SpinBox>("MainContent/Content/VelocityContent/velY");
        velZ ??= GetNodeOrNull<SpinBox>("MainContent/Content/VelocityContent/velZ");
        mass ??= GetNodeOrNull<SpinBox>("MainContent/Content/MassContent/mass");
        size ??= GetNodeOrNull<SpinBox>("MainContent/Content/SizeContent/size");
        DetailsPanel ??= GetNodeOrNull<VBoxContainer>("DetailsPanel");
        SatellitesScroll ??= GetNodeOrNull<ScrollContainer>("MainContent/Content/SatellitesContent/SatellitesScroll");
        AutoCalculateToggle ??= GetNodeOrNull<CheckButton>("MainContent/Header/AutoCalculateToggle");

        // Satellites UI
        AddSatellite ??= GetNodeOrNull<Button>(
            "MainContent/Content/SatellitesContent/SatellitesHeader/AddSatellite"
        );
        RemoveSatellite ??= GetNodeOrNull<Button>(
            "MainContent/Content/SatellitesContent/SatellitesHeader/RemoveSatellite"
        );
        SatellitesCountLabel ??= GetNodeOrNull<Label>(
            "MainContent/Content/SatellitesContent/SatellitesHeader/SatellitesCountLabel"
        );
        SatellitesList ??= GetNodeOrNull<VBoxContainer>(
            "MainContent/Content/SatellitesContent/SatellitesScroll/SatellitesList"
        );

        _satelliteItemScene = GD.Load<PackedScene>("res://UI/SatelliteItem.tscn");
        _satelliteBeltItemScene = GD.Load<PackedScene>("res://UI/SatelliteBeltItem.tscn");
        _detailPanelScene = GD.Load<PackedScene>("res://UI/DetailPanel.tscn");

        // Apply input constraints to fields
        ApplyConstraints();

        // Populate body types and hook selection
        if (OptionButton != null)
        {
            OptionButton.Clear();
            foreach (var name in System.Enum.GetNames(typeof(CelestialBodyType)))
                OptionButton.AddItem(name);

            OptionButton.ItemSelected += idx =>
            {
                var type = (CelestialBodyType)(int)idx;
                PropogateChangeDown(type);
                UpdateHeaderFromBodyType(OptionButton.GetItemText((int)idx));
                ApplyTemplate(type);
                EmitSignal(SignalName.ItemUpdate);
            };

            // Ensure a valid initial selection and template
            if (OptionButton.ItemCount > 0)
            {
                if (OptionButton.Selected < 0)
                    OptionButton.Select(0);
                ApplyTemplate((CelestialBodyType)OptionButton.Selected);
                UpdateHeaderFromBodyType(OptionButton.GetItemText(OptionButton.Selected));
            }
        }

        // Satellites UI handlers
        if (AddSatellite != null)
        {
            AddSatellite.Pressed += AddSatelliteItem;
        }
        if (RemoveSatellite != null)
        {
            RemoveSatellite.Pressed += RemoveLastSatelliteItem;
        }

        if (X != null)
        {
            X.GetLineEdit().TextSubmitted += idx => EmitSignal(SignalName.RecalculateVelocity, this);
        }
        if (Y != null)
        {
            Y.GetLineEdit().TextSubmitted += idx => EmitSignal(SignalName.RecalculateVelocity, this);
        }
        if (Z != null)
        {
            Z.GetLineEdit().TextSubmitted += idx => EmitSignal(SignalName.RecalculateVelocity, this);
        }
        if (velX != null)
        {
            velX.GetLineEdit().TextSubmitted += idx => EmitSignal(SignalName.RecalculatePosition, this);
        }
        if (velY != null)
        {
            velY.GetLineEdit().TextSubmitted += idx => EmitSignal(SignalName.RecalculatePosition, this);
        }
        if (velZ != null)
        {
            velZ.GetLineEdit().TextSubmitted += idx => EmitSignal(SignalName.RecalculatePosition, this);
        }
        if (AutoCalculateToggle != null)
        {
            AutoCalculateToggle.Pressed += UpdateAutoCalculate;
        }
        UpdateSatellitesCountLabel();
    }

    public void UpdateAutoCalculate()
    {
        GD.Print($"Emitting AutoCalculate, AutoCalculateToggle.ButtonPressed: {AutoCalculateToggle.ButtonPressed}");
        EmitSignal(SignalName.ShouldAutoCalculate, AutoCalculateToggle.ButtonPressed, this);
    }

    private void ApplyConstraints()
    {
        // Positions and velocities: clamp to [-Limit, Limit]
        foreach (var sb in new[] { X, Y, Z, velX, velY, velZ })
        {
            if (sb == null)
                continue;
            sb.MinValue = -Limit;
            sb.MaxValue = Limit;
            sb.AllowGreater = false;
            sb.AllowLesser = false;
        }
        // Mass: [0, Limit]
        if (mass != null)
        {
            mass.MinValue = 0.0;
            mass.MaxValue = MassLimit;
            mass.AllowGreater = true;
            mass.AllowLesser = false;
        }
    }

    public void PropogateChangeDown(CelestialBodyType type)
    {
        foreach (var si in SatellitesList.GetChildren())
        {
            SatellitesList.RemoveChild(si);
        }
        if (type == CelestialBodyType.BlackHole || type == CelestialBodyType.Star)
        {
            if (satelliteBeltHolder.Count > 0)
            {
                SatellitesScroll.CustomMinimumSize = SATELLITE_SCROLL_MIN_SIZE;
                foreach (SatelliteBeltItem item in satelliteBeltHolder)
                {
                    SatellitesList.AddChild((SatelliteBeltItem)item);
                }
            }
        }
        else
        {
            if (individualSatelliteHolder.Count > 0)
            {
                SatellitesScroll.CustomMinimumSize = SATELLITE_SCROLL_MIN_SIZE;
                foreach (SatelliteItem item in individualSatelliteHolder)
                {
                    SatellitesList.AddChild((SatelliteItem)item);
                }
            }
        }
        UpdateSatellitesCountLabel();
    }

    public void ApplyTemplate(CelestialBodyType type)
    {
        // Read defaults from TOML in Configuration/SystemGen with safe fallbacks
        var t = SystemGenTemplates.GetCelestialBodyDefaults(type);
        var template = (Godot.Collections.Dictionary)t["template"];
        bodyName = PickName((Godot.Collections.Dictionary)t["possible_names"]);


        // Assign to UI (already clamped by GetDefaults, but clamp again defensively)
        var position = (Vector3)template["position"];
        var velocity = (Vector3)template["velocity"];
        if (X != null)
            X.Value = Mathf.Clamp(position.X, -Limit, Limit);
        if (Y != null)
            Y.Value = Mathf.Clamp(position.Y, -Limit, Limit);
        if (Z != null)
            Z.Value = Mathf.Clamp(position.Z, -Limit, Limit);

        if (velX != null)
            velX.Value = Mathf.Clamp(velocity.X, -Limit, Limit);
        if (velY != null)
            velY.Value = Mathf.Clamp(velocity.Y, -Limit, Limit);
        if (velZ != null)
            velZ.Value = Mathf.Clamp(velocity.Z, -Limit, Limit);

        if (mass != null)
            mass.Value = Mathf.Clamp((float)template["mass"], 0f, MassLimit);
        if (size != null)
            size.Value = Mathf.Clamp((float)template["size"], 0f, SizeLimit);

        var baseMesh = (Godot.Collections.Dictionary)t["base_mesh"];
        subdivisions = (int)baseMesh["subdivisions"];
        verticesPerEdge = new int[subdivisions, 2];
        Godot.Collections.Array<Godot.Collections.Array<int>> vpeArray = (Godot.Collections.Array<Godot.Collections.Array<int>>)baseMesh["vertices_per_edge"];
        for (int i = 0; i < subdivisions; i++)
        {
            verticesPerEdge[i, 0] = (int)vpeArray[i][0];
            verticesPerEdge[i, 1] = (int)vpeArray[i][1];
        }
        numAbberations = (int)baseMesh["num_abberations"];
        numDeformationCycles = (int)baseMesh["num_deformation_cycles"];

        var tectonics = (Godot.Collections.Dictionary)t["tectonics"];
        numContinents = (int[])tectonics["num_continents"];
        stressScale = (float[])tectonics["stress_scale"];
        shearScale = (float[])tectonics["shear_scale"];
        maxPropagationDistance = (float[])tectonics["max_propagation_distance"];
        propagationFalloff = (float[])tectonics["propagation_falloff"];
        inactiveStressThreshold = (float[])tectonics["inactive_stress_threshold"];
        generalHeightScale = (float[])tectonics["general_height_scale"];
        generalShearScale = (float[])tectonics["general_shear_scale"];
        generalCompressionScale = (float[])tectonics["general_compression_scale"];
        generalTransformScale = (float[])tectonics["general_transform_scale"];

    }

    public void SetTemplate(Godot.Collections.Dictionary t)
    {
        var template = (Godot.Collections.Dictionary)t["template"];

        // Assign to UI (already clamped by GetDefaults, but clamp again defensively)
        var position = (Vector3)template["position"];
        var velocity = (Vector3)template["velocity"];
        if (X != null)
            X.Value = Mathf.Clamp(position.X, -Limit, Limit);
        if (Y != null)
            Y.Value = Mathf.Clamp(position.Y, -Limit, Limit);
        if (Z != null)
            Z.Value = Mathf.Clamp(position.Z, -Limit, Limit);

        if (velX != null)
            velX.Value = Mathf.Clamp(velocity.X, -Limit, Limit);
        if (velY != null)
            velY.Value = Mathf.Clamp(velocity.Y, -Limit, Limit);
        if (velZ != null)
            velZ.Value = Mathf.Clamp(velocity.Z, -Limit, Limit);

        if (mass != null)
            mass.Value = Mathf.Clamp((float)template["mass"], 0f, MassLimit);
        if (size != null)
            size.Value = Mathf.Clamp((float)template["size"], 0f, SizeLimit);

        var baseMesh = (Godot.Collections.Dictionary)t["base_mesh"];
        subdivisions = (int)baseMesh["subdivisions"];
        verticesPerEdge = new int[subdivisions, 2];
        Godot.Collections.Array<Godot.Collections.Array<int>> vpeArray = (Godot.Collections.Array<Godot.Collections.Array<int>>)baseMesh["vertices_per_edge"];
        for (int i = 0; i < subdivisions; i++)
        {
            verticesPerEdge[i, 0] = (int)vpeArray[i][0];
            verticesPerEdge[i, 1] = (int)vpeArray[i][1];
        }
        numAbberations = (int)baseMesh["num_abberations"];
        numDeformationCycles = (int)baseMesh["num_deformation_cycles"];

        var tectonics = (Godot.Collections.Dictionary)t["tectonics"];
        numContinents = (int[])tectonics["num_continents"];
        stressScale = (float[])tectonics["stress_scale"];
        shearScale = (float[])tectonics["shear_scale"];
        maxPropagationDistance = (float[])tectonics["max_propagation_distance"];
        propagationFalloff = (float[])tectonics["propagation_falloff"];
        inactiveStressThreshold = (float[])tectonics["inactive_stress_threshold"];
        generalHeightScale = (float[])tectonics["general_height_scale"];
        generalShearScale = (float[])tectonics["general_shear_scale"];
        generalCompressionScale = (float[])tectonics["general_compression_scale"];
        generalTransformScale = (float[])tectonics["general_transform_scale"];

        if (t.ContainsKey("satellites"))
        {
            SatellitesScroll.CustomMinimumSize = SATELLITE_SCROLL_MIN_SIZE;
            var satellites = (Godot.Collections.Array)t["satellites"];
            var ob = GetNode<OptionButton>("MainContent/Content/BodyTypeContent/OptionButton");

            foreach (Godot.Collections.Dictionary sat in satellites)
            {
                if ((CelestialBodyType)ob.Selected == CelestialBodyType.BlackHole || (CelestialBodyType)ob.Selected == CelestialBodyType.Star)
                {
                    var typeStr = (string)sat["type"];
                    var satItem = _satelliteBeltItemScene.Instantiate<SatelliteBeltItem>();
                    SatellitesList.AddChild(satItem);
                    satItem.SubscribeEvents();
                    if (Enum.TryParse<SatelliteGroupTypes>(typeStr, out var type))
                    {
                        if (satItem.OptionButton != null)
                        {
                            for (int i = 0; i < satItem.OptionButton.ItemCount; i++)
                            {
                                if (satItem.OptionButton.GetItemText(i) == typeStr)
                                {
                                    satItem.OptionButton.Select(i);
                                    satItem.UpdateHeaderFromType(typeStr);
                                    satItem.SetTemplate(sat);
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    var typeStr = (string)sat["type"];
                    var satTemplate = (Godot.Collections.Dictionary)sat["template"];
                    var satPosition = (Vector3)satTemplate["position"];
                    var satVelocity = (Vector3)satTemplate["velocity"];
                    var mass = (float)satTemplate["mass"];
                    var size = (int)satTemplate["size"];

                    var satItem = _satelliteItemScene.Instantiate<SatelliteItem>();
                    SatellitesList.AddChild(satItem);
                    satItem.SubscribeEvents();
                    if (Enum.TryParse<SatelliteBodyType>(typeStr, out var type))
                    {
                        // Set the option button to the correct type
                        if (satItem.OptionButton != null)
                        {
                            for (int i = 0; i < satItem.OptionButton.ItemCount; i++)
                            {
                                if (satItem.OptionButton.GetItemText(i) == typeStr)
                                {
                                    satItem.OptionButton.Select(i);
                                    satItem.UpdateHeaderFromType(typeStr);
                                    satItem.SetTemplate(sat);
                                    break;
                                }
                            }
                        }
                    }
                    satItem.SetPosition(position);
                    satItem.SetVelocity(velocity);
                    satItem.SetSize(size);
                    // Set mass
                    if (satItem.mass != null)
                        satItem.mass.Value = Mathf.Clamp(mass, 0f, 100000000f);
                    // Set templateDict directly from loaded data to preserve custom settings
                    satItem.ItemUpdate += OnSatelliteItemUpdate;
                }
            }
            UpdateSatellitesCountLabel();

        }

    }

    public String PickName(Godot.Collections.Dictionary nameDict)
    {
        if (nameDict == null || nameDict.Count == 0)
            return "";

        var categories = new Godot.Collections.Array(nameDict.Keys);
        if (categories.Count == 0)
            return "";

        var random = UtilityLibrary.Randomizer.rng;
        var selectedCategory = (string)categories[random.RandiRange(0, categories.Count - 1)];

        var names = (Godot.Collections.Array)nameDict[selectedCategory];
        if (names == null || names.Count == 0)
            return "";

        return (string)names[random.RandiRange(0, names.Count - 1)];
    }

    public void UpdateBody() { }

    public void UpdateHeaderFromBodyType(string typeName)
    {
        var headerBtn = GetNodeOrNull<Button>("MainContent/Header/Toggle");
        if (headerBtn != null)
        {
            headerBtn.Text = $"{bodyName} ({typeName})";
        }
    }

    public Vector3 GetBodyPosition()
    {
        return new Vector3(Mathf.Clamp((float)X.Value, -Limit, Limit), Mathf.Clamp((float)Y.Value, -Limit, Limit), Mathf.Clamp((float)Z.Value, -Limit, Limit));
    }
    public void SetPosition(Vector3 position)
    {
        if (X != null)
            X.Value = Mathf.Clamp(position.X, -Limit, Limit);
        if (Y != null)
            Y.Value = Mathf.Clamp(position.Y, -Limit, Limit);
        if (Z != null)
            Z.Value = Mathf.Clamp(position.Z, -Limit, Limit);
    }

    public Vector3 GetVelocity()
    {
        float vx = Mathf.Clamp((float)velX.Value, -Limit, Limit);
        float vy = Mathf.Clamp((float)velY.Value, -Limit, Limit);
        float vz = Mathf.Clamp((float)velZ.Value, -Limit, Limit);
        return new Vector3(vx, vy, vz);
    }

    public void SetVelocity(Vector3 velocity)
    {
        if (velX != null)
            velX.Value = Mathf.Clamp(velocity.X, -Limit, Limit);
        if (velY != null)
            velY.Value = Mathf.Clamp(velocity.Y, -Limit, Limit);
        if (velZ != null)
            velZ.Value = Mathf.Clamp(velocity.Z, -Limit, Limit);
    }

    public float GetBodySize()
    {
        return (float)size.Value;
    }

    public float GetBodyMass()
    {
        return (float)mass.Value;
    }

    public void SetSize(float size)
    {
        if (this.size != null)
            this.size.Value = Mathf.Clamp(size, 0f, SizeLimit);
    }

    public Godot.Collections.Dictionary ToParams()
    {
        Godot.Collections.Dictionary dict = new Godot.Collections.Dictionary();
        var ob = GetNode<OptionButton>("MainContent/Content/BodyTypeContent/OptionButton");
        // Clamp outgoing values as a final safeguard
        float cx = Mathf.Clamp(
            (float)GetNode<SpinBox>("MainContent/Content/PositionContent/X").Value,
            -Limit,
            Limit
        );
        float cy = Mathf.Clamp(
            (float)GetNode<SpinBox>("MainContent/Content/PositionContent/Y").Value,
            -Limit,
            Limit
        );
        float cz = Mathf.Clamp(
            (float)GetNode<SpinBox>("MainContent/Content/PositionContent/Z").Value,
            -Limit,
            Limit
        );

        float cvx = Mathf.Clamp(
            (float)GetNode<SpinBox>("MainContent/Content/VelocityContent/velX").Value,
            -Limit,
            Limit
        );
        float cvy = Mathf.Clamp(
            (float)GetNode<SpinBox>("MainContent/Content/VelocityContent/velY").Value,
            -Limit,
            Limit
        );
        float cvz = Mathf.Clamp(
            (float)GetNode<SpinBox>("MainContent/Content/VelocityContent/velZ").Value,
            -Limit,
            Limit
        );

        float cm = Mathf.Clamp(
            (float)GetNode<SpinBox>("MainContent/Content/MassContent/mass").Value,
            0f,
            MassLimit
        );
        float cs = Mathf.Clamp(
            (float)GetNode<SpinBox>("MainContent/Content/SizeContent/size").Value,
            0f,
            SizeLimit
        );

        dict["type"] = Enum.GetName(
            typeof(CelestialBodyType),
            (CelestialBodyType)ob.Selected
        );
        dict.Add("name", bodyName);
        var templateDict = new Godot.Collections.Dictionary();
        templateDict.Add("position", new Vector3(cx, cy, cz));
        templateDict.Add("velocity", new Vector3(cvx, cvy, cvz));
        templateDict.Add("mass", cm);
        templateDict.Add("size", cs);
        dict.Add("template", templateDict);
        var baseMesh = new Godot.Collections.Dictionary();
        baseMesh.Add("subdivisions", subdivisions);
        Godot.Collections.Array<Godot.Collections.Array<int>> vpeArray = new Godot.Collections.Array<Godot.Collections.Array<int>>();
        for (int i = 0; i < subdivisions; i++)
        {
            Godot.Collections.Array<int> row = new Godot.Collections.Array<int>();
            row.Add(verticesPerEdge[i, 0]);
            row.Add(verticesPerEdge[i, 1]);
            vpeArray.Add(row);
        }
        baseMesh.Add("vertices_per_edge", vpeArray);
        baseMesh.Add("num_abberations", numAbberations);
        baseMesh.Add("num_deformation_cycles", numDeformationCycles);
        dict.Add("base_mesh", baseMesh);
        var tectonics = new Godot.Collections.Dictionary();
        tectonics.Add("num_continents", numContinents);
        tectonics.Add("stress_scale", stressScale);
        tectonics.Add("shear_scale", shearScale);
        tectonics.Add("max_propagation_distance", maxPropagationDistance);
        tectonics.Add("propagation_falloff", propagationFalloff);
        tectonics.Add("inactive_stress_threshold", inactiveStressThreshold);
        tectonics.Add("general_height_scale", generalHeightScale);
        tectonics.Add("general_shear_scale", generalShearScale);
        tectonics.Add("general_compression_scale", generalCompressionScale);
        tectonics.Add("general_transform_scale", generalTransformScale);
        dict.Add("tectonics", tectonics);


        // Add satellites
        var satellitesList = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (
            (CelestialBodyType)ob.Selected == CelestialBodyType.Star
            || (CelestialBodyType)ob.Selected == CelestialBodyType.BlackHole
        )
        {
            foreach (Node child in SatellitesList.GetChildren())
            {
                if (child is SatelliteBeltItem sbi)
                {
                    satellitesList.Add(sbi.ToParams());
                }
            }
            if (dict.ContainsKey("satellites"))
            {
                dict["satellites"] = satellitesList;
            }
            else
            {
                dict.Add("satellites", satellitesList);
            }
            GD.Print($"Satellites List Size: {satellitesList.Count}");
        }
        else
        {
            foreach (Node child in SatellitesList.GetChildren())
            {
                if (child is SatelliteItem si)
                {
                    satellitesList.Add(si.ToParams());
                }
            }
            if (dict.ContainsKey("satellites"))
            {
                dict["satellites"] = satellitesList;
            }
            else
            {
                dict.Add("satellites", satellitesList);
            }
        }

        return dict;
    }

    private void AddSatelliteItem()
    {
        SatellitesScroll.CustomMinimumSize = SATELLITE_SCROLL_MIN_SIZE;
        var ob = GetNode<OptionButton>("MainContent/Content/BodyTypeContent/OptionButton");
        if (SatellitesList == null || _satelliteItemScene == null)
            return;
        if (
            (CelestialBodyType)ob.Selected == CelestialBodyType.BlackHole
            || (CelestialBodyType)ob.Selected == CelestialBodyType.Star
        )
        {
            var satelliteBeltItem = _satelliteBeltItemScene.Instantiate<SatelliteBeltItem>();
            satelliteBeltItem.SetParentType((CelestialBodyType)ob.Selected);
            satelliteBeltItem.OnRemoveRequested += RemoveSatelliteItem;
            satelliteBeltItem.ItemUpdate += OnSatelliteItemUpdate;
            SatellitesList.AddChild(satelliteBeltItem);
            satelliteBeltItem.SubscribeEvents();
            satelliteBeltHolder.Add(satelliteBeltItem);
        }
        else
        {
            var satelliteItem = _satelliteItemScene.Instantiate<SatelliteItem>();
            satelliteItem.SetParentType((CelestialBodyType)ob.Selected);
            satelliteItem.OnRemoveRequested += RemoveSatelliteItem;
            satelliteItem.ItemUpdate += OnSatelliteItemUpdate;
            SatellitesList.AddChild(satelliteItem);
            satelliteItem.SubscribeEvents();
            individualSatelliteHolder.Add(satelliteItem);
            RedistributeSatelliteRings();
        }
        UpdateSatellitesCountLabel();
    }

    private void RedistributeSatelliteRings()
    {
        var bodiesByRing = new System.Collections.Generic.Dictionary<
            float,
            System.Collections.Generic.List<SatelliteItem>
        >();
        foreach (Node child in SatellitesList.GetChildren())
        {
            if (child is SatelliteItem bi)
            {
                //var pos = ((Godot.Collections.Dictionary)bi.ToParams()["Template"])["position"]
                //    .AsVector3();
                var pos = bi.GetBodyPosition();
                float radius = pos.Length();
                if (!bodiesByRing.ContainsKey(radius))
                    bodiesByRing[radius] = new System.Collections.Generic.List<SatelliteItem>();
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
        System.Collections.Generic.List<SatelliteItem> bodies,
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

    private void RemoveLastSatelliteItem()
    {
        var ob = GetNode<OptionButton>("MainContent/Content/BodyTypeContent/OptionButton");
        if (SatellitesList.GetChildCount() <= 0)
            return; // No negatives
        var index = SatellitesList.GetChildCount() - 1;
        var last = SatellitesList.GetChild(index);
        if (
            (CelestialBodyType)ob.Selected == CelestialBodyType.BlackHole
            || (CelestialBodyType)ob.Selected == CelestialBodyType.Star
        )
        {
            satelliteBeltHolder.RemoveAt(index);
        }
        else
        {
            individualSatelliteHolder.RemoveAt(index);
        }
        SatellitesList.RemoveChild(last);
        last.QueueFree();
        UpdateSatellitesCountLabel();
    }

    private void RemoveSatelliteItem(Node item)
    {
        if (IsInstanceValid(item) && item.GetParent() == SatellitesList)
        {
            if (individualSatelliteHolder.Contains(item))
            {
                individualSatelliteHolder.Remove(item);
            }
            if (satelliteBeltHolder.Contains(item))
            {
                satelliteBeltHolder.Remove(item);
            }
            SatellitesList.RemoveChild(item);
            RedistributeSatelliteRings();
            item.QueueFree();
        }
        UpdateSatellitesCountLabel();
    }

    private void UpdateSatellitesCountLabel()
    {
        if (SatellitesCountLabel != null)
        {
            int count = SatellitesList.GetChildCount();
            SatellitesCountLabel.Text = count <= 1 ? $"{count} satellite" : $"{count} satellites";
            if (count == 0) SatellitesScroll.CustomMinimumSize = Vector2.Zero;
        }
    }

    private void OnSatelliteItemUpdate()
    {
        EmitSignal(SignalName.ItemUpdate);
    }

    private void ToggleDetailsPanel()
    {
        if (DetailsPanel == null) return;

        if (DetailsPanel.GetChildCount() == 0)
        {
            // Create and setup detail panel
            var detailPanel = _detailPanelScene.Instantiate<DetailPanel>();
            DetailsPanel.AddChild(detailPanel);
            detailPanel.SetupForBodyItem();
            detailPanel.ValueChanged += OnDetailValueChanged;

            // Set current values
            UpdateDetailPanelValues();
        }

        DetailsPanel.Visible = !DetailsPanel.Visible;
        EmitSignal(SignalName.ExpandMenu, this.Name, DetailsPanel.Visible);
    }

    private void OnDetailValueChanged()
    {
        // Update hidden values from detail panel
        UpdateHiddenValuesFromDetailPanel();
        EmitSignal(SignalName.ItemUpdate);
    }

    private void UpdateDetailPanelValues()
    {
        if (DetailsPanel?.GetChildCount() > 0)
        {
            var detailPanel = DetailsPanel.GetChild(0) as DetailPanel;
            if (detailPanel != null)
            {
                // Base Mesh values
                detailPanel.SetSubdivisions(subdivisions);
                detailPanel.SetVerticesPerEdge(verticesPerEdge);
                detailPanel.SetAberrations(numAbberations);
                detailPanel.SetDeformationCycles(numDeformationCycles);

                // Tectonics values
                detailPanel.SetTectonicsValues("Continents", Array.ConvertAll(numContinents, x => (float)x));
                detailPanel.SetTectonicsValues("Stress Scale", stressScale);
                detailPanel.SetTectonicsValues("Shear Scale", shearScale);
                detailPanel.SetTectonicsValues("Max Propagation Distance", maxPropagationDistance);
                detailPanel.SetTectonicsValues("Propagation Falloff", propagationFalloff);
                detailPanel.SetTectonicsValues("Inactive Stress Threshold", inactiveStressThreshold);
                detailPanel.SetTectonicsValues("General Height Scale", generalHeightScale);
                detailPanel.SetTectonicsValues("General Shear Scale", generalShearScale);
                detailPanel.SetTectonicsValues("General Compression Scale", generalCompressionScale);
                detailPanel.SetTectonicsValues("General Transform Scale", generalTransformScale);
            }
        }
    }

    private void UpdateHiddenValuesFromDetailPanel()
    {
        if (DetailsPanel?.GetChildCount() > 0)
        {
            var detailPanel = DetailsPanel.GetChild(0) as DetailPanel;
            if (detailPanel != null)
            {
                // Base Mesh values
                subdivisions = detailPanel.GetSubdivisions();
                verticesPerEdge = detailPanel.GetVerticesPerEdge(subdivisions);
                numAbberations = detailPanel.GetAberrations();
                numDeformationCycles = detailPanel.GetDeformationCycles();

                // Tectonics values
                var continentsFloat = detailPanel.GetTectonicsValues("Continents");
                numContinents = Array.ConvertAll(continentsFloat, x => (int)x);
                stressScale = detailPanel.GetTectonicsValues("Stress Scale");
                shearScale = detailPanel.GetTectonicsValues("Shear Scale");
                maxPropagationDistance = detailPanel.GetTectonicsValues("Max Propagation Distance");
                propagationFalloff = detailPanel.GetTectonicsValues("Propagation Falloff");
                inactiveStressThreshold = detailPanel.GetTectonicsValues("Inactive Stress Threshold");
                generalHeightScale = detailPanel.GetTectonicsValues("General Height Scale");
                generalShearScale = detailPanel.GetTectonicsValues("General Shear Scale");
                generalCompressionScale = detailPanel.GetTectonicsValues("General Compression Scale");
                generalTransformScale = detailPanel.GetTectonicsValues("General Transform Scale");
            }
        }
    }

    private void RemoveSatelliteItem(SatelliteItem item)
    {
        if (
            IsInstanceValid(item)
            && item.GetParent() == SatellitesList
            && SatellitesList.GetChildCount() > 1
        )
        {
            if (individualSatelliteHolder.Contains(item))
            {
                individualSatelliteHolder.Remove(item);
            }
            if (satelliteBeltHolder.Contains(item))
            {
                satelliteBeltHolder.Remove(item);
            }
            item.QueueFree();
            RedistributeSatelliteRings();
            UpdateSatellitesCountLabel();
        }
    }
}
