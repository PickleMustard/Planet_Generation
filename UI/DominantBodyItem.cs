using System;
using System.Text.RegularExpressions;
using Godot;
using Structures.Enums;
using UtilityLibrary.DataLoading;

namespace UI;

public partial class DominantBodyItem : HBoxContainer
{
    [Signal]
    public delegate void ItemUpdateEventHandler();

    [Signal]
    public delegate void ExpandMenuEventHandler(String sender, bool toggle);

    [Signal]
    public delegate void DistanceSubmittedEventHandler(DominantBodyItem item);

    [Signal]
    public delegate void EccentricitySubmittedEventHandler(DominantBodyItem item);

    [Signal]
    public delegate void MassSubmittedEventHandler(DominantBodyItem item);

    [Signal]
    public delegate void OnRemoveRequestedEventHandler(DominantBodyItem item);

    [Export]
    public Button? Toggle;

    [Export]
    public Button? RemoveItem;

    [Export]
    public OptionButton? OptionButton;

    [Export]
    public SpinBox? Mass;

    [Export]
    public SpinBox? BodySize;

    [Export]
    public SpinBox? Inclination;

    [Export]
    public SpinBox? StartAngle;

    //public Action<DominantBodyItem>? OnRemoveRequested;

    private String? bodyName;
    private const float Limit = 10000f; // constrain within ±10,000 units (mass 0..10,000)
    private const float MassLimit = 100000000f; // constrain within ±100,000,000,000 units (mass 0..100,000,000,000)
    private const float SizeLimit = 10000f; // constrain within ±10,000 units (size 0..10,000)

    private Vector3 position;
    private Vector3 velocity;

    private int subdivisions;
    private int[,]? verticesPerEdge;
    private int numAbberations;
    private int numDeformationCycles;

    bool hasSphericalHarmonics = false;
    private float[]? shAmplitudeRange;

    public override void _EnterTree()
    {
        // Hook remove
        var remove = GetNodeOrNull<Button>("MainContent/Header/RemoveItem");
        if (remove != null)
        {
            remove.Pressed += () => EmitSignal(SignalName.OnRemoveRequested, this);
        }

        // Cache field nodes if not set via exported references
        OptionButton ??= GetNodeOrNull<OptionButton>(
            "MainContent/Content/BodyTypeContent/OptionButton"
        );
        Mass ??= GetNodeOrNull<SpinBox>("MainContent/Content/MassContent/mass");
        BodySize ??= GetNodeOrNull<SpinBox>("MainContent/Content/SizeContent/size");
        StartAngle ??= GetNodeOrNull<SpinBox>("MainContent/Content/StartAngleContent/startAngle");
        Inclination ??= GetNodeOrNull<SpinBox>(
            "MainContent/Content/InclinationContent/inclination"
        );

        // Apply input constraints to fields
        ApplyConstraints();

        // Populate body types from YAML configuration and hook selection
        if (OptionButton != null)
        {
            PlanetaryTypeLoader.PopulateOptionButton(
                OptionButton,
                PlanetaryTypeLoader.GetDominantBodyTypes()
            );

            OptionButton.ItemSelected += idx =>
            {
                var internalName = PlanetaryTypeLoader.GetSelectedInternalName(OptionButton);
                if (internalName == null)
                    return;
                var type = PlanetaryTypeLoader.ToCelestialBodyType(internalName);
                UpdateHeaderFromBodyType(OptionButton.GetItemText((int)idx));
                ApplyTemplate(type);
                EmitSignal(SignalName.ItemUpdate);
            };

            // Ensure a valid initial selection and template
            if (OptionButton.ItemCount > 0)
            {
                if (OptionButton.Selected < 0)
                    OptionButton.Select(0);
                var initialName = PlanetaryTypeLoader.GetSelectedInternalName(OptionButton);
                if (initialName != null)
                {
                    ApplyTemplate(PlanetaryTypeLoader.ToCelestialBodyType(initialName));
                    UpdateHeaderFromBodyType(OptionButton.GetItemText(OptionButton.Selected));
                }
            }
        }

        if (Mass != null)
        {
            Mass.GetLineEdit().TextSubmitted += idx => EmitSignal(SignalName.MassSubmitted, this);
        }
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

        if (Inclination != null)
        {
            Inclination.MinValue = 0.0;
            Inclination.MaxValue = Limit;
            Inclination.AllowGreater = true;
            Inclination.AllowLesser = false;
        }
        if (StartAngle != null)
        {
            StartAngle.MinValue = 0.0;
            StartAngle.MaxValue = 360.0;
            StartAngle.AllowGreater = true;
            StartAngle.AllowLesser = false;
        }
    }

    public void ApplyTemplate(CelestialBodyType type)
    {
        // Read defaults from TOML in Configuration/SystemGen with safe fallbacks
        var t = TemplateHelpers.GetCelestialBodyDefaults(type);
        var template = (Godot.Collections.Dictionary)t["template"];
        bodyName = PickName((Godot.Collections.Dictionary)t["possible_names"]);

        if (template.ContainsKey("starting_angle"))
            SetStartingAngle((float)template["starting_angle"]);
        if (template.ContainsKey("vertical_offset"))
            SetInclination((float)template["vertical_offset"]);
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

        if (t.ContainsKey("spherical_harmonics_settings"))
        {
            var sphSettings = (Godot.Collections.Dictionary)t["spherical_harmonics_settings"];
            hasSphericalHarmonics = true;
            shAmplitudeRange = (float[])sphSettings["amplitude_range"];
        }
    }

    public void SetConfiguration(Godot.Collections.Dictionary t)
    {
        if (!t.ContainsKey("template"))
        {
            GD.PrintErr(
                "DominantBodyItem.SetConfiguration(): missing 'template' key in dictionary"
            );
            return;
        }
        var template = (Godot.Collections.Dictionary)t["template"];

        // Read central_parameters for inclination/starting_angle (matches ToParams() output)
        if (t.ContainsKey("central_parameters"))
        {
            var centralParams = (Godot.Collections.Dictionary)t["central_parameters"];
            if (centralParams.ContainsKey("starting_angle"))
                SetStartingAngle((float)centralParams["starting_angle"]);
            if (centralParams.ContainsKey("inclination"))
                SetInclination((float)centralParams["inclination"]);
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

        if (t.ContainsKey("spherical_harmonics_settings"))
        {
            var sphSettings = (Godot.Collections.Dictionary)t["spherical_harmonics_settings"];
            hasSphericalHarmonics = true;
            if (sphSettings.ContainsKey("amplitude_range"))
                shAmplitudeRange = (float[])sphSettings["amplitude_range"];
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

        string chosen = (string)names[random.RandiRange(0, names.Count - 1)];
        chosen = Regex.Replace(chosen, @"\s", "");
        chosen = chosen.ToLower();
        return chosen;
    }

    public void UpdateHeaderFromBodyType(string typeName)
    {
        var headerBtn = GetNodeOrNull<Button>("MainContent/Header/Toggle");
        if (headerBtn != null)
        {
            headerBtn.Text = $"{bodyName} ({typeName})";
        }
    }

    public Godot.Collections.Dictionary ToParams()
    {
        Godot.Collections.Dictionary dict = new Godot.Collections.Dictionary();
        var ob = GetNode<OptionButton>("MainContent/Content/BodyTypeContent/OptionButton");

        dict["type"] = PlanetaryTypeLoader.GetSelectedInternalName(ob) ?? "Star";
        dict.Add("name", bodyName!);
        var templateDict = new Godot.Collections.Dictionary();

        // Planetary bodies use orbital parameters
        templateDict.Add("mass", GetBodyMass());
        templateDict.Add("size", GetBodySize());
        templateDict.Add("position", GetBodyPosition());
        templateDict.Add("velocity", GetBodyVelocity());
        dict.Add("template", templateDict);

        // Add orbital parameters (per-body only; apogee/perigee are shared at the section level)
        var centralParams = new Godot.Collections.Dictionary();
        centralParams["inclination"] = GetInclination();
        centralParams["starting_angle"] = GetStartingAngle();
        dict.Add("central_parameters", centralParams);

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

        if (hasSphericalHarmonics)
        {
            Godot.Collections.Dictionary shDict = new Godot.Collections.Dictionary();
            shDict.Add("amplitude_range", shAmplitudeRange!);
            dict.Add("spherical_harmonics_settings", shDict);
        }

        return dict;
    }

    public float GetInclination() => (float)Inclination!.Value;

    public void SetInclination(float value)
    {
        float vertOffset = Mathf.Clamp(value, 0f, Limit);
        if (Inclination != null)
            Inclination.Value = vertOffset;
    }

    public float GetStartingAngle() => (float)StartAngle!.Value;

    public void SetStartingAngle(float value)
    {
        float startingAngle = Mathf.Clamp(value, 0f, 360f);
        if (StartAngle != null)
            StartAngle.Value = startingAngle;
    }

    public float GetBodySize()
    {
        return (float)BodySize!.Value;
    }

    public float GetBodyMass()
    {
        return (float)Mass!.Value;
    }

    public void SetBodyPosition(Vector3 value)
    {
        position = value;
    }

    public Vector3 GetBodyPosition()
    {
        return position;
    }

    public void SetBodyVelocity(Vector3 value)
    {
        velocity = value;
    }

    public Vector3 GetBodyVelocity()
    {
        return velocity;
    }

    public string GetBodyName() => bodyName ?? "Unnamed";
}
