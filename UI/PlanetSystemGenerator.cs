using System;
using Godot;
using Godot.Collections;
using Structures.Enums;
using UtilityLibrary;
using UtilityLibrary.GameMath.Orbital;
using FileAccess = Godot.FileAccess;

namespace UI;

public partial class PlanetSystemGenerator : Control
{
    Vector2 BASE_SIZE = new Vector2(400, 650);
    Vector2 EXPANDED_SIZE = new Vector2(600, 650);

    [Signal]
    public delegate void GeneratePressedEventHandler(
        Array<Dictionary> dominantBodies,
        Array<Dictionary> satelliteBelts,
        Array<Dictionary> planetaryBodies,
        Barycenter barycenter
    );

    [Signal]
    public delegate void ValidatePressedEventHandler(Array<Dictionary> bodies);

    // Section headers
    private SectionHeader? _dominantSectionHeader;
    private SectionHeader? _satelliteBeltsSectionHeader;
    private SectionHeader? _planetarySectionHeader;

    // Shared orbital parameters for dominant bodies (visible only when 2+ bodies exist)
    private VBoxContainer? _sharedOrbitalParams;
    private SpinBox? _sharedApogee;
    private SpinBox? _sharedPerigee;

    // Body lists
    private VBoxContainer? _dominantBodiesList;
    private VBoxContainer? _satelliteBeltsList;
    private VBoxContainer? _planetaryBodiesList;

    private Button? _generateBtn;
    private Button? _validateBtn;
    private Button? _saveBtn;
    private PackedScene? _DominantBodyItemScene;
    private PackedScene? _PlanetaryBodyItemScene;
    private PackedScene? _satelliteBeltItemScene;
    private TabContainer? _tabContainer;
    private VBoxContainer? _templatesList;
    private TextureRect? _stabilityIndicator;

    private Texture2D? _checkMark;
    private Texture2D? _xMark;
    private Dictionary<String, bool>? _toggledSubcontainers;
    private Array<DominantBodyItem>? _autoCalculateSubscribers;

    // Barycenter and dominant body tracking
    private Barycenter _barycenter = new Barycenter(Vector3.Zero, Vector3.Zero, 0f);
    private int _mostInfluentialBodyIndex = -1;
    private Array<string> _dominantBodyOptions = new Array<string>();

    // Auto-balance state
    private bool _isLoadingTemplate = false;
    private bool _shouldAutoCalculate = true;
    private const float DEFAULT_BASE_SEPARATION = 500f;

    public override void _EnterTree()
    {
        this.AddToGroup("GenerationMenu");
        var parent = GetParent() as Control;
        parent!.Size = BASE_SIZE;
        _toggledSubcontainers = new Dictionary<String, bool>();

        // Load scenes
        _DominantBodyItemScene = GD.Load<PackedScene>("res://UI/DominantBodyItem.tscn");
        _PlanetaryBodyItemScene = GD.Load<PackedScene>("res://UI/PlanetaryBodyItem.tscn");
        _satelliteBeltItemScene = GD.Load<PackedScene>("res://UI/SatelliteBeltItem.tscn");

        // Get tab container
        _tabContainer = GetNode<TabContainer>("MarginContainer/TabContainer");

        // Get section headers
        _dominantSectionHeader = GetNode<SectionHeader>(
            "MarginContainer/TabContainer/BodiesTab/SystemScrollBar/SystemScrollContainer/DominantBodiesSection"
        );
        _satelliteBeltsSectionHeader = GetNode<SectionHeader>(
            "MarginContainer/TabContainer/BodiesTab/SystemScrollBar/SystemScrollContainer/SatelliteBeltsSection"
        );
        _planetarySectionHeader = GetNode<SectionHeader>(
            "MarginContainer/TabContainer/BodiesTab/SystemScrollBar/SystemScrollContainer/PlanetaryBodiesSection"
        );

        // Get shared orbital parameters container and spinboxes
        _sharedOrbitalParams = GetNode<VBoxContainer>(
            "MarginContainer/TabContainer/BodiesTab/SystemScrollBar/SystemScrollContainer/DominantBodiesSection/SharedOrbitalParams"
        );
        _sharedApogee = GetNode<SpinBox>(
            "MarginContainer/TabContainer/BodiesTab/SystemScrollBar/SystemScrollContainer/DominantBodiesSection/SharedOrbitalParams/ApogeeContent/apogee"
        );
        _sharedPerigee = GetNode<SpinBox>(
            "MarginContainer/TabContainer/BodiesTab/SystemScrollBar/SystemScrollContainer/DominantBodiesSection/SharedOrbitalParams/PerigeeContent/perigee"
        );

        // Apply constraints to shared orbital parameter spinboxes
        if (_sharedApogee != null)
        {
            _sharedApogee.MinValue = 0.0;
            _sharedApogee.MaxValue = 10000.0;
            _sharedApogee.AllowGreater = true;
            _sharedApogee.AllowLesser = false;
        }
        if (_sharedPerigee != null)
        {
            _sharedPerigee.MinValue = 0.0;
            _sharedPerigee.MaxValue = 10000.0;
            _sharedPerigee.AllowGreater = true;
            _sharedPerigee.AllowLesser = false;
        }

        // Get body lists
        _dominantBodiesList = GetNode<VBoxContainer>(
            "MarginContainer/TabContainer/BodiesTab/SystemScrollBar/SystemScrollContainer/DominantBodiesSection/Content/ContentScroll/DominantBodiesList"
        );
        _satelliteBeltsList = GetNode<VBoxContainer>(
            "MarginContainer/TabContainer/BodiesTab/SystemScrollBar/SystemScrollContainer/SatelliteBeltsSection/Content/ContentScroll/SatelliteBeltsList"
        );
        _planetaryBodiesList = GetNode<VBoxContainer>(
            "MarginContainer/TabContainer/BodiesTab/SystemScrollBar/SystemScrollContainer/PlanetaryBodiesSection/Content/ContentScroll/PlanetaryBodiesList"
        );

        // Get buttons
        _generateBtn = GetNode<Button>(
            "MarginContainer/TabContainer/BodiesTab/GenerationMargin/GenerationRow/GenerateButton"
        );
        _validateBtn = GetNode<Button>(
            "MarginContainer/TabContainer/BodiesTab/GenerationMargin/GenerationRow/ValidateButton"
        );
        _saveBtn = GetNode<Button>(
            "MarginContainer/TabContainer/BodiesTab/GenerationMargin/GenerationRow/SaveFileButton"
        );
        _stabilityIndicator = GetNode<TextureRect>(
            "MarginContainer/TabContainer/BodiesTab/GenerationMargin/GenerationRow/StabilityIndicator"
        );
        _templatesList = GetNode<VBoxContainer>(
            "MarginContainer/TabContainer/TemplatesTab/TemplatesScroll/TemplatesList"
        );

        // Wire section header events
        _dominantSectionHeader!.AddPressed += AddDominantBodyItem;
        _dominantSectionHeader!.RemovePressed += RemoveLastDominantBodyItem;
        _satelliteBeltsSectionHeader!.AddPressed += AddSatelliteBeltItem;
        _satelliteBeltsSectionHeader!.RemovePressed += RemoveLastSatelliteBeltItem;
        _planetarySectionHeader!.AddPressed += AddPlanetaryBodyItem;
        _planetarySectionHeader!.RemovePressed += RemoveLastPlanetaryBodyItem;

        // Wire button events
        _generateBtn!.Pressed += OnGeneratePressed;
        _validateBtn!.Pressed += OnValidatePressed;
        _saveBtn!.Pressed += OnSavePressed;

        // Load textures
        _checkMark = GD.Load<Texture2D>("res://UI/checkmark.svg");
        _xMark = GD.Load<Texture2D>("res://UI/xmark.svg");
        _stabilityIndicator!.Texture = _checkMark;

        _autoCalculateSubscribers = new Array<DominantBodyItem>();

        // Initialize with default state
        UpdateDominantBodyOptions();
        LoadSystemTemplates();
        UpdateSectionCountLabels();
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateSectionCountLabels();
    }

    public void ExpandMenu(String sender, bool toggle)
    {
        var parent = GetParent() as Control;
        if (_toggledSubcontainers!.ContainsKey(sender) && !toggle)
        {
            _toggledSubcontainers.Remove(sender);
            if (_toggledSubcontainers.Count == 0)
            {
                parent!.Size = BASE_SIZE;
            }
        }
        else
        {
            parent!.Size = EXPANDED_SIZE;
            if (!_toggledSubcontainers.ContainsKey(sender))
                _toggledSubcontainers.Add(sender, toggle);
        }
    }

    #region Dominant Bodies

    private void AddDominantBodyItem()
    {
        if (_DominantBodyItemScene == null)
            return;
        var node = _DominantBodyItemScene.Instantiate<DominantBodyItem>();
        _dominantBodiesList!.AddChild(node);

        // Wire events
        node.OnRemoveRequested += HandleDominantBodyRemove;
        node.ExpandMenu += ExpandMenu;
        node.MassSubmitted += OnMassSubmitted;

        UpdateDominantBodyOptions();
        UpdateSharedOrbitalParamsVisibility();
        UpdateSectionCountLabels();
    }

    private void RemoveLastDominantBodyItem()
    {
        if (_dominantBodiesList!.GetChildCount() == 0)
            return;
        var last = _dominantBodiesList.GetChild(_dominantBodiesList.GetChildCount() - 1);
        _dominantBodiesList.RemoveChild(last);
        last.QueueFree();
        UpdateDominantBodyOptions();
        UpdateSharedOrbitalParamsVisibility();
        UpdateSectionCountLabels();
    }

    private void HandleDominantBodyRemove(DominantBodyItem item)
    {
        if (IsInstanceValid(item) && item.GetParent() == _dominantBodiesList)
        {
            _dominantBodiesList.RemoveChild(item);
            item.QueueFree();
            UpdateDominantBodyOptions();
            UpdateSharedOrbitalParamsVisibility();
            UpdateSectionCountLabels();
        }
    }

    /// <summary>
    /// Shows or hides the shared Apogee/Perigee section based on how many
    /// dominant bodies exist. When there are 2+ bodies they share a single
    /// relative orbit, so one pair of Apogee/Perigee controls is shown at the
    /// section level. For a single body the controls are hidden because that
    /// body is placed at the origin with no orbit.
    /// </summary>
    private void UpdateSharedOrbitalParamsVisibility()
    {
        if (_sharedOrbitalParams == null || _dominantBodiesList == null)
            return;

        _sharedOrbitalParams.Visible = _dominantBodiesList.GetChildCount() >= 2;
    }

    #endregion

    #region Satellite Belts

    private void AddSatelliteBeltItem()
    {
        if (_satelliteBeltItemScene == null)
            return;
        var node = _satelliteBeltItemScene.Instantiate<SatelliteBeltItem>();
        _satelliteBeltsList!.AddChild(node);
        node.SubscribeEvents();

        // Wire events
        node.OnRemoveRequested += HandleSatelliteBeltRemove;
        node.ItemUpdate += OnSatelliteBeltItemUpdate;

        // Update orbital center options
        node.UpdateOrbitalCenterOptions(_dominantBodyOptions);

        UpdateSectionCountLabels();
    }

    private void RemoveLastSatelliteBeltItem()
    {
        if (_satelliteBeltsList!.GetChildCount() == 0)
            return;
        var last = _satelliteBeltsList.GetChild(_satelliteBeltsList.GetChildCount() - 1);
        last.QueueFree();
        UpdateSectionCountLabels();
    }

    private void HandleSatelliteBeltRemove(SatelliteBeltItem item)
    {
        if (IsInstanceValid(item) && item.GetParent() == _satelliteBeltsList)
        {
            item.QueueFree();
            UpdateSectionCountLabels();
        }
    }

    private void OnSatelliteBeltItemUpdate()
    {
        // Satellite belts don't affect barycenter calculation
    }

    private void AddSatelliteBeltFromTemplate(Dictionary satDict, int orbitalCenterIndex)
    {
        if (_satelliteBeltItemScene == null)
            return;

        var node = _satelliteBeltItemScene.Instantiate<SatelliteBeltItem>();
        _satelliteBeltsList!.AddChild(node);
        node.SubscribeEvents();

        // Wire events
        node.OnRemoveRequested += HandleSatelliteBeltRemove;
        node.ItemUpdate += OnSatelliteBeltItemUpdate;

        // Set the belt type in the OptionButton and apply template values
        node.SetTemplate(satDict);

        // Update orbital center options and select the correct one
        node.UpdateOrbitalCenterOptions(_dominantBodyOptions);
        if (orbitalCenterIndex >= -1)
        {
            // SetOrbitalCenterIndex is not available on SatelliteBeltItem directly,
            // but UpdateOrbitalCenterOptions handles the dropdown; we need to set
            // the internal index via the dropdown selection
            // The orbital center dropdown maps: index 0 = Barycenter (-1), index 1+ = dominant bodies
            var dropdown = node.GetNodeOrNull<OptionButton>(
                "Content/OrbitalCenterContent/OrbitalCenterDropdown"
            );
            if (dropdown != null && orbitalCenterIndex + 1 < dropdown.ItemCount)
            {
                dropdown.Select(orbitalCenterIndex + 1);
            }
        }
    }

    #endregion

    #region Planetary Bodies

    private void AddPlanetaryBodyItem()
    {
        if (_PlanetaryBodyItemScene == null)
            return;
        var node = _PlanetaryBodyItemScene.Instantiate<PlanetaryBodyItem>();
        _planetaryBodiesList!.AddChild(node);

        // Wire events
        node.OnRemoveRequested += HandlePlanetaryBodyRemove;
        node.ItemUpdate += OnPlanetaryBodyItemUpdate;
        node.ExpandMenu += ExpandMenu;

        // Update orbital center options
        node.UpdateOrbitalCenterOptions(_dominantBodyOptions);

        UpdateSectionCountLabels();
    }

    private void RemoveLastPlanetaryBodyItem()
    {
        if (_planetaryBodiesList!.GetChildCount() == 0)
            return;
        var last = _planetaryBodiesList.GetChild(_planetaryBodiesList.GetChildCount() - 1);
        last.QueueFree();
        UpdateSectionCountLabels();
    }

    private void HandlePlanetaryBodyRemove(PlanetaryBodyItem item)
    {
        if (IsInstanceValid(item) && item.GetParent() == _planetaryBodiesList)
        {
            item.QueueFree();
            UpdateSectionCountLabels();
        }
    }

    private void OnPlanetaryBodyItemUpdate()
    {
        // Planetary bodies don't affect barycenter calculation
    }

    #endregion

    #region Barycenter and Dominant Body Tracking

    private void UpdateDominantBodyOptions()
    {
        _dominantBodyOptions = new Array<string> { "barycenter" };

        int index = 0;
        foreach (Node child in _dominantBodiesList!.GetChildren())
        {
            if (child is DominantBodyItem bi)
            {
                string bodyName = bi.GetBodyName().ToLower();
                if (string.IsNullOrEmpty(bodyName))
                    bodyName = $"Body {index + 1}";
                _dominantBodyOptions.Add(bodyName);
                index++;
            }
        }

        // Update all planetary bodies and satellite belts with new options
        UpdateAllOrbitalCenterOptions();
    }

    private void UpdateAllOrbitalCenterOptions()
    {
        // Update planetary bodies
        foreach (Node child in _planetaryBodiesList!.GetChildren())
        {
            if (child is PlanetaryBodyItem bi)
            {
                bi.UpdateOrbitalCenterOptions(_dominantBodyOptions);
            }
        }

        // Update satellite belts
        foreach (Node child in _satelliteBeltsList!.GetChildren())
        {
            if (child is SatelliteBeltItem sbi)
            {
                sbi.UpdateOrbitalCenterOptions(_dominantBodyOptions);
            }
        }
    }

    #endregion

    #region Auto-Balance Methods

    /// <summary>
    /// Full rebalance - recalculate barycenter and all velocities for dominant bodies.
    /// </summary>
    private void AutoBalanceDominantBodies()
    {
        if (_isLoadingTemplate)
            return;

        if (!_shouldAutoCalculate)
            return;

        // Collect positions and masses
        var positions = new System.Collections.Generic.List<Vector3>();
        var masses = new System.Collections.Generic.List<float>();
        var bodyItems = new System.Collections.Generic.List<DominantBodyItem>();

        foreach (Node child in _dominantBodiesList!.GetChildren())
        {
            //if (child is DominantBodyItem bi)
            //{
            //    float distance = bi.GetDistance();
            //    float startAngleDeg = bi.GetStartingAngle();
            //    float inclinationDeg = bi.GetInclination();
            //    float startAngleRad = Mathf.DegToRad(startAngleDeg);
            //    float inclinationRad = Mathf.DegToRad(inclinationDeg);
            //    Vector3 bodyPosition = new Vector3(
            //        distance * Mathf.Cos(startAngleRad) * Mathf.Cos(inclinationRad),
            //        distance * Mathf.Sin(inclinationRad),
            //        distance * Mathf.Sin(startAngleRad) * Mathf.Cos(inclinationRad)
            //    );

            //    positions.Add(bodyPosition);
            //    masses.Add(bi.GetBodyMass());
            //    bodyItems.Add(bi);
            //}
        }

        if (positions.Count == 0)
            return;

        // Handle single body case
        if (positions.Count == 1)
        {
            _barycenter.Position = Vector3.Zero;
            _barycenter.Mass = 0f;
            return;
        }

        // Calculate barycenter
        Vector3 totalPosition = Vector3.Zero;
        float totalMass = 0f;
        for (int i = 0; i < positions.Count; i++)
        {
            totalPosition += positions[i] * masses[i];
            totalMass += masses[i];
        }

        if (totalMass > 0f)
        {
            _barycenter.Position = totalPosition / totalMass;
            _barycenter.Mass = totalMass;
        }
        else
        {
            _barycenter.Position = Vector3.Zero;
            _barycenter.Mass = 0f;
        }

        // Recalculate velocities for all bodies
        var velocities = OrbitalMath.CalculateStableVelocitiesForDominantBodies(
            positions,
            masses,
            _barycenter
        );

        GD.Print(
            $"Auto-balanced {bodyItems.Count} dominant bodies around barycenter: {_barycenter}"
        );
    }

    /// <summary>
    /// Handler for when mass is edited (TextSubmitted) - full rebalance.
    /// </summary>
    private void OnMassSubmitted(DominantBodyItem item)
    {
        if (_isLoadingTemplate)
            return;

        if (!_shouldAutoCalculate)
            return;

        // Full rebalance when mass changes
        AutoBalanceDominantBodies();
    }

    /// <summary>
    /// Handler for auto-calculate toggle.
    /// </summary>
    private void OnShouldAutoCalculate(bool shouldAutoCalculate, PlanetaryBodyItem item)
    {
        _shouldAutoCalculate = shouldAutoCalculate;
        GD.Print($"Auto-calculate set to: {_shouldAutoCalculate}");
    }

    #endregion

    #region UI Updates

    private void UpdateSectionCountLabels()
    {
        int dominantCount = _dominantBodiesList?.GetChildCount() ?? 0;
        int beltCount = _satelliteBeltsList?.GetChildCount() ?? 0;
        int planetaryCount = _planetaryBodiesList?.GetChildCount() ?? 0;

        _dominantSectionHeader?.SetCount(dominantCount);
        _satelliteBeltsSectionHeader?.SetCount(beltCount);
        _planetarySectionHeader?.SetCount(planetaryCount);
    }

    #region Signal Handlers

    private void OnGeneratePressed()
    {
        // Build dominant bodies list
        var dominantBodies = new Array<Dictionary>();
        float totalMass = 0f;
        Vector3 weightedPositionSum = Vector3.Zero;

        // First pass: collect body params and compute positions from orbital parameters
        var bodyPositions = new System.Collections.Generic.List<Vector3>();
        var bodyMasses = new System.Collections.Generic.List<float>();
        var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();

        if (_dominantBodiesList!.GetChildCount() == 1)
        {
            GenerateSingleSystem(dominantBodies);
        }
        else if (_dominantBodiesList!.GetChildCount() == 2)
        {
            // Convert the body's distance + starting angle + inclination into a 3D position
            // relative to the origin, using spherical-to-Cartesian conversion
            float theta = rng.RandfRange(0f, Mathf.Pi * 2f); // Azimuthal angle [0, 2π]
            float phi = rng.RandfRange(0f, Mathf.Pi); // Polar angle [0, π]
            Vector3 pHat = new Vector3(
                Mathf.Sin(phi) * Mathf.Cos(theta),
                Mathf.Sin(phi) * Mathf.Sin(theta),
                Mathf.Cos(phi)
            ).Normalized();

            // Generate qHat as 90-degree rotation in orbital plane
            Vector3 upReference = Vector3.Up;
            if (Mathf.Abs(pHat.Dot(upReference)) > 0.99f)
            {
                upReference = Vector3.Right;
            }
            Vector3 qHat = -pHat.Cross(upReference).Normalized();
            GenerateBinarySystem(dominantBodies, pHat, qHat);
        }

        // Build satellite belts list
        var satelliteBelts = new Array<Dictionary>();
        foreach (Node child in _satelliteBeltsList!.GetChildren())
        {
            if (child is SatelliteBeltItem sbi)
                satelliteBelts.Add(sbi.ToParams());
        }

        // Build planetary bodies list
        var planetaryBodies = new Array<Dictionary>();
        foreach (Node child in _planetaryBodiesList!.GetChildren())
        {
            if (child is PlanetaryBodyItem bi)
                planetaryBodies.Add(bi.ToParams());
        }

        // Compute the mass-weighted barycenter from actual orbital positions
        Barycenter barycenter;
        if (totalMass > 0f)
        {
            weightedPositionSum /= totalMass;
            totalMass /= _dominantBodiesList!.GetChildCount();
            barycenter = new Barycenter(weightedPositionSum, Vector3.Zero, totalMass);
        }
        else
        {
            barycenter = new Barycenter(Vector3.Zero, Vector3.Zero, 0f);
        }

        _barycenter = barycenter;
        // Emit signal with all data including barycenter
        EmitSignal(
            SignalName.GeneratePressed,
            dominantBodies,
            satelliteBelts,
            planetaryBodies,
            _barycenter
        );
    }

    private void GenerateSingleSystem(Array<Dictionary> dominantBodies)
    {
        var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        DominantBodyItem? bi1 = _dominantBodiesList!.GetChild(0) as DominantBodyItem;
        bi1!.SetBodyPosition(Vector3.Zero);
        bi1!.SetBodyVelocity(Vector3.Zero);
        dominantBodies.Add(bi1!.ToParams());
    }

    private void GenerateBinarySystem(Array<Dictionary> dominantBodies, Vector3 pHat, Vector3 qHat)
    {
        DominantBodyItem? bi1 = _dominantBodiesList!.GetChild(0) as DominantBodyItem;
        DominantBodyItem? bi2 = _dominantBodiesList!.GetChild(1) as DominantBodyItem;
        float massA = bi1!.GetBodyMass();
        float massB = bi2!.GetBodyMass();

        // Both bodies share a single relative orbit defined by the section-level
        // apogee/perigee controls.
        float relativeApogee = (float)_sharedApogee!.Value;
        float relativePerigee = (float)_sharedPerigee!.Value;
        float startAngleRad = Mathf.DegToRad(bi1!.GetStartingAngle());

        // CalculateBinaryOrbitalState uses a single relative orbit and splits the
        // separation by mass ratio, guaranteeing the barycenter lies at the origin:
        //   M_A·posA + M_B·posB = 0 for any angle, eccentricity, or mass ratio.
        var (posA, velA, posB, velB) = OrbitalMath.CalculateBinaryOrbitalState(
            pHat,
            qHat,
            relativeApogee,
            relativePerigee,
            startAngleRad,
            massA,
            massB
        );

        bi1.SetBodyPosition(posA);
        bi1.SetBodyVelocity(velA);
        bi2.SetBodyPosition(posB);
        bi2.SetBodyVelocity(velB);
        dominantBodies.Add(bi1!.ToParams());
        dominantBodies.Add(bi2!.ToParams());
    }

    private void OnValidatePressed()
    {
        //CheckSystemStability();
    }

    private void LoadSystemTemplates()
    {
        var templateFiles = DirAccess.GetFilesAt("res://Configuration/SystemTemplate/");
        foreach (var file in templateFiles)
        {
            if (file.EndsWith(".yaml"))
            {
                var button = new Button();
                button.Text = file.Replace(".yaml", "");
                button.Pressed += () => ApplySystemTemplate(file);
                _templatesList!.AddChild(button);
            }
        }
    }

    private void ApplySystemTemplate(string fileName)
    {
        // Save previous loading state
        var previousLoadingState = _isLoadingTemplate;

        // Clear existing bodies from all lists
        ClearAllLists();

        // Load the 3-section template (dominant/belts/planetary)
        var templateData = TemplateHelpers.LoadSystemTemplate(fileName);
        GD.Print(
            $"Loaded template: {templateData.Dominant.Count} dominant, {templateData.Belts.Count} belts, {templateData.Planetary.Count} planetary"
        );

        // Set loading flag to prevent auto-balance during template load
        _isLoadingTemplate = true;

        // Pass 1: Dominant bodies
        foreach (var bodyDict in templateData.Dominant)
        {
            var typeStr = (string)bodyDict["type"];
            if (!Enum.TryParse<DominantBodyType>(typeStr, out _))
                continue;

            var template = (Godot.Collections.Dictionary)bodyDict["template"];
            var mass = (float)template["mass"];

            var bodyItem = AddDominantBodyFromTemplate(typeStr, bodyDict);
            if (bodyItem != null)
            {
                if (bodyItem.Mass != null)
                    bodyItem.Mass.Value = Mathf.Clamp(mass, 0f, 100000000f);
                bodyItem.SetSize(Size);
            }
        }

        // Update dominant body options so orbital center dropdowns are populated
        UpdateDominantBodyOptions();
        UpdateSharedOrbitalParamsVisibility();

        // Pass 2: Satellite belts
        foreach (var beltDict in templateData.Belts)
        {
            int orbitalCenterIndex = beltDict.ContainsKey("orbital_center_index")
                ? (int)beltDict["orbital_center_index"]
                : -1;
            AddSatelliteBeltFromTemplate(beltDict, orbitalCenterIndex);
        }

        // Pass 3: Planetary bodies
        foreach (var bodyDict in templateData.Planetary)
        {
            var typeStr = (string)bodyDict["type"];
            if (!Enum.TryParse<PlanetaryBodyType>(typeStr, out _))
                continue;

            var pTemplate = (Godot.Collections.Dictionary)bodyDict["template"];
            var pMass = (float)pTemplate["mass"];
            var pSize = Mathf.RoundToInt((float)pTemplate["size"]);

            var planetItem = AddPlanetaryBodyFromTemplate(typeStr, bodyDict);
            if (planetItem != null)
            {
                if (planetItem.Mass != null)
                    planetItem.Mass.Value = Mathf.Clamp(pMass, 0f, 100000000f);
                planetItem.SetSize(pSize);
            }
        }

        UpdateDominantBodyOptions();
        UpdateSectionCountLabels();

        // Restore previous loading state
        _isLoadingTemplate = previousLoadingState;
    }

    private DominantBodyItem? AddDominantBodyFromTemplate(string typeStr, Dictionary bodyDict)
    {
        if (_DominantBodyItemScene == null)
            return null;

        var template = (Dictionary)bodyDict["template"];
        var position = template.ContainsKey("position")
            ? (Vector3)template["position"]
            : Vector3.Zero;
        var velocity = template.ContainsKey("velocity")
            ? (Vector3)template["velocity"]
            : Vector3.Zero;

        var bodyItem = _DominantBodyItemScene.Instantiate<DominantBodyItem>();
        _dominantBodiesList!.AddChild(bodyItem);

        bodyItem.OnRemoveRequested += HandleDominantBodyRemove;
        bodyItem.ExpandMenu += ExpandMenu;
        bodyItem.MassSubmitted += OnMassSubmitted;

        // Set type by matching internal name stored in OptionButton metadata
        if (bodyItem.OptionButton != null)
        {
            if (PlanetaryTypeLoader.SelectByInternalName(bodyItem.OptionButton, typeStr))
            {
                var displayName = bodyItem.OptionButton.GetItemText(bodyItem.OptionButton.Selected);
                bodyItem.UpdateHeaderFromBodyType(displayName);
                bodyItem.SetConfiguration(bodyDict);
            }
        }

        return bodyItem;
    }

    private PlanetaryBodyItem? AddPlanetaryBodyFromTemplate(string typeStr, Dictionary bodyDict)
    {
        if (_PlanetaryBodyItemScene == null)
            return null;

        var bodyItem = _PlanetaryBodyItemScene.Instantiate<PlanetaryBodyItem>();
        _planetaryBodiesList!.AddChild(bodyItem);

        bodyItem.OnRemoveRequested += HandlePlanetaryBodyRemove;
        bodyItem.ItemUpdate += OnPlanetaryBodyItemUpdate;
        bodyItem.ExpandMenu += ExpandMenu;
        bodyItem.UpdateOrbitalCenterOptions(_dominantBodyOptions);

        // Set type by matching internal name stored in OptionButton metadata
        if (bodyItem.OptionButton != null)
        {
            if (PlanetaryTypeLoader.SelectByInternalName(bodyItem.OptionButton, typeStr))
            {
                var displayName = bodyItem.OptionButton.GetItemText(bodyItem.OptionButton.Selected);
                bodyItem.UpdateHeaderFromBodyType(displayName);
                bodyItem.SetConfiguration(bodyDict);
            }
        }

        // Handle orbital parameters if present
        if (bodyDict.ContainsKey("orbital_parameters"))
        {
            var orbitalParams = (Dictionary)bodyDict["orbital_parameters"];
            if (orbitalParams.ContainsKey("apogee"))
                bodyItem.SetApogee((float)orbitalParams["apogee"]);
            if (orbitalParams.ContainsKey("perigee"))
                bodyItem.SetPerigee((float)orbitalParams["perigee"]);
            if (orbitalParams.ContainsKey("starting_angle"))
                bodyItem.SetStartingAngle((float)orbitalParams["starting_angle"]);
            if (orbitalParams.ContainsKey("vertical_offset"))
                bodyItem.SetVerticalOffset((float)orbitalParams["vertical_offset"]);
            if (orbitalParams.ContainsKey("orbital_center_index"))
                bodyItem.SetOrbitalCenterIndex((int)orbitalParams["orbital_center_index"]);
        }

        return bodyItem;
    }

    private void ClearAllLists()
    {
        // Clear dominant bodies
        foreach (Node child in _dominantBodiesList!.GetChildren())
        {
            child.QueueFree();
        }

        // Clear satellite belts
        foreach (Node child in _satelliteBeltsList!.GetChildren())
        {
            child.QueueFree();
        }

        // Clear planetary bodies
        foreach (Node child in _planetaryBodiesList!.GetChildren())
        {
            child.QueueFree();
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

                Vector3 pos1 = body1.ContainsKey("position")
                    ? body1["position"].AsVector3()
                    : Vector3.Zero;
                Vector3 pos2 = body2.ContainsKey("position")
                    ? body2["position"].AsVector3()
                    : Vector3.Zero;

                // Get sizes (radii) from mesh parameters
                float size1 = 1.0f;
                float size2 = 1.0f;

                if (
                    body1.ContainsKey("mesh")
                    && body1["mesh"].AsGodotDictionary().ContainsKey("size")
                )
                {
                    size1 = body1["mesh"].AsGodotDictionary()["size"].AsSingle();
                }

                if (
                    body2.ContainsKey("mesh")
                    && body2["mesh"].AsGodotDictionary().ContainsKey("size")
                )
                {
                    size2 = body2["mesh"].AsGodotDictionary()["size"].AsSingle();
                }

                float distance = pos1.DistanceTo(pos2);
                float minDistance = (size1 + size2) * 0.5f;

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

    private void OnSavePressed()
    {
        ShowFileNameDialog();
    }

    private void ShowFileNameDialog()
    {
        var dialog = new AcceptDialog();
        dialog.Title = "Save System Configuration";

        var vbox = new VBoxContainer();
        var label = new Label();
        label.Text = "Enter filename for the system configuration:";
        vbox.AddChild(label);

        var lineEdit = new LineEdit();
        lineEdit.PlaceholderText = "MySystem";
        lineEdit.CustomMinimumSize = new Vector2(300, 0);
        vbox.AddChild(lineEdit);

        dialog.AddChild(vbox);

        dialog.Connect(
            AcceptDialog.SignalName.Confirmed,
            Callable.From(() => OnSaveDialogConfirmed(dialog, lineEdit))
        );
        dialog.Connect(
            AcceptDialog.SignalName.Canceled,
            Callable.From(() => OnSaveDialogCanceled(dialog))
        );

        AddChild(dialog);
        dialog.PopupCentered();

        lineEdit.GrabFocus();
        lineEdit.CallDeferred(LineEdit.MethodName.SelectAll);
    }

    private void OnSaveDialogConfirmed(AcceptDialog dialog, LineEdit lineEdit)
    {
        string fileName = lineEdit.Text.Trim();
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = "UntitledSystem";
        }

        if (!fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".yaml";
        }

        SaveSystemToFile(fileName);
        dialog.QueueFree();
    }

    private void OnSaveDialogCanceled(AcceptDialog dialog)
    {
        dialog.QueueFree();
    }

    private void SaveSystemToFile(string fileName)
    {
        try
        {
            var dominant = new Array<Dictionary>();
            var belts = new Array<Dictionary>();
            var planetary = new Array<Dictionary>();

            // Collect dominant bodies
            foreach (Node child in _dominantBodiesList!.GetChildren())
            {
                if (child is DominantBodyItem bi)
                    dominant.Add(bi.ToParams());
            }

            // Collect planetary bodies
            foreach (Node child in _planetaryBodiesList!.GetChildren())
            {
                if (child is PlanetaryBodyItem bi)
                    planetary.Add(bi.ToParams());
            }

            // Collect satellite belts
            foreach (Node child in _satelliteBeltsList!.GetChildren())
            {
                if (child is SatelliteBeltItem sbi)
                    belts.Add(sbi.ToParams());
            }

            string yamlContent = TemplateHelpers.GenerateYamlContent(dominant, belts, planetary);

            string filePath = $"res://Configuration/SystemTemplate/{fileName}";

            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PrintErr($"Failed to create file: {filePath}");
                ShowErrorDialog($"Failed to save file: {fileName}");
                return;
            }

            file.StoreString(yamlContent);
            file.Close();

            GD.Print($"System configuration saved to: {filePath}");
            ShowSuccessDialog($"System configuration saved as: {fileName}");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"Error saving system: {ex.Message}\n{ex.StackTrace}");
            ShowErrorDialog($"Error saving system: {ex.Message}");
        }
    }

    private void ShowSuccessDialog(string message)
    {
        var dialog = new AcceptDialog();
        dialog.Title = "Success";
        dialog.DialogText = message;
        AddChild(dialog);
        dialog.PopupCentered();
        dialog.Connect(
            AcceptDialog.SignalName.Confirmed,
            new Callable(dialog, Node.MethodName.QueueFree)
        );
    }

    private void ShowErrorDialog(string message)
    {
        var dialog = new AcceptDialog();
        dialog.Title = "Error";
        dialog.DialogText = message;
        AddChild(dialog);
        dialog.PopupCentered();
        dialog.Connect(
            AcceptDialog.SignalName.Confirmed,
            new Callable(dialog, Node.MethodName.QueueFree)
        );
    }
    #endregion
    #endregion
}
