using System;
using Godot;
using PlanetGeneration;
using UtilityLibrary;
using UI;

namespace UI;

public partial class SatelliteItem : HBoxContainer
{
    [Signal]
    public delegate void ItemUpdateEventHandler();

    [Export]
    public Button Toggle;

    [Export]
    public Button RemoveItem;

    [Export]
    public Button DetailsToggle;

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

    [Export]
    public VBoxContainer DetailsPanel;

    public Action<SatelliteItem> OnRemoveRequested;

    private CelestialBodyType parentType;
    private int NumberInBelt = 25;
    private const float Limit = 10000f; // constrain within ±10,000 units
    private const float MassLimit = 10000f; // constrain mass 0..10,000
    private const float SizeLimit = 10000f; // constrain size 0..10,000

    private PackedScene _detailPanelScene;

    //Hidden Values
    //Base Mesh
    private int subdivisions;
    private int[,] verticesPerEdge;
    private int numAbberations;
    private int numDeformationCycles;

    //Scaling
    private float[] xScaleRange;
    private float[] yScaleRange;
    private float[] zScaleRange;

    //Noise Settings
    private float[] amplitudeRange;
    private float[] scalingRange;
    private int[] octaveRange;

    private String satName;

    public void SetParentType(CelestialBodyType type)
    {
        this.parentType = type;
    }

    public override void _EnterTree()
    {
        // Cache field nodes if not set via exported references
        OptionButton ??= GetNodeOrNull<OptionButton>("MainContent/Content/TypeContent/OptionButton");
        X ??= GetNodeOrNull<SpinBox>("MainContent/Content/PositionContent/X");
        Y ??= GetNodeOrNull<SpinBox>("MainContent/Content/PositionContent/Y");
        Z ??= GetNodeOrNull<SpinBox>("MainContent/Content/PositionContent/Z");
        velX ??= GetNodeOrNull<SpinBox>("MainContent/Content/VelocityContent/velX");
        velY ??= GetNodeOrNull<SpinBox>("MainContent/Content/VelocityContent/velY");
        velZ ??= GetNodeOrNull<SpinBox>("MainContent/Content/VelocityContent/velZ");
        mass ??= GetNodeOrNull<SpinBox>("MainContent/Content/MassContent/mass");
        size ??= GetNodeOrNull<SpinBox>("MainContent/Content/SizeContent/size");

        // Apply input constraints to fields
        ApplyConstraints();
    }

    public void SubscribeEvents()
    {
        // Hook remove
        var remove = GetNodeOrNull<Button>("MainContent/Header/RemoveItem");
        if (remove != null)
        {
            remove.Pressed += () => OnRemoveRequested?.Invoke(this);
        }

        // Hook details toggle
        DetailsToggle ??= GetNodeOrNull<Button>("MainContent/Header/DetailsToggle");
        DetailsPanel ??= GetNodeOrNull<VBoxContainer>("DetailsPanel");
        if (DetailsToggle != null)
        {
            DetailsToggle.Pressed += ToggleDetailsPanel;
        }

        _detailPanelScene = GD.Load<PackedScene>("res://UI/DetailPanel.tscn");
        // Hook up input events for updates
        var spinBoxes = new[]
        {
            X,
            Y,
            Z,
            velX,
            velY,
            velZ,
            mass,
            size,
        };
        foreach (var sb in spinBoxes)
        {
            if (sb != null)
            {
                sb.ValueChanged += value => EmitSignal(SignalName.ItemUpdate);
            }
        }

        // Populate satellite types and hook selection
        if (OptionButton != null)
        {
            OptionButton.Clear();
            foreach (var name in System.Enum.GetNames(typeof(SatelliteBodyType)))
                OptionButton.AddItem(name);

            OptionButton.ItemSelected += idx =>
            {
                var type = (SatelliteBodyType)(int)idx;
                ApplyTemplate(type);
                UpdateHeaderFromType(OptionButton.GetItemText((int)idx));
                EmitSignal(SignalName.ItemUpdate);
            };

            // Ensure a valid initial selection
            if (OptionButton.ItemCount > 0)
            {
                if (OptionButton.Selected < 0)
                    OptionButton.Select(0);
                ApplyTemplate((SatelliteBodyType)OptionButton.Selected);
                UpdateHeaderFromType(OptionButton.GetItemText(OptionButton.Selected));
            }
        }
    }

    public void ApplyTemplate(SatelliteBodyType type)
    {
        // Read defaults from TOML in Configuration/SystemGen with safe fallbacks
        var t = SystemGenTemplates.GetSatelliteBodyDefaults(type);
        var template = (Godot.Collections.Dictionary)t["template"];
        satName = PickName((Godot.Collections.Dictionary)t["possible_names"]);

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

        var scaling = (Godot.Collections.Dictionary)t["scaling_settings"];
        xScaleRange = (float[])scaling["x_scale_range"];
        yScaleRange = (float[])scaling["y_scale_range"];
        zScaleRange = (float[])scaling["z_scale_range"];

        var noiseSettings = (Godot.Collections.Dictionary)t["noise_settings"];
        amplitudeRange = (float[])noiseSettings["amplitude_range"];
        scalingRange = (float[])noiseSettings["scaling_range"];
        octaveRange = (int[])noiseSettings["octave_range"];
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

        var scaling = (Godot.Collections.Dictionary)t["scaling_settings"];
        xScaleRange = (float[])scaling["x_scale_range"];
        yScaleRange = (float[])scaling["y_scale_range"];
        zScaleRange = (float[])scaling["z_scale_range"];

        var noiseSettings = (Godot.Collections.Dictionary)t["noise_settings"];
        amplitudeRange = (float[])noiseSettings["amplitude_range"];
        scalingRange = (float[])noiseSettings["scaling_range"];
        octaveRange = (int[])noiseSettings["octave_range"];

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
        // Mass: [0, MassLimit]
        if (mass != null)
        {
            mass.MinValue = 0.0;
            mass.MaxValue = MassLimit;
            mass.AllowGreater = false;
            mass.AllowLesser = false;
        }
        // Size: [0, SizeLimit]
        if (size != null)
        {
            size.MinValue = 0.0;
            size.MaxValue = SizeLimit;
            size.AllowGreater = false;
            size.AllowLesser = false;
        }
    }

    public void UpdateHeaderFromType(string typeName)
    {
        var headerBtn = GetNodeOrNull<Button>("MainContent/Header/Toggle");
        if (headerBtn != null)
        {
            headerBtn.Text = $"{satName} ({typeName})";
        }
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

    public new Vector3 GetPosition()
    {
        float x = Mathf.Clamp((float)X.Value, -Limit, Limit);
        float y = Mathf.Clamp((float)Y.Value, -Limit, Limit);
        float z = Mathf.Clamp((float)Z.Value, -Limit, Limit);
        return new Vector3(x, y, z);
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

    public Vector3 GetVelocity()
    {
        float vx = Mathf.Clamp((float)velX.Value, -Limit, Limit);
        float vy = Mathf.Clamp((float)velY.Value, -Limit, Limit);
        float vz = Mathf.Clamp((float)velZ.Value, -Limit, Limit);
        return new Vector3(vx, vy, vz);
    }

    public void SetMass(float massValue)
    {
        if (mass != null)
            mass.Value = Mathf.Clamp(massValue, 0f, MassLimit);
    }

    public float GetMass()
    {
        return Mathf.Clamp((float)mass.Value, 0f, MassLimit);
    }

    public void SetSize(float sizeValue)
    {
        if (size != null)
            size.Value = Mathf.Clamp(sizeValue, 0f, SizeLimit);
    }

    public new float GetSize()
    {
        return Mathf.Clamp((float)size.Value, 0f, SizeLimit);
    }

    public string GetSatelliteType()
    {
        if (OptionButton != null && OptionButton.Selected >= 0)
        {
            return OptionButton.GetItemText(OptionButton.Selected);
        }
        return "Asteroid";
    }

    public Vector3 GetBodyPosition()
    {
        return new Vector3(Mathf.Clamp((float)X.Value, -Limit, Limit), Mathf.Clamp((float)Y.Value, -Limit, Limit), Mathf.Clamp((float)Z.Value, -Limit, Limit));
    }

    public Godot.Collections.Dictionary ToParams()
    {
        Godot.Collections.Dictionary dict = new Godot.Collections.Dictionary();
        dict.Add("type", GetSatelliteType());
        dict.Add("name", satName);
        Godot.Collections.Dictionary templateDict = new Godot.Collections.Dictionary();
        templateDict.Add("base_position", GetPosition());
        templateDict.Add("satellite_velocity", GetVelocity());
        templateDict.Add("size", GetSize());
        templateDict.Add("mass", GetMass());
        dict.Add("template", templateDict);
        Godot.Collections.Dictionary meshDict = new Godot.Collections.Dictionary();
        meshDict.Add("subdivisions", subdivisions);
        Godot.Collections.Array<Godot.Collections.Array<int>> vpeArray = new Godot.Collections.Array<Godot.Collections.Array<int>>();
        for (int i = 0; i < subdivisions; i++)
        {
            Godot.Collections.Array<int> row = new Godot.Collections.Array<int>();
            row.Add(verticesPerEdge[i, 0]);
            row.Add(verticesPerEdge[i, 1]);
            vpeArray.Add(row);
        }
        meshDict.Add("vertices_per_edge", vpeArray);
        meshDict.Add("num_abberations", numAbberations);
        meshDict.Add("num_deformation_cycles", numDeformationCycles);
        dict.Add("base_mesh", meshDict);
        Godot.Collections.Dictionary scalingDict = new Godot.Collections.Dictionary();
        scalingDict.Add("x_scale_range", xScaleRange);
        scalingDict.Add("y_scale_range", yScaleRange);
        scalingDict.Add("z_scale_range", zScaleRange);
        dict.Add("scaling_settings", scalingDict);
        Godot.Collections.Dictionary noiseDict = new Godot.Collections.Dictionary();
        noiseDict.Add("amplitude_range", amplitudeRange);
        noiseDict.Add("scaling_range", scalingRange);
        noiseDict.Add("octave_range", octaveRange);
        dict.Add("noise_settings", noiseDict);
        return dict;
    }

    private void ToggleDetailsPanel()
    {
        if (DetailsPanel == null) return;

        if (DetailsPanel.GetChildCount() == 0)
        {
            // Create and setup detail panel
            var detailPanel = _detailPanelScene.Instantiate<DetailPanel>();
            DetailsPanel.AddChild(detailPanel);
            detailPanel.SetupForSatelliteItem();
            detailPanel.ValueChanged += OnDetailValueChanged;

            // Set current values
            UpdateDetailPanelValues();
        }

        DetailsPanel.Visible = !DetailsPanel.Visible;
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

                // Scaling values
                detailPanel.SetTectonicsValues("X Scale", xScaleRange);
                detailPanel.SetTectonicsValues("Y Scale", yScaleRange);
                detailPanel.SetTectonicsValues("Z Scale", zScaleRange);

                // Noise values
                detailPanel.SetTectonicsValues("Amplitude", amplitudeRange);
                detailPanel.SetTectonicsValues("Scaling", scalingRange);
                detailPanel.SetTectonicsValues("Octaves", Array.ConvertAll(octaveRange, x => (float)x));
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

                // Scaling values
                xScaleRange = detailPanel.GetTectonicsValues("X Scale");
                yScaleRange = detailPanel.GetTectonicsValues("Y Scale");
                zScaleRange = detailPanel.GetTectonicsValues("Z Scale");

                // Noise values
                amplitudeRange = detailPanel.GetTectonicsValues("Amplitude");
                scalingRange = detailPanel.GetTectonicsValues("Scaling");
                var octavesFloat = detailPanel.GetTectonicsValues("Octaves");
                octaveRange = Array.ConvertAll(octavesFloat, x => (int)x);
            }
        }
    }
}
