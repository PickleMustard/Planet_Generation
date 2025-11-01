using System;
using Godot;
using PlanetGeneration;
using UtilityLibrary;

namespace UI;

public partial class BodyItem : VBoxContainer
{
    [Signal]
    public delegate void ItemUpdateEventHandler();

    [Export]
    public Button Toggle;

    [Export]
    public Button RemoveItem;

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

    private Godot.Collections.Array individualSatelliteHolder;
    private Godot.Collections.Array satelliteBeltHolder;

    private const float Limit = 10000f; // constrain within ±10,000 units (mass 0..10,000)
    private const float MassLimit = 100000000f; // constrain within ±100,000,000,000 units (mass 0..100,000,000,000)
    private const float SizeLimit = 10000f; // constrain within ±10,000 units (size 0..10,000)

    public override void _EnterTree()
    {
        // Hook remove
        var remove = GetNodeOrNull<Button>("Header/RemoveItem");
        individualSatelliteHolder = new Godot.Collections.Array();
        satelliteBeltHolder = new Godot.Collections.Array();
        if (remove != null)
        {
            remove.Pressed += () => OnRemoveRequested?.Invoke(this);
        }

        // Cache field nodes if not set via exported references
        OptionButton ??= GetNodeOrNull<OptionButton>("Content/BodyTypeContent/OptionButton");
        X ??= GetNodeOrNull<SpinBox>("Content/PositionContent/X");
        Y ??= GetNodeOrNull<SpinBox>("Content/PositionContent/Y");
        Z ??= GetNodeOrNull<SpinBox>("Content/PositionContent/Z");
        velX ??= GetNodeOrNull<SpinBox>("Content/VelocityContent/velX");
        velY ??= GetNodeOrNull<SpinBox>("Content/VelocityContent/velY");
        velZ ??= GetNodeOrNull<SpinBox>("Content/VelocityContent/velZ");
        mass ??= GetNodeOrNull<SpinBox>("Content/MassContent/mass");
        size ??= GetNodeOrNull<SpinBox>("Content/SizeContent/size");

        // Satellites UI
        AddSatellite ??= GetNodeOrNull<Button>(
            "Content/SatellitesContent/SatellitesHeader/AddSatellite"
        );
        RemoveSatellite ??= GetNodeOrNull<Button>(
            "Content/SatellitesContent/SatellitesHeader/RemoveSatellite"
        );
        SatellitesCountLabel ??= GetNodeOrNull<Label>(
            "Content/SatellitesContent/SatellitesHeader/SatellitesCountLabel"
        );
        SatellitesList ??= GetNodeOrNull<VBoxContainer>(
            "Content/SatellitesContent/SatellitesScroll/SatellitesList"
        );

        _satelliteItemScene = GD.Load<PackedScene>("res://UI/SatelliteItem.tscn");
        _satelliteBeltItemScene = GD.Load<PackedScene>("res://UI/SatelliteBeltItem.tscn");

        // Apply input constraints to fields
        ApplyConstraints();

        if (X != null)
        {
            X.GuiInput += idx => EmitSignal(SignalName.ItemUpdate);
        }
        if (Y != null)
        {
            Y.GuiInput += idx => EmitSignal(SignalName.ItemUpdate);
        }
        if (Z != null)
        {
            Z.GuiInput += idx => EmitSignal(SignalName.ItemUpdate);
        }
        if (velX != null)
        {
            velX.GuiInput += idx => EmitSignal(SignalName.ItemUpdate);
        }
        if (velY != null)
        {
            velY.GuiInput += idx => EmitSignal(SignalName.ItemUpdate);
        }
        if (velZ != null)
        {
            velZ.GuiInput += idx => EmitSignal(SignalName.ItemUpdate);
        }

        // Populate body types and hook selection
        if (OptionButton != null)
        {
            OptionButton.Clear();
            foreach (var name in System.Enum.GetNames(typeof(CelestialBodyType)))
                OptionButton.AddItem(name);

            OptionButton.ItemSelected += idx =>
            {
                var type = (CelestialBodyType)(int)idx;
                UpdateHeaderFromBodyType(OptionButton.GetItemText((int)idx));
                PropogateChangeDown(type);
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

        UpdateSatellitesCountLabel();
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
        GD.Print($"ApplyTemplate: {type}");
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
        GD.Print($"Vertices Per Edge: {baseMesh["vertices_per_edge"]}");
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
        GD.Print($"SetTemplate: {t}");
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
        var headerBtn = GetNodeOrNull<Button>("Header/Toggle");
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

    public void SetSize(float size)
    {
        if (this.size != null)
            this.size.Value = Mathf.Clamp(size, 0f, SizeLimit);
    }

    public Godot.Collections.Dictionary ToParams()
    {
        Godot.Collections.Dictionary dict = new Godot.Collections.Dictionary();
        var ob = GetNode<OptionButton>("Content/BodyTypeContent/OptionButton");
        // Clamp outgoing values as a final safeguard
        float cx = Mathf.Clamp(
            (float)GetNode<SpinBox>("Content/PositionContent/X").Value,
            -Limit,
            Limit
        );
        float cy = Mathf.Clamp(
            (float)GetNode<SpinBox>("Content/PositionContent/Y").Value,
            -Limit,
            Limit
        );
        float cz = Mathf.Clamp(
            (float)GetNode<SpinBox>("Content/PositionContent/Z").Value,
            -Limit,
            Limit
        );

        float cvx = Mathf.Clamp(
            (float)GetNode<SpinBox>("Content/VelocityContent/velX").Value,
            -Limit,
            Limit
        );
        float cvy = Mathf.Clamp(
            (float)GetNode<SpinBox>("Content/VelocityContent/velY").Value,
            -Limit,
            Limit
        );
        float cvz = Mathf.Clamp(
            (float)GetNode<SpinBox>("Content/VelocityContent/velZ").Value,
            -Limit,
            Limit
        );

        float cm = Mathf.Clamp(
            (float)GetNode<SpinBox>("Content/MassContent/mass").Value,
            0f,
            MassLimit
        );
        float cs = Mathf.Clamp(
            (float)GetNode<SpinBox>("Content/SizeContent/size").Value,
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
            if (dict.ContainsKey("Satellites"))
            {
                dict["Satellites"] = satellitesList;
            }
            else
            {
                dict.Add("Satellites", satellitesList);
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
            if (dict.ContainsKey("Satellites"))
            {
                dict["Satellites"] = satellitesList;
            }
            else
            {
                dict.Add("Satellites", satellitesList);
            }
        }

        return dict;
    }

    private void AddSatelliteItem()
    {
        var ob = GetNode<OptionButton>("Content/BodyTypeContent/OptionButton");
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
        var ob = GetNode<OptionButton>("Content/BodyTypeContent/OptionButton");
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
        }
    }

    private void OnSatelliteItemUpdate()
    {
        EmitSignal(SignalName.ItemUpdate);
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
