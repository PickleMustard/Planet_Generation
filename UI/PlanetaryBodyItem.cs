using System;
using Godot;
using Structures.Enums;
using UtilityLibrary;

namespace UI;

public partial class PlanetaryBodyItem : HBoxContainer
{
    [Signal]
    public delegate void ItemUpdateEventHandler();

    [Signal]
    public delegate void ExpandMenuEventHandler(String sender, bool toggle);

    [Signal]
    public delegate void ShouldAutoCalculateEventHandler(
        bool shouldAutoCalculate,
        PlanetaryBodyItem item
    );

    [Signal]
    public delegate void RecalculateVelocityEventHandler(PlanetaryBodyItem item);

    [Signal]
    public delegate void RecalculatePositionEventHandler(PlanetaryBodyItem item);

    [Signal]
    public delegate void MassSubmittedEventHandler(PlanetaryBodyItem item);

    [Export]
    public Button? Toggle;

    [Export]
    public Button? RemoveItem;

    [Export]
    public Button? DetailsToggle;

    [Export]
    public CheckButton? AutoCalculateToggle;

    [Export]
    public OptionButton? OptionButton;

    [Export]
    public OptionButton? OrbitCenterOptionButton;

    [Export]
    public SpinBox? Apogee;

    [Export]
    public SpinBox? Perigee;

    [Export]
    public SpinBox? StartingAngle;

    [Export]
    public SpinBox? VerticalOffset;

    [Export]
    public SpinBox? Mass;

    [Export]
    public SpinBox? BodySize;

    // Satellites UI
    [Export]
    public Button? AddSatellite;

    [Export]
    public Button? RemoveSatellite;

    [Export]
    public Label? SatellitesCountLabel;

    [Export]
    public VBoxContainer? SatellitesList;

    [Export]
    public VBoxContainer? DetailsPanel;

    [Export]
    public ScrollContainer? SatellitesScroll;

    public Action<PlanetaryBodyItem>? OnRemoveRequested;

    private String? bodyName;

    //Hidden Values
    //Base Mesh
    private int subdivisions;
    private int[,]? verticesPerEdge;
    private int numAbberations;
    private int numDeformationCycles;

    //Tectonics
    private int[]? numContinents;
    private float[]? stressScale;
    private float[]? shearScale;
    private float[]? maxPropagationDistance;
    private float[]? propagationFalloff;
    private float[]? inactiveStressThreshold;
    private float[]? generalHeightScale;
    private float[]? generalShearScale;
    private float[]? generalCompressionScale;
    private float[]? generalTransformScale;

    private PackedScene? _satelliteItemScene;
    private PackedScene? _detailPanelScene;

    private Godot.Collections.Array? individualSatelliteHolder;

    private const float Limit = 10000f; // constrain within ±10,000 units (mass 0..10,000)
    private const float MassLimit = 100000000f; // constrain within ±100,000,000,000 units (mass 0..100,000,000,000)
    private const float SizeLimit = 10000f; // constrain within ±10,000 units (size 0..10,000)
    private const float OrbitalLimit = 100000f; // constrain orbital parameters 0..100,000
    private Vector2 SATELLITE_SCROLL_MIN_SIZE = new Vector2(0, 200);

    // Orbital parameters for planetary bodies
    private float _apogee = 1000f;
    private float _perigee = 500f;
    private float _startingAngle = 0f;
    private float _verticalOffset = 0f;
    private int _orbitalCenterIndex = -1; // -1 = barycenter, 0+ = dominant body index
    private Callable _orbitalCenterSelectedCallable;

    public override void _EnterTree()
    {
        // Hook remove
        var remove = GetNodeOrNull<Button>("MainContent/Header/RemoveItem");
        individualSatelliteHolder = new Godot.Collections.Array();
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
        OptionButton ??= GetNodeOrNull<OptionButton>(
            "MainContent/Content/BodyTypeContent/OptionButton"
        );
        OrbitCenterOptionButton ??= GetNodeOrNull<OptionButton>(
            "MainContent/Content/OrbitalCenterContent/OptionButton"
        );
        Mass ??= GetNodeOrNull<SpinBox>("MainContent/Content/MassContent/mass");
        BodySize ??= GetNodeOrNull<SpinBox>("MainContent/Content/SizeContent/size");
        DetailsPanel ??= GetNodeOrNull<VBoxContainer>("DetailsPanel");
        SatellitesScroll ??= GetNodeOrNull<ScrollContainer>(
            "MainContent/Content/SatellitesContent/SatellitesScroll"
        );
        AutoCalculateToggle ??= GetNodeOrNull<CheckButton>(
            "MainContent/Header/AutoCalculateToggle"
        );

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

        if (OrbitCenterOptionButton != null)
        {
            OrbitCenterOptionButton.AddItem("barycenter");
            OrbitCenterOptionButton.Select(0);
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

        if (Mass != null)
        {
            Mass.GetLineEdit().TextSubmitted += idx => EmitSignal(SignalName.MassSubmitted, this);
        }
        if (AutoCalculateToggle != null)
        {
            AutoCalculateToggle.Pressed += UpdateAutoCalculate;
        }
        UpdateSatellitesCountLabel();
    }

    public void UpdateAutoCalculate()
    {
        GD.Print(
            $"Emitting AutoCalculate, AutoCalculateToggle.ButtonPressed: {AutoCalculateToggle!.ButtonPressed}"
        );
        EmitSignal(SignalName.ShouldAutoCalculate, AutoCalculateToggle!.ButtonPressed, this);
    }

    private void ApplyConstraints()
    {
        // Mass: [0, Limit]
        if (Mass != null)
        {
            Mass.MinValue = 0.0;
            Mass.MaxValue = MassLimit;
            Mass.AllowGreater = true;
            Mass.AllowLesser = false;
        }
        if (BodySize != null)
        {
            BodySize.MinValue = 0.0;
            BodySize.MaxValue = SizeLimit;
            BodySize.AllowGreater = true;
            BodySize.AllowLesser = false;
        }
    }

    public void PropogateChangeDown(CelestialBodyType type)
    {
        foreach (var si in SatellitesList!.GetChildren())
        {
            SatellitesList!.RemoveChild(si);
        }
        if (individualSatelliteHolder!.Count > 0)
        {
            SatellitesScroll!.CustomMinimumSize = SATELLITE_SCROLL_MIN_SIZE;
            foreach (SatelliteItem item in individualSatelliteHolder)
            {
                SatellitesList.AddChild((SatelliteItem)item);
            }
        }
        UpdateSatellitesCountLabel();
    }

    public void ApplyTemplate(CelestialBodyType type)
    {
        // Read defaults from TOML in Configuration/SystemGen with safe fallbacks
        var t = TemplateHelpers.GetCelestialBodyDefaults(type);
        var template = (Godot.Collections.Dictionary)t["template"];
        bodyName = PickName((Godot.Collections.Dictionary)t["possible_names"]);

        // Planetary bodies use orbital parameters from template
        if (template.ContainsKey("apogee"))
            SetApogee((float)template["apogee"]);
        if (template.ContainsKey("perigee"))
            SetPerigee((float)template["perigee"]);
        if (template.ContainsKey("starting_angle"))
            SetStartingAngle((float)template["starting_angle"]);
        if (template.ContainsKey("vertical_offset"))
            SetVerticalOffset((float)template["vertical_offset"]);
        if (Mass != null)
            Mass.Value = Mathf.Clamp((float)template["mass"], 0f, MassLimit);
        if (BodySize != null)
            BodySize.Value = Mathf.Clamp((float)template["size"], 0f, SizeLimit);

        var baseMesh = (Godot.Collections.Dictionary)t["base_mesh"];
        subdivisions = (int)baseMesh["subdivisions"];
        verticesPerEdge = new int[subdivisions, 2];
        Godot.Collections.Array<Godot.Collections.Array<int>> vpeArray =
            (Godot.Collections.Array<Godot.Collections.Array<int>>)baseMesh["vertices_per_edge"];
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

    public void SetConfiguration(Godot.Collections.Dictionary t)
    {
        if (!t.ContainsKey("template"))
        {
            GD.PrintErr("PlanetaryBodyItem.SetTemplate(): missing 'template' key in dictionary");
            return;
        }
        var template = (Godot.Collections.Dictionary)t["template"];

        // Handle orbital parameters for planetary bodies (stored in bodyDict, not template)
        if (t.ContainsKey("orbital_parameters"))
        {
            var orbitalParams = (Godot.Collections.Dictionary)t["orbital_parameters"];
            if (orbitalParams.ContainsKey("apogee"))
                SetApogee((float)orbitalParams["apogee"]);
            if (orbitalParams.ContainsKey("perigee"))
                SetPerigee((float)orbitalParams["perigee"]);
            if (orbitalParams.ContainsKey("starting_angle"))
                SetStartingAngle((float)orbitalParams["starting_angle"]);
            if (orbitalParams.ContainsKey("vertical_offset"))
                SetVerticalOffset((float)orbitalParams["vertical_offset"]);
            if (orbitalParams.ContainsKey("orbital_center_index"))
                SetOrbitalCenterIndex((int)orbitalParams["orbital_center_index"]);
        }

        if (Mass != null && template.ContainsKey("mass"))
            Mass.Value = Mathf.Clamp((float)template["mass"], 0f, MassLimit);
        if (BodySize != null && template.ContainsKey("size"))
            BodySize.Value = Mathf.Clamp((float)template["size"], 0f, SizeLimit);

        if (t.ContainsKey("base_mesh"))
        {
            var baseMesh = (Godot.Collections.Dictionary)t["base_mesh"];
            subdivisions = (int)baseMesh["subdivisions"];
            verticesPerEdge = new int[subdivisions, 2];
            Godot.Collections.Array<Godot.Collections.Array<int>> vpeArray =
                (Godot.Collections.Array<Godot.Collections.Array<int>>)
                    baseMesh["vertices_per_edge"];
            for (int i = 0; i < subdivisions; i++)
            {
                verticesPerEdge[i, 0] = (int)vpeArray[i][0];
                verticesPerEdge[i, 1] = (int)vpeArray[i][1];
            }
            numAbberations = (int)baseMesh["num_abberations"];
            numDeformationCycles = (int)baseMesh["num_deformation_cycles"];
        }

        if (t.ContainsKey("tectonics"))
        {
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

        // Only process individual satellites for non-dominant bodies.
        // Belt-type satellites are handled by the top-level Satellite Belts section
        // in PlanetSystemGenerator.LoadTemplate().
        if (t.ContainsKey("satellites"))
        {
            SatellitesScroll!.CustomMinimumSize = SATELLITE_SCROLL_MIN_SIZE;
            var satellites = (Godot.Collections.Array)t["satellites"];

            foreach (Godot.Collections.Dictionary sat in satellites)
            {
                var typeStr = (string)sat["type"];

                // Skip belt-type entries — these are handled by the separate
                // Satellite Belts section in PlanetSystemGenerator.LoadTemplate()
                if (Enum.TryParse<SatelliteGroupTypes>(typeStr, out _))
                    continue;

                // Individual satellite (Moon, Asteroid, Comet, etc.)
                var satTemplate = (Godot.Collections.Dictionary)sat["template"];
                var satMass = (float)satTemplate["mass"];
                // Size stored as float by ReadFloat, cast safely
                var satSize = Mathf.RoundToInt((float)satTemplate["size"]);

                var satItem = _satelliteItemScene!.Instantiate<SatelliteItem>();
                SatellitesList!.AddChild(satItem);
                satItem.SubscribeEvents();
                if (Enum.TryParse<SatelliteBodyType>(typeStr, out var satBodyType))
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
                // Individual satellites use orbital parameters, not position/velocity
                if (satTemplate.ContainsKey("apogee"))
                    satItem.SetApogee((float)satTemplate["apogee"]);
                if (satTemplate.ContainsKey("perigee"))
                    satItem.SetPerigee((float)satTemplate["perigee"]);
                if (satTemplate.ContainsKey("starting_angle"))
                    satItem.SetStartingAngle((float)satTemplate["starting_angle"]);
                if (satTemplate.ContainsKey("vertical_offset"))
                    satItem.SetVerticalOffset((float)satTemplate["vertical_offset"]);
                satItem.SetSize(satSize);
                if (satItem.mass != null)
                    satItem.mass.Value = Mathf.Clamp(satMass, 0f, 100000000f);
                satItem.ItemUpdate += OnSatelliteItemUpdate;
                individualSatelliteHolder!.Add(satItem);
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

    public void UpdateHeaderFromBodyType(string typeName)
    {
        var headerBtn = GetNodeOrNull<Button>("MainContent/Header/Toggle");
        if (headerBtn != null)
        {
            headerBtn.Text = $"{bodyName} ({typeName})";
        }
    }

    public float GetBodySize()
    {
        return (float)BodySize!.Value;
    }

    public float GetBodyMass()
    {
        return (float)Mass!.Value;
    }

    public void SetSize(float size)
    {
        if (this.BodySize != null)
            this.BodySize.Value = Mathf.Clamp(size, 0f, SizeLimit);
    }

    public Godot.Collections.Dictionary ToParams()
    {
        Godot.Collections.Dictionary dict = new Godot.Collections.Dictionary();
        var ob = GetNode<OptionButton>("MainContent/Content/BodyTypeContent/OptionButton");

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

        dict["type"] = Enum.GetName(typeof(CelestialBodyType), (CelestialBodyType)ob.Selected)!;
        dict.Add("name", bodyName!);
        var templateDict = new Godot.Collections.Dictionary();

        // Planetary bodies use orbital parameters
        templateDict.Add("mass", cm);
        templateDict.Add("size", cs);

        // Add orbital parameters
        var orbitalParams = new Godot.Collections.Dictionary();
        orbitalParams["apogee"] = _apogee;
        orbitalParams["perigee"] = _perigee;
        orbitalParams["starting_angle"] = _startingAngle;
        orbitalParams["vertical_offset"] = _verticalOffset;
        orbitalParams["orbital_center_index"] = _orbitalCenterIndex;
        orbitalParams["parent_body"] = OrbitCenterOptionButton!.GetItemText(
            OrbitCenterOptionButton!.Selected
        );
        dict.Add("orbital_parameters", orbitalParams);

        dict.Add("template", templateDict);
        var baseMesh = new Godot.Collections.Dictionary();
        baseMesh.Add("subdivisions", subdivisions);
        Godot.Collections.Array<Godot.Collections.Array<int>> vpeArray =
            new Godot.Collections.Array<Godot.Collections.Array<int>>();
        for (int i = 0; i < subdivisions; i++)
        {
            Godot.Collections.Array<int> row = new Godot.Collections.Array<int>();
            row.Add(verticesPerEdge![i, 0]);
            row.Add(verticesPerEdge[i, 1]);
            vpeArray.Add(row);
        }
        baseMesh.Add("vertices_per_edge", vpeArray);
        baseMesh.Add("num_abberations", numAbberations);
        baseMesh.Add("num_deformation_cycles", numDeformationCycles);
        dict.Add("base_mesh", baseMesh);
        var tectonics = new Godot.Collections.Dictionary();
        tectonics.Add("num_continents", numContinents!);
        tectonics.Add("stress_scale", stressScale!);
        tectonics.Add("shear_scale", shearScale!);
        tectonics.Add("max_propagation_distance", maxPropagationDistance!);
        tectonics.Add("propagation_falloff", propagationFalloff!);
        tectonics.Add("inactive_stress_threshold", inactiveStressThreshold!);
        tectonics.Add("general_height_scale", generalHeightScale!);
        tectonics.Add("general_shear_scale", generalShearScale!);
        tectonics.Add("general_compression_scale", generalCompressionScale!);
        tectonics.Add("general_transform_scale", generalTransformScale!);
        dict.Add("tectonics", tectonics);

        // Add satellites (only for planetary bodies — dominant bodies have no embedded satellites)
        var satellitesList = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (Node child in SatellitesList!.GetChildren())
        {
            if (child is SatelliteItem si)
            {
                satellitesList.Add(si.ToParams());
            }
        }
        if (satellitesList.Count > 0)
        {
            dict.Add("satellites", satellitesList);
        }

        return dict;
    }

    private void AddSatelliteItem()
    {
        SatellitesScroll!.CustomMinimumSize = SATELLITE_SCROLL_MIN_SIZE;
        var ob = GetNode<OptionButton>("MainContent/Content/BodyTypeContent/OptionButton");
        if (SatellitesList == null || _satelliteItemScene == null)
            return;
        var satelliteItem = _satelliteItemScene.Instantiate<SatelliteItem>();
        satelliteItem.SetParentType((CelestialBodyType)ob.Selected);
        satelliteItem.OnRemoveRequested += RemoveSatelliteItem;
        satelliteItem.ItemUpdate += OnSatelliteItemUpdate;
        SatellitesList!.AddChild(satelliteItem);
        satelliteItem.SubscribeEvents();
        individualSatelliteHolder!.Add(satelliteItem);
        RedistributeSatelliteRings();
        UpdateSatellitesCountLabel();
    }

    private void RedistributeSatelliteRings()
    {
        var bodiesByRing = new System.Collections.Generic.Dictionary<
            float,
            System.Collections.Generic.List<SatelliteItem>
        >();
        foreach (Node child in SatellitesList!.GetChildren())
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
        if (SatellitesList!.GetChildCount() <= 0)
            return; // No negatives
        var index = SatellitesList.GetChildCount() - 1;
        var last = SatellitesList.GetChild(index);
        individualSatelliteHolder!.RemoveAt(index);
        SatellitesList.RemoveChild(last);
        last.QueueFree();
        UpdateSatellitesCountLabel();
    }

    private void RemoveSatelliteItem(Node item)
    {
        if (IsInstanceValid(item) && item.GetParent() == SatellitesList)
        {
            if (individualSatelliteHolder!.Contains(item))
            {
                individualSatelliteHolder.Remove(item);
            }
            SatellitesList!.RemoveChild(item);
            RedistributeSatelliteRings();
            item.QueueFree();
        }
        UpdateSatellitesCountLabel();
    }

    private void UpdateSatellitesCountLabel()
    {
        if (SatellitesCountLabel != null)
        {
            int count = SatellitesList!.GetChildCount();
            SatellitesCountLabel.Text = count <= 1 ? $"{count} satellite" : $"{count} satellites";
            if (count == 0)
                SatellitesScroll!.CustomMinimumSize = Vector2.Zero;
        }
    }

    private void OnSatelliteItemUpdate()
    {
        EmitSignal(SignalName.ItemUpdate);
    }

    private void ToggleDetailsPanel()
    {
        if (DetailsPanel == null)
            return;

        if (DetailsPanel.GetChildCount() == 0)
        {
            // Create and setup detail panel
            var detailPanel = _detailPanelScene!.Instantiate<DetailPanel>();
            DetailsPanel.AddChild(detailPanel);
            detailPanel.SetupForPlanetaryBodyItem();
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
                detailPanel.SetVerticesPerEdge(verticesPerEdge!);
                detailPanel.SetAberrations(numAbberations);
                detailPanel.SetDeformationCycles(numDeformationCycles);

                // Tectonics values
                detailPanel.SetTectonicsValues(
                    "Continents",
                    Array.ConvertAll(numContinents!, x => (float)x)
                );
                detailPanel.SetTectonicsValues("Stress Scale", stressScale!);
                detailPanel.SetTectonicsValues("Shear Scale", shearScale!);
                detailPanel.SetTectonicsValues("Max Propagation Distance", maxPropagationDistance!);
                detailPanel.SetTectonicsValues("Propagation Falloff", propagationFalloff!);
                detailPanel.SetTectonicsValues(
                    "Inactive Stress Threshold",
                    inactiveStressThreshold!
                );
                detailPanel.SetTectonicsValues("General Height Scale", generalHeightScale!);
                detailPanel.SetTectonicsValues("General Shear Scale", generalShearScale!);
                detailPanel.SetTectonicsValues(
                    "General Compression Scale",
                    generalCompressionScale!
                );
                detailPanel.SetTectonicsValues("General Transform Scale", generalTransformScale!);
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
                inactiveStressThreshold = detailPanel.GetTectonicsValues(
                    "Inactive Stress Threshold"
                );
                generalHeightScale = detailPanel.GetTectonicsValues("General Height Scale");
                generalShearScale = detailPanel.GetTectonicsValues("General Shear Scale");
                generalCompressionScale = detailPanel.GetTectonicsValues(
                    "General Compression Scale"
                );
                generalTransformScale = detailPanel.GetTectonicsValues("General Transform Scale");
            }
        }
    }

    private void RemoveSatelliteItem(SatelliteItem item)
    {
        if (
            IsInstanceValid(item)
            && item.GetParent() == SatellitesList
            && SatellitesList!.GetChildCount() > 1
        )
        {
            if (individualSatelliteHolder!.Contains(item))
            {
                individualSatelliteHolder.Remove(item);
            }
            item.QueueFree();
            RedistributeSatelliteRings();
            UpdateSatellitesCountLabel();
        }
    }

    private void ShowPositionVelocityControls(bool show)
    {
        var posContent = GetNodeOrNull<GridContainer>("MainContent/Content/PositionContent");
        var velContent = GetNodeOrNull<GridContainer>("MainContent/Content/VelocityContent");

        if (posContent != null)
            posContent.Visible = show;
        if (velContent != null)
            velContent.Visible = show;
    }

    public void UpdateOrbitalCenterOptions(Godot.Collections.Array<string> options)
    {
        if (OrbitCenterOptionButton != null)
        {
            OrbitCenterOptionButton.Clear();
            foreach (var option in options)
            {
                OrbitCenterOptionButton.AddItem(option);
            }

            // Clamp index to valid range: _orbitalCenterIndex is 0-based dominant body index
            // (-1 = Barycenter), and options includes "Barycenter" at index 0, so the max
            // valid _orbitalCenterIndex is options.Count - 2 (the last dominant body).
            int maxDominantIndex = options.Count - 2;
            if (_orbitalCenterIndex > maxDominantIndex || _orbitalCenterIndex < -1)
                _orbitalCenterIndex = -1;

            OrbitCenterOptionButton.Select(_orbitalCenterIndex + 1); // +1 because "Barycenter" is at index 0
        }
    }

    public float GetApogee() => _apogee;

    public void SetApogee(float value)
    {
        _apogee = Mathf.Clamp(value, 0f, OrbitalLimit);
        // Guard: ensure apogee >= perigee
        if (_apogee < _perigee)
        {
            _perigee = _apogee;
            if (Perigee != null)
                Perigee.Value = _perigee;
        }
        if (Apogee != null)
            Apogee.Value = _apogee;
    }

    public float GetPerigee() => _perigee;

    public void SetPerigee(float value)
    {
        _perigee = Mathf.Clamp(value, 0f, OrbitalLimit);
        // Guard: ensure perigee <= apogee
        if (_perigee > _apogee)
        {
            _apogee = _perigee;
            if (Apogee != null)
                Apogee.Value = _apogee;
        }
        if (Perigee != null)
            Perigee.Value = _perigee;
    }

    public float GetStartingAngle() => _startingAngle;

    public void SetStartingAngle(float value)
    {
        _startingAngle = Mathf.Clamp(value, 0f, 360f);
        if (StartingAngle != null)
            StartingAngle.Value = _startingAngle;
    }

    public float GetVerticalOffset() => _verticalOffset;

    public void SetVerticalOffset(float value)
    {
        _verticalOffset = Mathf.Clamp(value, -90f, 90f);
        if (VerticalOffset != null)
            VerticalOffset.Value = _verticalOffset;
    }

    public int GetOrbitalCenterIndex() => _orbitalCenterIndex;

    public void SetOrbitalCenterIndex(int index)
    {
        _orbitalCenterIndex = index;
        if (OrbitCenterOptionButton != null && index + 1 < OrbitCenterOptionButton.ItemCount)
        {
            OrbitCenterOptionButton.Select(index + 1);
        }
    }

    public string GetBodyName() => bodyName ?? "Unnamed";
}
