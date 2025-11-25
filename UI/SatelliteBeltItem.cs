using System;
using Godot;
using Structures.Enums;
using UtilityLibrary;

namespace UI;

public partial class SatelliteBeltItem : VBoxContainer
{
    [Signal]
    public delegate void ItemUpdateEventHandler();

    [Export]
    public Button Toggle;

    [Export]
    public Button RemoveItem;

    [Export]
    public OptionButton OptionButton;

    [Export] public SpinBox Apogee;
    [Export] public SpinBox Perigee;
    //[Export]
    //public SpinBox RingDistanceX;

    //[Export]
    //public SpinBox RingDistanceY;

    //[Export]
    //public SpinBox RingDistanceZ;

    [Export]
    public SpinBox RingVelocityX;

    [Export]
    public SpinBox RingVelocityY;

    [Export]
    public SpinBox RingVelocityZ;

    [Export]
    public SpinBox MinMass,
        MaxMass;

    [Export]
    public SpinBox MinSize,
        MaxSize;

    [Export]
    public SpinBox NumInBeltLower,
        NumInBeltUpper;

    [Export]
    public OptionButton BeltGrouping;

    private HBoxContainer BeltNumContainer;
    private Godot.Collections.Dictionary templateDict = new Godot.Collections.Dictionary();

    public Action<SatelliteBeltItem> OnRemoveRequested;

    private CelestialBodyType parentType;
    private const float Limit = 10000f; // constrain within ±10,000 units
    private const float MassLimit = 10000f; // constrain mass 0..10,000
    private const float SizeLimit = 10000f; // constrain size 0..10,000

    public void SetParentType(CelestialBodyType type)
    {
        this.parentType = type;
    }

    public override void _EnterTree()
    {
        // Cache field nodes if not set via exported references
        OptionButton ??= GetNodeOrNull<OptionButton>("Content/TypeContent/OptionButton");
        Apogee ??= GetNodeOrNull<SpinBox>("Content/PositionContent/Apogee");
        Perigee ??= GetNodeOrNull<SpinBox>("Content/PositionContent/Perigee");
        RingVelocityX ??= GetNodeOrNull<SpinBox>("Content/VelocityContent/VelocityPositionX");
        RingVelocityY ??= GetNodeOrNull<SpinBox>("Content/VelocityContent/VelocityPositionY");
        RingVelocityZ ??= GetNodeOrNull<SpinBox>("Content/VelocityContent/VelocityPositionZ");
        MinMass ??= GetNodeOrNull<SpinBox>("Content/MassMinContent/MinMass");
        MaxMass ??= GetNodeOrNull<SpinBox>("Content/MassMaxContent/MaxMass");
        MinSize ??= GetNodeOrNull<SpinBox>("Content/SizeMinContent/MinSize");
        MaxSize ??= GetNodeOrNull<SpinBox>("Content/SizeMaxContent/MaxSize");
        NumInBeltLower ??= GetNodeOrNull<SpinBox>("Content/BeltContent/NumInBeltLower");
        NumInBeltUpper ??= GetNodeOrNull<SpinBox>("Content/BeltContent/NumInBeltUpper");
        BeltNumContainer ??= GetNodeOrNull<HBoxContainer>("Content/BeltContent");
        BeltGrouping ??= GetNodeOrNull<OptionButton>("Content/BeltGrouping");

        // Apply input constraints to fields
        ApplyConstraints();
    }

    public void SubscribeEvents()
    {
        // Hook remove
        var remove = GetNodeOrNull<Button>("Header/RemoveItem");
        if (remove != null)
        {
            remove.Pressed += () => OnRemoveRequested?.Invoke(this);
        }
        // Hook up input events for updates
        var spinBoxes = new[]
        {
            Apogee,
            Perigee,
            RingVelocityX,
            RingVelocityY,
            RingVelocityZ,
            MinMass,
            MaxMass,
            MinSize,
            MaxSize,
            NumInBeltLower,
            NumInBeltUpper,
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
            foreach (var name in System.Enum.GetNames(typeof(SatelliteGroupTypes)))
                OptionButton.AddItem(name);

            OptionButton.ItemSelected += idx =>
            {
                var type = (SatelliteGroupTypes)(int)idx;
                ApplyTemplate(type);
                UpdateHeaderFromType(OptionButton.GetItemText((int)idx));
                EmitSignal(SignalName.ItemUpdate);
            };

            // Ensure a valid initial selection
            if (OptionButton.ItemCount > 0)
            {
                if (OptionButton.Selected < 0)
                    OptionButton.Select(0);
                ApplyTemplate((SatelliteGroupTypes)OptionButton.Selected);
                UpdateHeaderFromType(OptionButton.GetItemText(OptionButton.Selected));
            }
        }

        if (BeltGrouping != null)
        {
            BeltGrouping.Clear();
            foreach (var name in System.Enum.GetNames(typeof(GroupingCategories)))
                BeltGrouping.AddItem(name);
        }
    }

    public void ApplyTemplate(SatelliteGroupTypes type)
    {
        // Read defaults from TOML in Configuration/SystemGen with safe fallbacks
        var t = SystemGenTemplates.GetSatelliteGroupDefaults(type);
        t = (Godot.Collections.Dictionary)t["template"];
        GD.Print($"Template: {t}");

        // Assign to UI (already clamped by GetDefaults, but clamp again defensively)
        var apogee = (float)t["ring_apogee"];
        var perigee = (float)t["ring_perigee"];
        var velocity = (Vector3)t["ring_velocity"];
        var lowerRange = (int)t["lower_range"];
        var upperRange = (int)t["upper_range"];
        var grouping = (String)t["grouping"];

        if (Apogee != null)
        {
            Apogee.Value = Mathf.Clamp((float)apogee, 0f, Limit);
        }
        if (Perigee != null)
        {
            Perigee.Value = Mathf.Clamp((float)perigee, 0f, Limit);
        }
        if (RingVelocityX != null)
        {
            RingVelocityX.Value = Mathf.Clamp((float)velocity.X, 0f, Limit);
        }
        if (RingVelocityY != null)
        {
            RingVelocityY.Value = Mathf.Clamp((float)velocity.Y, 0f, Limit);
        }
        if (RingVelocityZ != null)
        {
            RingVelocityZ.Value = Mathf.Clamp((float)velocity.Z, 0f, Limit);
        }
        if (MinMass != null)
            MinMass.Value = Mathf.Clamp((float)t["mass_min"], 0f, MassLimit);
        if (MaxMass != null)
            MaxMass.Value = Mathf.Clamp((float)t["mass_max"], 0f, MassLimit);
        if (MinSize != null)
            MinSize.Value = Mathf.Clamp((float)t["size_min"], 0f, SizeLimit);
        if (MaxSize != null)
            MaxSize.Value = Mathf.Clamp((float)t["size_max"], 0f, SizeLimit);
        if (NumInBeltLower != null)
        {
            NumInBeltLower.Value = lowerRange;
        }
        if (NumInBeltUpper != null)
        {
            NumInBeltUpper.Value = upperRange;
        }
        if (BeltGrouping != null)
        {
            BeltGrouping.Clear();
            foreach (var name in System.Enum.GetNames(typeof(GroupingCategories)))
                BeltGrouping.AddItem(name);
        }
        templateDict = t;
    }

    public void SetTemplate(Godot.Collections.Dictionary t)
    {
        var template = (Godot.Collections.Dictionary)t["template"];
        var apogee = (float)template["ring_apogee"];
        var perigee = (float)template["ring_perigee"];
        var velocity = (Vector3)template["ring_velocity"];
        var lowerRange = (int)template["lower_range"];
        var upperRange = (int)template["upper_range"];
        var grouping = (String)template["grouping"];

        if (Apogee != null)
        {
            Apogee.Value = Mathf.Clamp((float)apogee, 0f, Limit);
        }
        if (Perigee != null)
        {
            Perigee.Value = Mathf.Clamp((float)perigee, 0f, Limit);
        }
        if (RingVelocityX != null)
        {
            RingVelocityX.Value = Mathf.Clamp((float)velocity.X, 0f, Limit);
        }
        if (RingVelocityY != null)
        {
            RingVelocityY.Value = Mathf.Clamp((float)velocity.Y, 0f, Limit);
        }
        if (RingVelocityZ != null)
        {
            RingVelocityZ.Value = Mathf.Clamp((float)velocity.Z, 0f, Limit);
        }
        if (MinMass != null)
            MinMass.Value = Mathf.Clamp((float)template["mass_min"], 0f, MassLimit);
        if (MaxMass != null)
            MaxMass.Value = Mathf.Clamp((float)template["mass_max"], 0f, MassLimit);
        if (MinSize != null)
            MinSize.Value = Mathf.Clamp((float)template["size_min"], 0f, SizeLimit);
        if (MaxSize != null)
            MaxSize.Value = Mathf.Clamp((float)template["size_max"], 0f, SizeLimit);
        if (NumInBeltLower != null)
        {
            NumInBeltLower.Value = lowerRange;
        }
        if (NumInBeltUpper != null)
        {
            NumInBeltUpper.Value = upperRange;
        }
        if (BeltGrouping != null)
        {
            if (System.Enum.TryParse<GroupingCategories>(grouping, false, out var group))
                BeltGrouping.Select((int)group);
        }

    }

    private void ApplyConstraints()
    {
        // Positions and velocities: clamp to [-Limit, Limit]
        foreach (
            var sb in new[]
            {
                Apogee,
                Perigee,
                RingVelocityX,
                RingVelocityY,
                RingVelocityZ,
            }
        )
        {
            if (sb == null)
                continue;
            sb.MinValue = -Limit;
            sb.MaxValue = Limit;
            sb.AllowGreater = false;
            sb.AllowLesser = false;
        }
        // Mass: [0, MassLimit]
        foreach (var sb in new[] { MinMass, MaxMass })
        {
            if (sb == null)
                continue;
            sb.MinValue = 0.0;
            sb.MaxValue = MassLimit;
            sb.AllowGreater = false;
            sb.AllowLesser = false;
        }
        // Size: [0, SizeLimit]
        foreach (var sb in new[] { MinSize, MaxSize })
        {
            if (sb == null)
                continue;
            sb.MinValue = 0.0;
            sb.MaxValue = SizeLimit;
            sb.AllowGreater = false;
            sb.AllowLesser = false;
        }
    }

    public void UpdateHeaderFromType(string typeName)
    {
        var headerBtn = GetNodeOrNull<Button>("Header/Toggle");
        if (headerBtn != null)
        {
            headerBtn.Text = typeName;
        }
    }

    public float GetRingApogee()
    {
        return Mathf.Clamp((float)Apogee.Value, -Limit, Limit);
    }

    public float GetRingPerigee()
    {
        return Mathf.Clamp((float)Perigee.Value, -Limit, Limit);
    }

    public Vector3 GetRingVelocity()
    {
        float vx = Mathf.Clamp((float)RingVelocityX.Value, -Limit, Limit);
        float vy = Mathf.Clamp((float)RingVelocityY.Value, -Limit, Limit);
        float vz = Mathf.Clamp((float)RingVelocityZ.Value, -Limit, Limit);
        return new Vector3(vx, vy, vz);
    }

    public float GetMassMin()
    {
        return Mathf.Clamp((float)MinMass.Value, 0f, MassLimit);
    }

    public float GetMassMax()
    {
        return Mathf.Clamp((float)MaxMass.Value, 0f, MassLimit);
    }

    public float GetSizeMin()
    {
        return Mathf.Clamp((float)MinSize.Value, 0f, SizeLimit);
    }

    public float GetSizeMax()
    {
        return Mathf.Clamp((float)MaxSize.Value, 0f, SizeLimit);
    }

    public string GetSatelliteType()
    {
        if (OptionButton != null && OptionButton.Selected >= 0)
        {
            return OptionButton.GetItemText(OptionButton.Selected);
        }
        return "Asteroid";
    }

    public (int, int) GetNumberInBelt()
    {
        if (NumInBeltLower != null && NumInBeltUpper != null)
        {
            int lowerRange = Mathf.RoundToInt(NumInBeltLower.Value);
            int upperRange = Mathf.RoundToInt(NumInBeltUpper.Value);
            return (lowerRange, upperRange);
        }
        return (25, 75);
    }

    public String GetBeltGrouping()
    {
        if (BeltGrouping != null && BeltGrouping.Selected >= 0)
        {
            return BeltGrouping.GetItemText(BeltGrouping.Selected);
        }
        return "Balanced";
    }

    public Godot.Collections.Dictionary ToParams()
    {
        Godot.Collections.Dictionary dict = new Godot.Collections.Dictionary();
        dict.Add("type", GetSatelliteType());
        dict.Add("ring_apogee", GetRingApogee());
        dict.Add("ring_perigee", GetRingPerigee());
        dict.Add("ring_velocity", GetRingVelocity());
        dict.Add("size_min", GetSizeMin());
        dict.Add("size_max", GetSizeMax());
        dict.Add("mass_min", GetMassMin());
        dict.Add("mass_max", GetMassMax());
        var numRange = GetNumberInBelt();
        dict.Add("lower_range", numRange.Item1);
        dict.Add("upper_range", numRange.Item2);
        dict.Add("grouping", GetBeltGrouping());
        return dict;
    }
}
