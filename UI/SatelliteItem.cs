using System;
using Godot;
using Structures.Enums;
using UtilityLibrary;

namespace UI;

public partial class SatelliteItem : HBoxContainer
{
    [Signal]
    public delegate void ItemUpdateEventHandler();

    [Export]
    public Button? Toggle;

    [Export]
    public Button? RemoveItem;

    [Export]
    public Button? DetailsToggle;

    [Export]
    public OptionButton? OptionButton;

    [Export]
    public SpinBox? Apogee;

    [Export]
    public SpinBox? Perigee;

    [Export]
    public SpinBox? StartingAngle;

    [Export]
    public SpinBox? VerticalOffset;

    [Export]
    public SpinBox? mass;

    [Export]
    public SpinBox? size;

    [Export]
    public VBoxContainer? DetailsPanel;

    public Action<SatelliteItem>? OnRemoveRequested;

    private PlanetaryBodyType parentType;
    private int NumberInBelt = 25;
    private const float Limit = 10000f; // constrain within ±10,000 units
    private const float MassLimit = 10000f; // constrain mass 0..10,000
    private const float SizeLimit = 10000f; // constrain size 0..10,000

    private PackedScene? _detailPanelScene;

    //Hidden Values
    //Base Mesh
    private int subdivisions;
    private int[,]? verticesPerEdge;
    private int numAbberations;
    private int numDeformationCycles;

    //Tectonics
    bool hasTectonics = false;
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

    //Scaling (backward compatibility)
    bool hasScaling = false;
    private float[]? xScaleRange;
    private float[]? yScaleRange;
    private float[]? zScaleRange;

    //Spherical Harmonics
    bool hasSphericalHarmonics = false;
    private float[]? shAmplitudeRange;

    //Noise Settings
    bool hasNoise = false;
    private float[]? amplitudeRange;
    private float[]? scalingRange;
    private int[]? octaveRange;

    private String? satName;

    public void SetParentType(PlanetaryBodyType type)
    {
        this.parentType = type;
    }

    public override void _EnterTree()
    {
        // Cache field nodes if not set via exported references
        OptionButton ??= GetNodeOrNull<OptionButton>(
            "MainContent/Content/TypeContent/OptionButton"
        );
        Apogee ??= GetNodeOrNull<SpinBox>("MainContent/Content/OrbitalContent/Apogee");
        Perigee ??= GetNodeOrNull<SpinBox>("MainContent/Content/OrbitalContent/Perigee");
        StartingAngle ??= GetNodeOrNull<SpinBox>(
            "MainContent/Content/OrbitalContent/StartingAngle"
        );
        VerticalOffset ??= GetNodeOrNull<SpinBox>(
            "MainContent/Content/OrbitalContent/VerticalOffset"
        );
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
        var spinBoxes = new[] { Apogee, Perigee, StartingAngle, VerticalOffset, mass, size };
        foreach (var sb in spinBoxes)
        {
            if (sb != null)
            {
                sb.ValueChanged += value => EmitSignal(SignalName.ItemUpdate);
            }
        }

        // Populate satellite types from YAML configuration and hook selection
        if (OptionButton != null)
        {
            PlanetaryTypeLoader.PopulateOptionButton(
                OptionButton,
                PlanetaryTypeLoader.GetSatelliteBodyTypes()
            );

            OptionButton.ItemSelected += idx =>
            {
                var internalName = PlanetaryTypeLoader.GetSelectedInternalName(OptionButton);
                if (internalName == null)
                    return;
                var type = PlanetaryTypeLoader.ToSatelliteBodyType(internalName);
                ApplyTemplate(type);
                UpdateHeaderFromType(OptionButton.GetItemText((int)idx));
                EmitSignal(SignalName.ItemUpdate);
            };

            // Ensure a valid initial selection
            if (OptionButton.ItemCount > 0)
            {
                if (OptionButton.Selected < 0)
                    OptionButton.Select(0);
                var initialName = PlanetaryTypeLoader.GetSelectedInternalName(OptionButton);
                if (initialName != null)
                {
                    ApplyTemplate(PlanetaryTypeLoader.ToSatelliteBodyType(initialName));
                    UpdateHeaderFromType(OptionButton.GetItemText(OptionButton.Selected));
                }
            }
        }
    }

    public void ApplyTemplate(SatelliteBodyType type)
    {
        // Read defaults from TOML in Configuration/SystemGen with safe fallbacks
        var t = TemplateHelpers.GetSatelliteBodyDefaults(type);
        var template = (Godot.Collections.Dictionary)t["template"];
        satName = PickName((Godot.Collections.Dictionary)t["possible_names"]);

        // Assign orbital parameters to UI (already clamped by GetDefaults, but clamp again defensively)
        // Default values: apogee=500, perigee=300, starting_angle=0, vertical_offset=0
        float defaultApogee = template.ContainsKey("apogee") ? (float)template["apogee"] : 500f;
        float defaultPerigee = template.ContainsKey("perigee") ? (float)template["perigee"] : 300f;
        float defaultStartingAngle = template.ContainsKey("starting_angle")
            ? (float)template["starting_angle"]
            : 0f;
        float defaultVerticalOffset = template.ContainsKey("vertical_offset")
            ? (float)template["vertical_offset"]
            : 0f;

        if (Apogee != null)
            Apogee.Value = Mathf.Clamp(defaultApogee, 0f, Limit);
        if (Perigee != null)
            Perigee.Value = Mathf.Clamp(defaultPerigee, 0f, Limit);
        if (StartingAngle != null)
            StartingAngle.Value = Mathf.Clamp(defaultStartingAngle, 0f, 360f);
        if (VerticalOffset != null)
            VerticalOffset.Value = Mathf.Clamp(defaultVerticalOffset, -90f, 90f);

        if (mass != null)
            mass.Value = Mathf.Clamp((float)template["mass"], 0f, MassLimit);
        if (size != null)
            size.Value = Mathf.Clamp((float)template["size"], 0f, SizeLimit);

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

        if (t.ContainsKey("tectonic"))
        {
            hasTectonics = true;
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
        // Check for spherical_harmonics_settings first, fall back to scaling_settings
        if (t.ContainsKey("spherical_harmonics_settings"))
        {
            hasSphericalHarmonics = true;
            var shSettings = (Godot.Collections.Dictionary)t["spherical_harmonics_settings"];
            shAmplitudeRange = (float[])shSettings["amplitude_range"];
        }
        else if (t.ContainsKey("scaling_settings"))
        {
            hasScaling = true;
            var scaling = (Godot.Collections.Dictionary)t["scaling_settings"];
            xScaleRange = (float[])scaling["scaling_range_x"];
            yScaleRange = (float[])scaling["scaling_range_y"];
            zScaleRange = (float[])scaling["scaling_range_z"];
        }

        if (t.ContainsKey("noise_settings"))
        {
            hasNoise = true;
            var noiseSettings = (Godot.Collections.Dictionary)t["noise_settings"];
            amplitudeRange = (float[])noiseSettings["amplitude_range"];
            scalingRange = (float[])noiseSettings["scaling_range"];
            octaveRange = (int[])noiseSettings["octave_range"];
        }
    }

    public void SetTemplate(Godot.Collections.Dictionary t)
    {
        var template = (Godot.Collections.Dictionary)t["template"];

        // Assign orbital parameters to UI
        float apogee = template.ContainsKey("apogee") ? (float)template["apogee"] : 500f;
        float perigee = template.ContainsKey("perigee") ? (float)template["perigee"] : 300f;
        float startingAngle = template.ContainsKey("starting_angle")
            ? (float)template["starting_angle"]
            : 0f;
        float verticalOffset = template.ContainsKey("vertical_offset")
            ? (float)template["vertical_offset"]
            : 0f;

        if (Apogee != null)
            Apogee.Value = Mathf.Clamp(apogee, 0f, Limit);
        if (Perigee != null)
            Perigee.Value = Mathf.Clamp(perigee, 0f, Limit);
        if (StartingAngle != null)
            StartingAngle.Value = Mathf.Clamp(startingAngle, 0f, 360f);
        if (VerticalOffset != null)
            VerticalOffset.Value = Mathf.Clamp(verticalOffset, -90f, 90f);

        if (mass != null)
            mass.Value = Mathf.Clamp((float)template["mass"], 0f, MassLimit);
        if (size != null)
            size.Value = Mathf.Clamp((float)template["size"], 0f, SizeLimit);

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

        if (t.ContainsKey("tectonic"))
        {
            hasTectonics = true;
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

        // Check for spherical_harmonics_settings first, fall back to scaling_settings
        if (t.ContainsKey("spherical_harmonics_settings"))
        {
            hasSphericalHarmonics = true;
            var shSettings = (Godot.Collections.Dictionary)t["spherical_harmonics_settings"];
            shAmplitudeRange = (float[])shSettings["amplitude_range"];
        }
        else if (t.ContainsKey("scaling_settings"))
        {
            hasScaling = true;
            var scaling = (Godot.Collections.Dictionary)t["scaling_settings"];
            xScaleRange = (float[])scaling["scaling_range_x"];
            yScaleRange = (float[])scaling["scaling_range_y"];
            zScaleRange = (float[])scaling["scaling_range_z"];
        }

        if (t.ContainsKey("noise_settings"))
        {
            hasNoise = true;
            var noiseSettings = (Godot.Collections.Dictionary)t["noise_settings"];
            amplitudeRange = (float[])noiseSettings["amplitude_range"];
            scalingRange = (float[])noiseSettings["scaling_range"];
            octaveRange = (int[])noiseSettings["octave_range"];
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

    private void ApplyConstraints()
    {
        // Orbital parameters: apogee/perigee [0, Limit], starting_angle [0, 360], vertical_offset [-90, 90]
        if (Apogee != null)
        {
            Apogee.MinValue = 0f;
            Apogee.MaxValue = Limit;
            Apogee.AllowGreater = false;
            Apogee.AllowLesser = false;
        }
        if (Perigee != null)
        {
            Perigee.MinValue = 0f;
            Perigee.MaxValue = Limit;
            Perigee.AllowGreater = false;
            Perigee.AllowLesser = false;
        }
        if (StartingAngle != null)
        {
            StartingAngle.MinValue = 0f;
            StartingAngle.MaxValue = 360f;
            StartingAngle.AllowGreater = false;
            StartingAngle.AllowLesser = false;
        }
        if (VerticalOffset != null)
        {
            VerticalOffset.MinValue = -90f;
            VerticalOffset.MaxValue = 90f;
            VerticalOffset.AllowGreater = false;
            VerticalOffset.AllowLesser = false;
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

    public void SetApogee(float value)
    {
        if (Apogee != null)
            Apogee.Value = Mathf.Clamp(value, 0f, Limit);
    }

    public float GetApogee()
    {
        return Mathf.Clamp((float)Apogee!.Value, 0f, Limit);
    }

    public void SetPerigee(float value)
    {
        if (Perigee != null)
            Perigee.Value = Mathf.Clamp(value, 0f, Limit);
    }

    public float GetPerigee()
    {
        return Mathf.Clamp((float)Perigee!.Value, 0f, Limit);
    }

    public void SetStartingAngle(float value)
    {
        if (StartingAngle != null)
            StartingAngle.Value = Mathf.Clamp(value, 0f, 360f);
    }

    public float GetStartingAngle()
    {
        return Mathf.Clamp((float)StartingAngle!.Value, 0f, 360f);
    }

    public void SetVerticalOffset(float value)
    {
        if (VerticalOffset != null)
            VerticalOffset.Value = Mathf.Clamp(value, -90f, 90f);
    }

    public float GetVerticalOffset()
    {
        return Mathf.Clamp((float)VerticalOffset!.Value, -90f, 90f);
    }

    /// <summary>
    /// Backwards compatibility method for loading old saves with position/velocity.
    /// Converts position to approximate apogee/perigee and calculates starting angle.
    /// </summary>
    public void SetPosition(Vector3 position)
    {
        // For backwards compatibility: approximate orbital parameters from position
        // This is a rough conversion - the user should update to new format
        float distance = position.Length();
        if (distance > 0)
        {
            if (Apogee != null)
                Apogee.Value = Mathf.Clamp(distance * 1.2f, 0f, Limit); // Approximate apogee as 1.2x distance
            if (Perigee != null)
                Perigee.Value = Mathf.Clamp(distance * 0.8f, 0f, Limit); // Approximate perigee as 0.8x distance
        }
    }

    /// <summary>
    /// Backwards compatibility method - velocity is now calculated automatically.
    /// </summary>
    public void SetVelocity(Vector3 velocity)
    {
        // Velocity is now calculated automatically from orbital parameters
        // This method exists for backwards compatibility with old saves
    }

    /// <summary>
    /// Backwards compatibility method for loading old saves.
    /// </summary>
    public new Vector3 GetPosition()
    {
        // Return the calculated position based on orbital parameters
        // This is approximate - for display purposes only
        float apogee = GetApogee();
        float perigee = GetPerigee();
        float angle = GetStartingAngle();
        float inclination = GetVerticalOffset();

        float angleRad = Mathf.DegToRad(angle);
        float inclinationRad = Mathf.DegToRad(inclination);

        float eccentricity = (apogee - perigee) / (apogee + perigee);
        float semiMajorAxis = (apogee + perigee) / 2f;
        float radius =
            semiMajorAxis
            * (1 - eccentricity * eccentricity)
            / (1 + eccentricity * Mathf.Cos(angleRad));

        Vector3 pHat = new Vector3(Mathf.Cos(angleRad), 0, Mathf.Sin(angleRad)).Normalized();
        Vector3 qHat = new Vector3(
            -Mathf.Sin(angleRad) * Mathf.Cos(inclinationRad),
            Mathf.Sin(inclinationRad),
            Mathf.Cos(angleRad) * Mathf.Cos(inclinationRad)
        ).Normalized();

        return radius * Mathf.Cos(angleRad) * pHat + radius * Mathf.Sin(angleRad) * qHat;
    }

    /// <summary>
    /// Backwards compatibility method - velocity is calculated automatically.
    /// </summary>
    public Vector3 GetVelocity()
    {
        // Velocity is calculated automatically - return zero as placeholder
        return Vector3.Zero;
    }

    /// <summary>
    /// Backwards compatibility method for loading old saves.
    /// </summary>
    public Vector3 GetBodyPosition()
    {
        return GetPosition();
    }

    public void SetMass(float massValue)
    {
        if (mass != null)
            mass.Value = Mathf.Clamp(massValue, 0f, MassLimit);
    }

    public float GetMass()
    {
        return Mathf.Clamp((float)mass!.Value, 0f, MassLimit);
    }

    public void SetSize(float sizeValue)
    {
        if (size != null)
            size.Value = Mathf.Clamp(sizeValue, 0f, SizeLimit);
    }

    public new float GetSize()
    {
        return Mathf.Clamp((float)size!.Value, 0f, SizeLimit);
    }

    public string GetSatelliteType()
    {
        if (OptionButton != null && OptionButton.Selected >= 0)
        {
            return PlanetaryTypeLoader.GetSelectedInternalName(OptionButton) ?? "Asteroid";
        }
        return "Asteroid";
    }

    /// <summary>
    /// Gets the orbital parameters as a dictionary with calculated position and velocity.
    /// The position and velocity are calculated based on apogee, perigee, starting angle, and vertical offset.
    /// </summary>
    public Godot.Collections.Dictionary GetOrbitalParameters()
    {
        return new Godot.Collections.Dictionary()
        {
            { "apogee", GetApogee() },
            { "perigee", GetPerigee() },
            { "starting_angle", GetStartingAngle() },
            { "vertical_offset", GetVerticalOffset() },
        };
    }

    public Godot.Collections.Dictionary ToParams()
    {
        Godot.Collections.Dictionary dict = new Godot.Collections.Dictionary();
        dict.Add("type", GetSatelliteType());
        dict.Add("name", satName!);
        Godot.Collections.Dictionary templateDict = new Godot.Collections.Dictionary();
        templateDict.Add("apogee", GetApogee());
        templateDict.Add("perigee", GetPerigee());
        templateDict.Add("starting_angle", GetStartingAngle());
        templateDict.Add("vertical_offset", GetVerticalOffset());
        templateDict.Add("size", GetSize());
        templateDict.Add("mass", GetMass());
        dict.Add("template", templateDict);
        Godot.Collections.Dictionary meshDict = new Godot.Collections.Dictionary();
        meshDict.Add("subdivisions", subdivisions);
        Godot.Collections.Array<Godot.Collections.Array<int>> vpeArray =
            new Godot.Collections.Array<Godot.Collections.Array<int>>();
        for (int i = 0; i < subdivisions; i++)
        {
            Godot.Collections.Array<int> row = new Godot.Collections.Array<int>();
            row.Add(verticesPerEdge![i, 0]);
            row.Add(verticesPerEdge[i, 1]);
            vpeArray.Add(row);
        }
        meshDict.Add("vertices_per_edge", vpeArray);
        meshDict.Add("num_abberations", numAbberations);
        meshDict.Add("num_deformation_cycles", numDeformationCycles);
        dict.Add("base_mesh", meshDict);
        if (hasTectonics)
        {
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
        }
        if (hasSphericalHarmonics)
        {
            Godot.Collections.Dictionary shDict = new Godot.Collections.Dictionary();
            shDict.Add("amplitude_range", shAmplitudeRange!);
            dict.Add("spherical_harmonics_settings", shDict);
        }
        else if (hasScaling)
        {
            // Backward compatibility: output scaling_settings if SH not set
            Godot.Collections.Dictionary scalingDict = new Godot.Collections.Dictionary();
            scalingDict.Add("scaling_range_x", xScaleRange!);
            scalingDict.Add("scaling_range_y", yScaleRange!);
            scalingDict.Add("scaling_range_z", zScaleRange!);
            dict.Add("scaling_settings", scalingDict);
        }
        if (hasNoise)
        {
            Godot.Collections.Dictionary noiseDict = new Godot.Collections.Dictionary();
            noiseDict.Add("amplitude_range", amplitudeRange!);
            noiseDict.Add("scaling_range", scalingRange!);
            noiseDict.Add("octave_range", octaveRange!);
            dict.Add("noise_settings", noiseDict);
        }
        return dict;
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
                detailPanel.SetVerticesPerEdge(verticesPerEdge!);
                detailPanel.SetAberrations(numAbberations);
                detailPanel.SetDeformationCycles(numDeformationCycles);

                // Spherical Harmonics values (preferred) or Scaling values (backward compatibility)
                if (hasSphericalHarmonics)
                {
                    detailPanel.SetTectonicsValues("SH Amplitude", shAmplitudeRange!);
                }
                else if (hasScaling)
                {
                    detailPanel.SetTectonicsValues("X Scale", xScaleRange!);
                    detailPanel.SetTectonicsValues("Y Scale", yScaleRange!);
                    detailPanel.SetTectonicsValues("Z Scale", zScaleRange!);
                }

                // Noise values
                detailPanel.SetTectonicsValues("Amplitude", amplitudeRange!);
                detailPanel.SetTectonicsValues("Scaling", scalingRange!);
                detailPanel.SetTectonicsValues(
                    "Octaves",
                    Array.ConvertAll(octaveRange!, x => (float)x)
                );
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

                // Spherical Harmonics values (preferred) or Scaling values (backward compatibility)
                if (hasSphericalHarmonics)
                {
                    shAmplitudeRange = detailPanel.GetTectonicsValues("SH Amplitude");
                }
                else if (hasScaling)
                {
                    xScaleRange = detailPanel.GetTectonicsValues("X Scale");
                    yScaleRange = detailPanel.GetTectonicsValues("Y Scale");
                    zScaleRange = detailPanel.GetTectonicsValues("Z Scale");
                }

                // Noise values
                amplitudeRange = detailPanel.GetTectonicsValues("Amplitude");
                scalingRange = detailPanel.GetTectonicsValues("Scaling");
                var octavesFloat = detailPanel.GetTectonicsValues("Octaves");
                octaveRange = Array.ConvertAll(octavesFloat, x => (int)x);
            }
        }
    }
}
