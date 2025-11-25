using Godot;

namespace UI;

public partial class DetailPanel : VBoxContainer
{
    [Signal]
    public delegate void ValueChangedEventHandler();

    // Base Mesh Controls
    [Export] public VBoxContainer BaseMeshSection;
    [Export] public SpinBox SubdivisionsSpinBox;

    // Tectonics Controls (BodyItem only)
    [Export] public VBoxContainer TectonicsSection;

    // Scaling Controls (SatelliteItem only)
    [Export] public VBoxContainer ScalingSection;

    // Noise Controls (SatelliteItem only)
    [Export] public VBoxContainer NoiseSection;

    private PackedScene _rangeControlScene;
    private bool _isForBodyItem = true;

    public override void _EnterTree()
    {
        _rangeControlScene = GD.Load<PackedScene>("res://UI/RangeControl.tscn");

        // Cache nodes
        BaseMeshSection ??= GetNodeOrNull<VBoxContainer>("BaseMeshSection");
        SubdivisionsSpinBox ??= GetNodeOrNull<SpinBox>("BaseMeshSection/SubdivisionsControl/SubdivisionsSpinBox");

        // Connect subdivisions change
        if (SubdivisionsSpinBox != null)
        {
            SubdivisionsSpinBox.ValueChanged += OnSubdivisionsChanged;
        }
    }

    public void SetupForBodyItem()
    {
        _isForBodyItem = true;
        SetupBaseMeshControls();
        SetupTectonicsControls();

        // Hide satellite-specific sections
        if (ScalingSection != null) ScalingSection.Visible = false;
        if (NoiseSection != null) NoiseSection.Visible = false;
    }

    public void SetupForSatelliteItem()
    {
        _isForBodyItem = false;
        SetupBaseMeshControls();
        SetupScalingControls();
        SetupNoiseControls();

        // Hide body-specific sections
        if (TectonicsSection != null) TectonicsSection.Visible = false;
    }

    private void SetupBaseMeshControls()
    {
        // Subdivisions already set up in scene
        // Add additional base mesh controls
        if (BaseMeshSection != null)
        {
            // Add vertices per edge controls
            AddRangeControl(BaseMeshSection, "Vertices Per Edge", 3, 20, 1, 3, 8);

            // Add aberrations control
            AddSingleControl(BaseMeshSection, "Aberrations", 0, 10, 1, 2);

            // Add deformation cycles control
            AddSingleControl(BaseMeshSection, "Deformation Cycles", 0, 10, 1, 3);
        }
    }

    private void AddSingleControl(VBoxContainer parent, string label, float minVal, float maxVal, float step, float defaultValue)
    {
        var container = new HBoxContainer();

        var labelNode = new Label();
        labelNode.Text = label;
        labelNode.CustomMinimumSize = new Vector2(120, 0);
        container.AddChild(labelNode);

        var spinBox = new SpinBox();
        spinBox.MinValue = minVal;
        spinBox.MaxValue = maxVal;
        spinBox.Step = step;
        spinBox.Value = defaultValue;
        spinBox.ValueChanged += (value) => EmitSignal(SignalName.ValueChanged);
        container.AddChild(spinBox);

        parent.AddChild(container);
    }

    private void SetupTectonicsControls()
    {
        if (_isForBodyItem && BaseMeshSection != null)
        {
            // Create tectonics section if it doesn't exist
            if (TectonicsSection == null)
            {
                TectonicsSection = new VBoxContainer();
                TectonicsSection.Name = "TectonicsSection";
                AddChild(TectonicsSection);

                var header = new Label();
                header.Text = "Tectonics";
                TectonicsSection.AddChild(header);

                // Add tectonics controls here
                AddTectonicsControl(TectonicsSection, "Continents", 1, 20, 1, 1, 7);
                AddTectonicsControl(TectonicsSection, "Stress Scale", 0.1f, 10.0f, 0.1f, 0.5f, 2.0f);
                AddTectonicsControl(TectonicsSection, "Shear Scale", 0.1f, 10.0f, 0.1f, 0.5f, 2.0f);
                // Add more tectonics controls as needed
            }
            TectonicsSection.Visible = true;
        }
    }

    private void SetupScalingControls()
    {
        if (!_isForBodyItem && BaseMeshSection != null)
        {
            // Create scaling section if it doesn't exist
            if (ScalingSection == null)
            {
                ScalingSection = new VBoxContainer();
                ScalingSection.Name = "ScalingSection";
                AddChild(ScalingSection);

                var header = new Label();
                header.Text = "Scaling";
                ScalingSection.AddChild(header);

                // Add scaling controls
                AddRangeControl(ScalingSection, "X Scale", 0.1f, 5.0f, 0.1f, 0.8f, 1.2f);
                AddRangeControl(ScalingSection, "Y Scale", 0.1f, 5.0f, 0.1f, 0.8f, 1.2f);
                AddRangeControl(ScalingSection, "Z Scale", 0.1f, 5.0f, 0.1f, 0.8f, 1.2f);
            }
            ScalingSection.Visible = true;
        }
    }

    private void SetupNoiseControls()
    {
        if (!_isForBodyItem && BaseMeshSection != null)
        {
            // Create noise section if it doesn't exist
            if (NoiseSection == null)
            {
                NoiseSection = new VBoxContainer();
                NoiseSection.Name = "NoiseSection";
                AddChild(NoiseSection);

                var header = new Label();
                header.Text = "Noise Settings";
                NoiseSection.AddChild(header);

                // Add noise controls
                AddRangeControl(NoiseSection, "Amplitude", 0.01f, 2.0f, 0.01f, 0.1f, 0.5f);
                AddRangeControl(NoiseSection, "Scaling", 0.1f, 10.0f, 0.1f, 1.0f, 3.0f);
                AddRangeControl(NoiseSection, "Octaves", 1, 8, 1, 1, 4);
            }
            NoiseSection.Visible = true;
        }
    }

    private void AddTectonicsControl(VBoxContainer parent, string label, float minVal, float maxVal, float step, float defaultLower, float defaultUpper)
    {
        var rangeControl = _rangeControlScene.Instantiate<RangeControl>();
        rangeControl.Setup(label, minVal, maxVal, step, defaultLower, defaultUpper);
        rangeControl.ValueChanged += OnRangeValueChanged;
        parent.AddChild(rangeControl);
    }

    private void AddRangeControl(VBoxContainer parent, string label, float minVal, float maxVal, float step, float defaultLower, float defaultUpper)
    {
        var rangeControl = _rangeControlScene.Instantiate<RangeControl>();
        rangeControl.Setup(label, minVal, maxVal, step, defaultLower, defaultUpper);
        rangeControl.ValueChanged += OnRangeValueChanged;
        parent.AddChild(rangeControl);
    }

    private void OnSubdivisionsChanged(double value)
    {
        EmitSignal(SignalName.ValueChanged);
    }

    private void OnRangeValueChanged(float lowerValue, float upperValue)
    {
        EmitSignal(SignalName.ValueChanged);
    }

    // Methods to get/set values
    public void SetSubdivisions(int value)
    {
        if (SubdivisionsSpinBox != null)
        {
            SubdivisionsSpinBox.Value = Mathf.Clamp(value, 1, 10);
        }
    }

    public int GetSubdivisions()
    {
        return SubdivisionsSpinBox != null ? (int)SubdivisionsSpinBox.Value : 1;
    }

    public void SetVerticesPerEdge(int[,] verticesPerEdge)
    {
        // Find the vertices per edge range control and update it
        var rangeControls = FindRangeControlsByLabel("Vertices Per Edge");
        if (rangeControls.Count > 0 && verticesPerEdge != null)
        {
            // For now, use the first row values
            rangeControls[0].SetValues(verticesPerEdge[0, 0], verticesPerEdge[0, 1]);
        }
    }

    public int[,] GetVerticesPerEdge(int subdivisions)
    {
        var result = new int[subdivisions, 2];
        var rangeControls = FindRangeControlsByLabel("Vertices Per Edge");
        if (rangeControls.Count > 0)
        {
            var values = rangeControls[0].GetValues();
            result[0, 0] = (int)values.Item1;
            result[0, 1] = (int)values.Item2;
        }
        return result;
    }

    public void SetAberrations(int value)
    {
        var spinBox = FindSpinBoxByLabel("Aberrations");
        if (spinBox != null)
        {
            spinBox.Value = Mathf.Clamp(value, 0, 10);
        }
    }

    public int GetAberrations()
    {
        var spinBox = FindSpinBoxByLabel("Aberrations");
        return spinBox != null ? (int)spinBox.Value : 0;
    }

    public void SetDeformationCycles(int value)
    {
        var spinBox = FindSpinBoxByLabel("Deformation Cycles");
        if (spinBox != null)
        {
            spinBox.Value = Mathf.Clamp(value, 0, 10);
        }
    }

    public int GetDeformationCycles()
    {
        var spinBox = FindSpinBoxByLabel("Deformation Cycles");
        return spinBox != null ? (int)spinBox.Value : 0;
    }

    public void SetTectonicsValues(string label, float[] values)
    {
        var rangeControls = FindRangeControlsByLabel(label);
        if (rangeControls.Count > 0 && values != null && values.Length >= 2)
        {
            rangeControls[0].SetValues(values[0], values[1]);
        }
    }

    public float[] GetTectonicsValues(string label)
    {
        var rangeControls = FindRangeControlsByLabel(label);
        if (rangeControls.Count > 0)
        {
            var values = rangeControls[0].GetValues();
            return new float[] { values.Item1, values.Item2 };
        }
        return new float[] { 0f, 0f };
    }

    private Godot.Collections.Array<RangeControl> FindRangeControlsByLabel(string label)
    {
        var result = new Godot.Collections.Array<RangeControl>();

        if (BaseMeshSection != null)
            FindRangeControlsInContainer(BaseMeshSection, label, result);
        if (TectonicsSection != null)
            FindRangeControlsInContainer(TectonicsSection, label, result);
        if (ScalingSection != null)
            FindRangeControlsInContainer(ScalingSection, label, result);
        if (NoiseSection != null)
            FindRangeControlsInContainer(NoiseSection, label, result);

        return result;
    }

    private void FindRangeControlsInContainer(Container container, string label, Godot.Collections.Array<RangeControl> result)
    {
        foreach (Node child in container.GetChildren())
        {
            if (child is RangeControl rangeControl && HasLabel(rangeControl, label))
            {
                result.Add(rangeControl);
            }
            else if (child is Container childContainer)
            {
                FindRangeControlsInContainer(childContainer, label, result);
            }
        }
    }

    private bool HasLabel(RangeControl rangeControl, string label)
    {
        if (rangeControl.ParameterLabel != null)
        {
            return rangeControl.ParameterLabel.Text == label;
        }
        return false;
    }

    private SpinBox FindSpinBoxByLabel(string label)
    {
        SpinBox result = null;

        if (BaseMeshSection != null)
            result = FindSpinBoxInContainer(BaseMeshSection, label);
        if (result == null && TectonicsSection != null)
            result = FindSpinBoxInContainer(TectonicsSection, label);
        if (result == null && ScalingSection != null)
            result = FindSpinBoxInContainer(ScalingSection, label);
        if (result == null && NoiseSection != null)
            result = FindSpinBoxInContainer(NoiseSection, label);

        return result;
    }

    private SpinBox FindSpinBoxInContainer(Container container, string label)
    {
        foreach (Node child in container.GetChildren())
        {
            if (child is HBoxContainer hbox)
            {
                var labelNode = GetFirstChild<Label>(hbox);
                var spinBox = GetFirstChild<SpinBox>(hbox);
                if (labelNode != null && spinBox != null && labelNode.Text == label)
                {
                    return spinBox;
                }
            }
            else if (child is Container childContainer)
            {
                var result = FindSpinBoxInContainer(childContainer, label);
                if (result != null)
                    return result;
            }
        }
        return null;
    }

    private T GetFirstChild<T>(Container container) where T : Node
    {
        foreach (Node child in container.GetChildren())
        {
            if (child is T typedChild)
            {
                return typedChild;
            }
        }
        return null;
    }

    // Additional methods for satellite-specific controls
    public void SetScalingValues(string label, float[] values)
    {
        var rangeControls = FindRangeControlsByLabel(label);
        if (rangeControls.Count > 0 && values != null && values.Length >= 2)
        {
            rangeControls[0].SetValues(values[0], values[1]);
        }
    }

    public float[] GetScalingValues(string label)
    {
        var rangeControls = FindRangeControlsByLabel(label);
        if (rangeControls.Count > 0)
        {
            var values = rangeControls[0].GetValues();
            return new float[] { values.Item1, values.Item2 };
        }
        return new float[] { 0f, 0f };
    }

    public void SetNoiseValues(string label, float[] values)
    {
        var rangeControls = FindRangeControlsByLabel(label);
        if (rangeControls.Count > 0 && values != null && values.Length >= 2)
        {
            rangeControls[0].SetValues(values[0], values[1]);
        }
    }

    public float[] GetNoiseValues(string label)
    {
        var rangeControls = FindRangeControlsByLabel(label);
        if (rangeControls.Count > 0)
        {
            var values = rangeControls[0].GetValues();
            return new float[] { values.Item1, values.Item2 };
        }
        return new float[] { 0f, 0f };
    }
}
