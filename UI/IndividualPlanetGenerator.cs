using System;
using Godot;
using Godot.Collections;
using ProceduralGeneration;
using ProceduralGeneration.MeshGeneration;
using ProceduralGeneration.PlanetGeneration;
using Structures;
using Structures.Enums;
using UI.Generation;
using UtilityLibrary;
using UtilityLibrary.DataLoading;
using UtilityLibrary.NameGeneration;

namespace UI;

public partial class IndividualPlanetGenerator : Control
{
    private const float MASS_LIMIT = 100000000f;
    private const float SIZE_LIMIT = 10000f;
    private const float ORBIT_DISTANCE = 3.0f;
    private const float AUTO_ORBIT_SPEED = 0.3f;
    private const float MOUSE_SENSITIVITY = 0.005f;
    private const float RESUME_AUTO_ORBIT_DELAY = 3.0f;

    [Export]
    public Node3D? PlanetContainer;

    [Export]
    public Camera3D? PreviewCamera;

    // UI references
    [Export]
    public OptionButton? _bodyTypeOption;

    [Export]
    public SpinBox? _mass;

    [Export]
    public SpinBox? _size;

    [Export]
    public SpinBox? _subdivisions;

    [Export]
    public VBoxContainer? _subdivisionsContainer;

    [Export]
    public SpinBox? _num_abberation;

    [Export]
    public SpinBox? _num_deformation;

    [Export]
    public SpinBox? _num_continents;

    [Export]
    public SpinBox? _stress_scale;

    [Export]
    public SpinBox? _shear_scale;

    [Export]
    public SpinBox? _max_propagation_distance;

    [Export]
    public SpinBox? _propagation_falloff;

    [Export]
    public SpinBox? _inactive_stress_threshold;

    [Export]
    public SpinBox? _height_factor;

    [Export]
    public SpinBox? _shear_factor;

    [Export]
    public SpinBox? _compression_factor;

    [Export]
    public SpinBox? _transform_factor;

    [Export]
    public Button? _generateBtn;

    [Export]
    public Button? _resetBtn;

    [Export]
    public DetailPanel? _detailPanel;

    [Export]
    public VBoxContainer? _templatesList;

    [Export]
    public TextureViewerPanel? _textureViewer;

    // Generation state
    private CelestialBody? _currentPlanet;
    private string? _currentBodyName;
    private bool _isGenerating;

    // Camera orbit state
    private bool _isAutoOrbiting = true;
    private float _orbitAngleH;
    private float _orbitAngleV = 0.3f;
    private float _orbitRadius = 500f;
    private float _resumeTimer;
    private bool _isDragging;

    private PackedScene? _detailPanelScene;

    public override void _EnterTree()
    {
        _detailPanelScene = GD.Load<PackedScene>("res://UI/DetailPanel.tscn");

        // Setup detail panel
        var detailContainer = GetNodeOrNull<VBoxContainer>(
            "Panel/MarginContainer/TabContainer/Parameters/GenerationScroll/ParametersContainer/DetailPanelContainer"
        );
        if (detailContainer != null && _detailPanelScene != null)
        {
            _detailPanel = _detailPanelScene.Instantiate<DetailPanel>();
            detailContainer.AddChild(_detailPanel);
            _detailPanel.SetupForPlanetaryBodyItem();
        }

        // Setup mass/size constraints
        if (_mass != null)
        {
            _mass.MinValue = 0.0;
            _mass.MaxValue = MASS_LIMIT;
            _mass.Step = 1.0;
            _mass.AllowGreater = false;
            _mass.AllowLesser = false;
        }
        if (_size != null)
        {
            _size.MinValue = 1.0;
            _size.MaxValue = SIZE_LIMIT;
            _size.Step = 1.0;
            _size.AllowGreater = false;
            _size.AllowLesser = false;
        }

        // Populate body type dropdown
        if (_bodyTypeOption != null)
        {
            PlanetaryTypeLoader.PopulateOptionButton(
                _bodyTypeOption,
                PlanetaryTypeLoader.GetPlanetaryBodyTypes()
            );
            _bodyTypeOption.ItemSelected += OnBodyTypeSelected;
        }

        // Wire buttons
        if (_generateBtn != null)
            _generateBtn.Pressed += OnGeneratePressed;
        if (_resetBtn != null)
            _resetBtn.Pressed += OnResetPressed;

        // Load body type templates tab
        LoadBodyTypeTemplates();

        // Apply initial defaults
        if (_bodyTypeOption != null && _bodyTypeOption.ItemCount > 0)
        {
            OnBodyTypeSelected(0);
        }

        _subdivisions.GetLineEdit().TextSubmitted += UpdateSubdivisions;
        UpdateSubdivisions("");
    }

    public override void _Process(double delta)
    {
        if (PreviewCamera == null)
            return;

        // Handle auto-orbit resume timer
        if (!_isAutoOrbiting && !_isDragging)
        {
            _resumeTimer -= (float)delta;
            if (_resumeTimer <= 0f)
            {
                _isAutoOrbiting = true;
            }
        }

        // Auto-orbit camera
        if (_isAutoOrbiting)
        {
            _orbitAngleH += AUTO_ORBIT_SPEED * (float)delta;
        }

        UpdateCameraPosition();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                _isDragging = mouseButton.Pressed;
                if (!mouseButton.Pressed)
                {
                    _resumeTimer = RESUME_AUTO_ORBIT_DELAY;
                }
            }
            // Scroll to zoom
            if (mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                _orbitRadius = Mathf.Max(100f, _orbitRadius - _orbitRadius * 0.1f);
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                _orbitRadius = Mathf.Min(5000f, _orbitRadius + _orbitRadius * 0.1f);
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _isDragging)
        {
            _isAutoOrbiting = false;
            _orbitAngleH -= mouseMotion.Relative.X * MOUSE_SENSITIVITY;
            _orbitAngleV = Mathf.Clamp(
                _orbitAngleV - mouseMotion.Relative.Y * MOUSE_SENSITIVITY,
                -Mathf.Pi / 2f + 0.1f,
                Mathf.Pi / 2f - 0.1f
            );
        }
    }

    private void UpdateSubdivisions(string text)
    {
        foreach (var child in _subdivisionsContainer.GetChildren())
        {
            child.QueueFree();
        }

        var subdivisions = _subdivisions.Value;
        for (int i = 0; i < subdivisions; i++)
        {
            HBoxContainer indentedContainer = new HBoxContainer();
            indentedContainer.Name = $"VerticesIndex{i}Content";
            Label indexLabel = new Label();
            indexLabel.Text = $"Vertices Index {i}";
            indexLabel.Name = $"VerticesIndexLabel{i}";
            indentedContainer.AddChild(indexLabel);
            SpinBox verticesPerEdge = new SpinBox();
            verticesPerEdge.Name = $"VerticesPerEdgeSpinBox{i}";
            verticesPerEdge.MinValue = 1;
            verticesPerEdge.MaxValue = 10;
            verticesPerEdge.Step = 1;
            verticesPerEdge.Value = 2;
            verticesPerEdge.AllowLesser = false;
            verticesPerEdge.AllowGreater = false;
            indentedContainer.AddChild(verticesPerEdge);
            _subdivisionsContainer.AddChild(indentedContainer);
        }
    }

    private void UpdateCameraPosition()
    {
        if (PreviewCamera == null)
            return;

        float x = _orbitRadius * Mathf.Cos(_orbitAngleV) * Mathf.Cos(_orbitAngleH);
        float y = _orbitRadius * Mathf.Sin(_orbitAngleV);
        float z = _orbitRadius * Mathf.Cos(_orbitAngleV) * Mathf.Sin(_orbitAngleH);

        PreviewCamera.Position = new Vector3(x, y, z);
        PreviewCamera.LookAt(Vector3.Zero, Vector3.Up);
    }

    private void OnBodyTypeSelected(long idx)
    {
        if (_bodyTypeOption == null)
            return;

        var internalName = PlanetaryTypeLoader.GetSelectedInternalName(_bodyTypeOption);
        if (internalName == null)
            return;

        var type = PlanetaryTypeLoader.ToCelestialBodyType(internalName);
        ApplyDefaults(type);
    }

    private void ApplyDefaults(CelestialBodyType type)
    {
        var defaults = TemplateHelpers.GetCelestialBodyDefaults(type);

        if (!defaults.ContainsKey("template"))
            return;

        var template = (Dictionary)defaults["template"];

        // Set mass and size
        if (_mass != null && template.ContainsKey("mass"))
            _mass.Value = Mathf.Clamp((float)template["mass"], 0f, MASS_LIMIT);
        if (_size != null && template.ContainsKey("size"))
            _size.Value = Mathf.Clamp((float)template["size"], 0f, SIZE_LIMIT);

        // Set orbit radius based on planet size
        if (template.ContainsKey("size"))
            _orbitRadius = (float)template["size"] * ORBIT_DISTANCE;

        // Set base mesh values on detail panel
        if (_detailPanel != null && defaults.ContainsKey("base_mesh"))
        {
            var baseMesh = (Dictionary)defaults["base_mesh"];
            if (baseMesh.ContainsKey("subdivisions"))
                _detailPanel.SetSubdivisions((int)baseMesh["subdivisions"]);
            if (baseMesh.ContainsKey("vertices_per_edge"))
            {
                int subdivisions = (int)baseMesh["subdivisions"];
                var vpeArray = (Array<Array<int>>)baseMesh["vertices_per_edge"];
                var vpe = new int[subdivisions, 2];
                for (int i = 0; i < subdivisions && i < vpeArray.Count; i++)
                {
                    vpe[i, 0] = vpeArray[i][0];
                    vpe[i, 1] = vpeArray[i][1];
                }
                _detailPanel.SetVerticesPerEdge(vpe);
            }
            if (baseMesh.ContainsKey("num_abberations"))
                _detailPanel.SetAberrations((int)baseMesh["num_abberations"]);
            if (baseMesh.ContainsKey("num_deformation_cycles"))
                _detailPanel.SetDeformationCycles((int)baseMesh["num_deformation_cycles"]);
        }

        // Set tectonic values on detail panel
        if (_detailPanel != null && defaults.ContainsKey("tectonics"))
        {
            var tectonics = (Dictionary)defaults["tectonics"];
            SetTectonicRange(tectonics, "num_continents", "Continents", isInt: true);
            SetTectonicRange(tectonics, "stress_scale", "Stress Scale");
            SetTectonicRange(tectonics, "shear_scale", "Shear Scale");
            SetTectonicRange(tectonics, "max_propagation_distance", "Max Propagation Distance");
            SetTectonicRange(tectonics, "propagation_falloff", "Propagation Falloff");
            SetTectonicRange(tectonics, "inactive_stress_threshold", "Inactive Stress Threshold");
            SetTectonicRange(tectonics, "general_height_scale", "General Height Scale");
            SetTectonicRange(tectonics, "general_shear_scale", "General Shear Scale");
            SetTectonicRange(tectonics, "general_compression_scale", "General Compression Scale");
            SetTectonicRange(tectonics, "general_transform_scale", "General Transform Scale");
        }

        // Generate a name
        if (defaults.ContainsKey("possible_names"))
        {
            _currentBodyName = NameGenerator.PickName((Dictionary)defaults["possible_names"]);
        }
    }

    private void SetTectonicRange(
        Dictionary tectonics,
        string key,
        string label,
        bool isInt = false
    )
    {
        if (_detailPanel == null || !tectonics.ContainsKey(key))
            return;

        if (isInt)
        {
            var values = (int[])tectonics[key];
            _detailPanel.SetTectonicsValues(label, new float[] { values[0], values[1] });
        }
        else
        {
            var values = (float[])tectonics[key];
            _detailPanel.SetTectonicsValues(label, values);
        }
    }

    private void OnGeneratePressed()
    {
        if (_isGenerating)
            return;

        _isGenerating = true;

        // Clear previous planet
        if (_currentPlanet != null && IsInstanceValid(_currentPlanet))
        {
            _currentPlanet.QueueFree();
            _currentPlanet = null;
        }

        // Clear texture viewer
        _textureViewer?.Clear();

        var bodyParams = CollectParams();

        // Extract parameters for builder
        var templateDict = (Dictionary)bodyParams["template"];
        float mass = (float)templateDict["mass"];
        string typeStr = (string)bodyParams["type"];
        var bodyType = (CelestialBodyType)Enum.Parse(typeof(CelestialBodyType), typeStr);
        string name = (string)bodyParams["name"];

        // Select subtype
        var rng = Randomizer.GetRandomNumberGenerator();
        var auManager = new AUProbabilityManager(rng);
        BodyClassification classification = auManager.SelectClassification(bodyType, 0f);

        // Build the celestial body
        var mesh = new UnifiedCelestialMesh();
        var builder = new CelestialBody.Builder();
        builder
            .WithVelocity(Vector3.Zero)
            .WithMass(mass)
            .WithBodyDict(bodyParams)
            .WithMesh(mesh)
            .WithClassification(classification)
            .WithName(name);

        _currentPlanet = builder.Build();

        if (PlanetContainer != null)
        {
            PlanetContainer.AddChild(_currentPlanet);
            _currentPlanet.Position = Vector3.Zero;
        }

        _currentPlanet.StartMeshGeneration(
            onCompleted: OnPlanetGenerationComplete,
            onFailed: OnPlanetGenerationFailed
        );
    }

    private void OnPlanetGenerationComplete(CelestialBody completedBody)
    {
        _isGenerating = false;

        // Adjust orbit radius to fit planet
        if (_size != null)
            _orbitRadius = (float)_size.Value * ORBIT_DISTANCE;

        // Update texture viewer with new body
        _textureViewer?.CallDeferred("UpdateForBody", completedBody);

        GameLogger.Info($"Planet '{_currentBodyName}' generated successfully");
    }

    private void OnPlanetGenerationFailed(CelestialBody failedBody, string error)
    {
        _isGenerating = false;
        GameLogger.Error($"Planet generation failed: {error}");
    }

    private Dictionary CollectParams()
    {
        var dict = new Dictionary();

        // Type
        string typeStr = "RockyPlanet";
        if (_bodyTypeOption != null)
        {
            typeStr = PlanetaryTypeLoader.GetSelectedInternalName(_bodyTypeOption) ?? "RockyPlanet";
        }
        dict["type"] = typeStr;
        dict["name"] = _currentBodyName ?? "Planet";

        // Template (mass, size, position, velocity)
        var templateDict = new Dictionary();
        templateDict["mass"] =
            _mass != null ? Mathf.Clamp((float)_mass.Value, 0f, MASS_LIMIT) : 1000f;
        templateDict["size"] =
            _size != null ? Mathf.Clamp((float)_size.Value, 0f, SIZE_LIMIT) : 150f;
        dict["template"] = templateDict;

        // Base mesh
        var baseMesh = new Dictionary();

        var vpe = _subdivisions.Value;
        baseMesh["subdivisions"] = vpe;
        var vpeArray = new Array<Array<int>>();
        var indices = _subdivisionsContainer.GetChildren();
        for (int i = 0; i < vpe; i++)
        {
            var row = new Array<int>();
            row.Add(Mathf.RoundToInt(((SpinBox)indices[i].GetChild(1)).Value));
            row.Add(Mathf.RoundToInt(((SpinBox)indices[i].GetChild(1)).Value));
            vpeArray.Add(row);
        }
        baseMesh["vertices_per_edge"] = vpeArray;
        baseMesh["num_abberations"] = _num_abberation;
        baseMesh["num_deformation_cycles"] = _num_deformation;
        dict["base_mesh"] = baseMesh;

        // Tectonics
        var tectonics = new Dictionary();
        tectonics["num_continents"] = new int[]
        {
            (int)_num_continents.Value,
            (int)_num_continents.Value,
        };
        tectonics["stress_scale"] = new double[] { _stress_scale.Value, _stress_scale.Value };
        tectonics["shear_scale"] = new double[] { _shear_scale.Value, _shear_scale.Value };
        tectonics["max_propagation_distance"] = new double[]
        {
            _max_propagation_distance.Value,
            _max_propagation_distance.Value,
        };
        tectonics["propagation_falloff"] = new double[]
        {
            _propagation_falloff.Value,
            _propagation_falloff.Value,
        };
        tectonics["inactive_stress_threshold"] = new double[]
        {
            _inactive_stress_threshold.Value,
            _inactive_stress_threshold.Value,
        };
        tectonics["general_height_scale"] = new double[]
        {
            _height_factor.Value,
            _height_factor.Value,
        };
        tectonics["general_shear_scale"] = new double[]
        {
            _shear_factor.Value,
            _shear_factor.Value,
        };
        tectonics["general_compression_scale"] = new double[]
        {
            _compression_factor.Value,
            _compression_factor.Value,
        };
        tectonics["general_transform_scale"] = new double[]
        {
            _transform_factor.Value,
            _transform_factor.Value,
        };
        dict["tectonics"] = tectonics;

        return dict;
    }

    private void OnResetPressed()
    {
        if (_bodyTypeOption == null)
            return;

        var internalName = PlanetaryTypeLoader.GetSelectedInternalName(_bodyTypeOption);
        if (internalName == null)
            return;

        var type = PlanetaryTypeLoader.ToCelestialBodyType(internalName);
        ApplyDefaults(type);
    }

    private void LoadBodyTypeTemplates()
    {
        if (_templatesList == null)
            return;

        var types = PlanetaryTypeLoader.GetPlanetaryBodyTypes();
        foreach (var entry in types)
        {
            var button = new Button();
            button.Text = entry.DisplayName;
            var internalName = entry.Name;
            button.Pressed += () =>
            {
                // Select the matching type in the dropdown
                if (_bodyTypeOption != null)
                {
                    PlanetaryTypeLoader.SelectByInternalName(_bodyTypeOption, internalName);
                }
                var type = PlanetaryTypeLoader.ToCelestialBodyType(internalName);
                ApplyDefaults(type);
            };
            _templatesList.AddChild(button);
        }
    }
}
