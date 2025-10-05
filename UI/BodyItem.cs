using Godot;
using System;
using PlanetGeneration;
using UtilityLibrary;

public partial class BodyItem : VBoxContainer
{
    [Signal]
    public delegate void ItemUpdateEventHandler();

    [Export] public Button Toggle;
    [Export] public Button RemoveItem;
    [Export] public OptionButton OptionButton;
    [Export] public SpinBox X;
    [Export] public SpinBox Y;
    [Export] public SpinBox Z;
    [Export] public SpinBox velX;
    [Export] public SpinBox velY;
    [Export] public SpinBox velZ;
    [Export] public SpinBox mass;
    [Export] public SpinBox size;

    // Satellites UI
    [Export] public Button AddSatellite;
    [Export] public Button RemoveSatellite;
    [Export] public Label SatellitesCountLabel;
    [Export] public VBoxContainer SatellitesList;

    public Action<BodyItem> OnRemoveRequested;

    private PackedScene _satelliteItemScene;

    // Mesh parameters
    public int Subdivisions = 1;
    public int[] VerticesPerEdge = { 2 };
    public int NumAbberations = 3;
    public int NumDeformationCycles = 3;
    public int NumContinents = 5;
    public float StressScale = 4.0f;
    public float ShearScale = 1.2f;
    public float MaxPropagationDistance = 0.1f;
    public float PropagationFalloff = 1.5f;
    public float InactiveStressThreshold = 0.1f;
    public float GeneralHeightScale = 1.0f;
    public float GeneralShearScale = 1.2f;
    public float GeneralCompressionScale = 1.75f;
    public float GeneralTransformScale = 1.1f;

    private const float Limit = 10000f; // constrain within ±10,000 units (mass 0..10,000)
    private const float MassLimit = 100000000f; // constrain within ±100,000,000,000 units (mass 0..100,000,000,000)
    private const float SizeLimit = 10000f; // constrain within ±10,000 units (size 0..10,000)

    public override void _EnterTree()
    {
        // Hook remove
        var remove = GetNodeOrNull<Button>("Header/RemoveItem");
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
        AddSatellite ??= GetNodeOrNull<Button>("Content/SatellitesContent/SatellitesHeader/AddSatellite");
        RemoveSatellite ??= GetNodeOrNull<Button>("Content/SatellitesContent/SatellitesHeader/RemoveSatellite");
        SatellitesCountLabel ??= GetNodeOrNull<Label>("Content/SatellitesContent/SatellitesHeader/SatellitesCountLabel");
        SatellitesList ??= GetNodeOrNull<VBoxContainer>("Content/SatellitesContent/SatellitesScroll/SatellitesList");

        _satelliteItemScene = GD.Load<PackedScene>("res://UI/SatelliteItem.tscn");

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
                ApplyTemplate(type);
                HandleSpecialBodyTypes(type);
                EmitSignal(SignalName.ItemUpdate);
            };

            // Ensure a valid initial selection and template
            if (OptionButton.ItemCount > 0)
            {
                if (OptionButton.Selected < 0) OptionButton.Select(0);
                UpdateHeaderFromBodyType(OptionButton.GetItemText(OptionButton.Selected));
                ApplyTemplate((CelestialBodyType)OptionButton.Selected);
                HandleSpecialBodyTypes((CelestialBodyType)OptionButton.Selected);
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

        // Ensure at least one satellite
        if (SatellitesList.GetChildCount() == 0)
        {
            AddSatelliteItem();
        }
        UpdateSatellitesCountLabel();
    }

    private void ApplyConstraints()
    {
        // Positions and velocities: clamp to [-Limit, Limit]
        foreach (var sb in new[] { X, Y, Z, velX, velY, velZ })
        {
            if (sb == null) continue;
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

    public void ApplyTemplate(CelestialBodyType type)
    {
        // Read defaults from TOML in Configuration/SystemGen with safe fallbacks
        var t = SystemGenTemplates.GetDefaults(type);

        // Assign to UI (already clamped by GetDefaults, but clamp again defensively)
        if (X != null) X.Value = Mathf.Clamp(t.Position.X, -Limit, Limit);
        if (Y != null) Y.Value = Mathf.Clamp(t.Position.Y, -Limit, Limit);
        if (Z != null) Z.Value = Mathf.Clamp(t.Position.Z, -Limit, Limit);

        if (velX != null) velX.Value = Mathf.Clamp(t.Velocity.X, -Limit, Limit);
        if (velY != null) velY.Value = Mathf.Clamp(t.Velocity.Y, -Limit, Limit);
        if (velZ != null) velZ.Value = Mathf.Clamp(t.Velocity.Z, -Limit, Limit);

        if (mass != null) mass.Value = Mathf.Clamp(t.Mass, 0f, MassLimit);
        if (size != null) size.Value = Mathf.Clamp(t.Size, 0f, SizeLimit);
    }

    public void UpdateBody()
    {
    }

    public void UpdateHeaderFromBodyType(string typeName)
    {
        var headerBtn = GetNodeOrNull<Button>("Header/Toggle");
        if (headerBtn != null)
        {
            headerBtn.Text = typeName;
        }
    }

    public void SetPosition(Vector3 position)
    {
        if (X != null) X.Value = Mathf.Clamp(position.X, -Limit, Limit);
        if (Y != null) Y.Value = Mathf.Clamp(position.Y, -Limit, Limit);
        if (Z != null) Z.Value = Mathf.Clamp(position.Z, -Limit, Limit);
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
        if (velX != null) velX.Value = Mathf.Clamp(velocity.X, -Limit, Limit);
        if (velY != null) velY.Value = Mathf.Clamp(velocity.Y, -Limit, Limit);
        if (velZ != null) velZ.Value = Mathf.Clamp(velocity.Z, -Limit, Limit);
    }

    public void SetSize(float size)
    {
        if (this.size != null) this.size.Value = Mathf.Clamp(size, 0f, SizeLimit);
    }

    public Godot.Collections.Dictionary ToParams()
    {
        var ob = GetNode<OptionButton>("Content/BodyTypeContent/OptionButton");
        // Clamp outgoing values as a final safeguard
        float cx = Mathf.Clamp((float)GetNode<SpinBox>("Content/PositionContent/X").Value, -Limit, Limit);
        float cy = Mathf.Clamp((float)GetNode<SpinBox>("Content/PositionContent/Y").Value, -Limit, Limit);
        float cz = Mathf.Clamp((float)GetNode<SpinBox>("Content/PositionContent/Z").Value, -Limit, Limit);

        float cvx = Mathf.Clamp((float)GetNode<SpinBox>("Content/VelocityContent/velX").Value, -Limit, Limit);
        float cvy = Mathf.Clamp((float)GetNode<SpinBox>("Content/VelocityContent/velY").Value, -Limit, Limit);
        float cvz = Mathf.Clamp((float)GetNode<SpinBox>("Content/VelocityContent/velZ").Value, -Limit, Limit);

        float cm = Mathf.Clamp((float)GetNode<SpinBox>("Content/MassContent/mass").Value, 0f, MassLimit);
        float cs = Mathf.Clamp((float)GetNode<SpinBox>("Content/SizeContent/size").Value, 0f, SizeLimit);

        Godot.Collections.Dictionary dict = new Godot.Collections.Dictionary();
        dict.Add("Position", new Vector3(cx, cy, cz));
        dict.Add("Velocity", new Vector3(cvx, cvy, cvz));
        dict.Add("Mass", cm);
        dict.Add("Type", Enum.GetName(typeof(CelestialBodyType), (CelestialBodyType)ob.Selected));
        dict.Add("Size", cs);

        // Add mesh parameters
        var meshDict = new Godot.Collections.Dictionary();
        meshDict.Add("subdivisions", Subdivisions);
        meshDict.Add("vertices_per_edge", VerticesPerEdge);
        meshDict.Add("num_abberations", NumAbberations);
        meshDict.Add("num_deformation_cycles", NumDeformationCycles);

        var tectonicDict = new Godot.Collections.Dictionary();
        tectonicDict.Add("num_continents", NumContinents);
        tectonicDict.Add("stress_scale", StressScale);
        tectonicDict.Add("shear_scale", ShearScale);
        tectonicDict.Add("max_propagation_distance", MaxPropagationDistance);
        tectonicDict.Add("propagation_falloff", PropagationFalloff);
        tectonicDict.Add("inactive_stress_threshold", InactiveStressThreshold);
        tectonicDict.Add("general_height_scale", GeneralHeightScale);
        tectonicDict.Add("general_shear_scale", GeneralShearScale);
        tectonicDict.Add("general_compression_scale", GeneralCompressionScale);
        tectonicDict.Add("general_transform_scale", GeneralTransformScale);

        meshDict.Add("tectonic", tectonicDict);
        dict.Add("mesh", meshDict);

        return dict;
    }

    private void HandleSpecialBodyTypes(CelestialBodyType type)
    {
        // For AsteroidBelt, Comet, IceBelt, generate multiple satellites
        if (type == CelestialBodyType.AsteroidBelt || type == CelestialBodyType.Comet || type == CelestialBodyType.IceBelt)
        {
            // Clear existing satellites
            foreach (Node child in SatellitesList.GetChildren())
            {
                child.QueueFree();
            }

            // Add multiple satellites based on type
            int numSatellites = 5; // Default number
            for (int i = 0; i < numSatellites; i++)
            {
                AddSatelliteItem();
            }
        }
        else
        {
            // For other types, clear satellites
            foreach (Node child in SatellitesList.GetChildren())
            {
                child.QueueFree();
            }
        }
        UpdateSatellitesCountLabel();
    }

    private void AddSatelliteItem()
    {
        if (SatellitesList == null || _satelliteItemScene == null) return;
        var satelliteItem = _satelliteItemScene.Instantiate<SatelliteItem>();
        satelliteItem.OnRemoveRequested += RemoveSatelliteItem;
        satelliteItem.ItemUpdate += OnSatelliteItemUpdate;
        SatellitesList.AddChild(satelliteItem);
        UpdateSatellitesCountLabel();
    }

    private void RemoveLastSatelliteItem()
    {
        if (SatellitesList.GetChildCount() <= 1) return; // Keep at least one satellite
        var last = SatellitesList.GetChild(SatellitesList.GetChildCount() - 1);
        last.QueueFree();
        UpdateSatellitesCountLabel();
    }

    private void RemoveSatelliteItem(Node item)
    {
        if (IsInstanceValid(item) && item.GetParent() == SatellitesList)
        {
            item.QueueFree();
            UpdateSatellitesCountLabel();
        }
    }

    private void UpdateSatellitesCountLabel()
    {
        if (SatellitesCountLabel != null)
        {
            int count = SatellitesList.GetChildCount();
            SatellitesCountLabel.Text = count == 1 ? "1 satellite" : $"{count} satellites";
        }
    }

    private void OnSatelliteItemUpdate()
    {
        EmitSignal(SignalName.ItemUpdate);
    }

    private void RemoveSatelliteItem(SatelliteItem item)
    {
        if (IsInstanceValid(item) && item.GetParent() == SatellitesList && SatellitesList.GetChildCount() > 1)
        {
            item.QueueFree();
            UpdateSatellitesCountLabel();
        }
    }
}

