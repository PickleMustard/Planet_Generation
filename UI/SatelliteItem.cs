using System;
using Godot;
using PlanetGeneration;
using UtilityLibrary;

namespace UI;

public partial class SatelliteItem : VBoxContainer
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

    [Export]
    public SpinBox numInBeltLower;

    [Export]
    public SpinBox numInBeltUpper;

    private HBoxContainer beltNumContainer;
    private Godot.Collections.Dictionary templateDict = new Godot.Collections.Dictionary();

    public Action<SatelliteItem> OnRemoveRequested;

    private CelestialBodyType parentType;
    private int NumberInBelt = 25;
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
        X ??= GetNodeOrNull<SpinBox>("Content/PositionContent/X");
        Y ??= GetNodeOrNull<SpinBox>("Content/PositionContent/Y");
        Z ??= GetNodeOrNull<SpinBox>("Content/PositionContent/Z");
        velX ??= GetNodeOrNull<SpinBox>("Content/VelocityContent/velX");
        velY ??= GetNodeOrNull<SpinBox>("Content/VelocityContent/velY");
        velZ ??= GetNodeOrNull<SpinBox>("Content/VelocityContent/velZ");
        mass ??= GetNodeOrNull<SpinBox>("Content/MassContent/mass");
        size ??= GetNodeOrNull<SpinBox>("Content/SizeContent/size");
        numInBeltLower ??= GetNodeOrNull<SpinBox>("Content/BeltContent/beltNumLower");
        numInBeltUpper ??= GetNodeOrNull<SpinBox>("Content/BeltContent/beltNumUpper");
        beltNumContainer ??= GetNodeOrNull<HBoxContainer>("Content/BeltContent");

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
            X,
            Y,
            Z,
            velX,
            velY,
            velZ,
            mass,
            size,
            numInBeltLower,
            numInBeltUpper,
        };
        foreach (var sb in spinBoxes)
        {
            if (sb != null)
            {
                sb.ValueChanged += value => EmitSignal(SignalName.ItemUpdate);
            }
        }

        if (beltNumContainer != null)
        {
            if (parentType == CelestialBodyType.Star || parentType == CelestialBodyType.BlackHole)
            {
                beltNumContainer.Visible = true;
            }
            else
            {
                beltNumContainer.Visible = false;
            }
        }

        // Populate satellite types and hook selection
        if (OptionButton != null)
        {
            OptionButton.Clear();
            if (parentType == CelestialBodyType.Star || parentType == CelestialBodyType.BlackHole)
            {
                foreach (var name in System.Enum.GetNames(typeof(SatelliteGroupTypes)))
                    OptionButton.AddItem(name);
            }
            else
            {
                foreach (var name in System.Enum.GetNames(typeof(SatelliteBodyType)))
                    OptionButton.AddItem(name);
            }

            OptionButton.ItemSelected += idx =>
            {
                UpdateHeaderFromType(OptionButton.GetItemText((int)idx));
                EmitSignal(SignalName.ItemUpdate);
            };

            // Ensure a valid initial selection
            if (OptionButton.ItemCount > 0)
            {
                if (OptionButton.Selected < 0)
                    OptionButton.Select(0);
                UpdateHeaderFromType(OptionButton.GetItemText(OptionButton.Selected));
            }
        }
    }

    public void ApplyTemplate(CelestialBodyType type)
    {
        // Read defaults from TOML in Configuration/SystemGen with safe fallbacks
        var t = SystemGenTemplates.GetCelestialBodyDefaults(type);

        // Assign to UI (already clamped by GetDefaults, but clamp again defensively)
        var position = (Vector3)t["Position"];
        var velocity = (Vector3)t["Velocity"];
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
            mass.Value = Mathf.Clamp((float)t["Mass"], 0f, MassLimit);
        if (size != null)
            size.Value = Mathf.Clamp((float)t["Size"], 0f, SizeLimit);
        templateDict = t;
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
        var headerBtn = GetNodeOrNull<Button>("Header/Toggle");
        if (headerBtn != null)
        {
            headerBtn.Text = typeName;
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

    public (int, int) GetNumberInBelt()
    {
        if (numInBeltLower != null && numInBeltUpper != null)
        {
            int lowerRange = Mathf.RoundToInt(numInBeltLower.Value);
            int upperRange = Mathf.RoundToInt(numInBeltUpper.Value);
            return (lowerRange, upperRange);
        }
        return (25, 25);
    }

    public Godot.Collections.Dictionary ToParams()
    {
        Godot.Collections.Dictionary dict = new Godot.Collections.Dictionary();
        dict.Add("Type", GetSatelliteType());
        Godot.Collections.Dictionary templateDict = new Godot.Collections.Dictionary();
        templateDict.Add("BasePosition", GetPosition());
        templateDict.Add("SatelliteVelocity", GetVelocity());
        templateDict.Add("Size", GetSize());
        templateDict.Add("Mass", GetMass());
        dict.Add("Template", templateDict);
        Godot.Collections.Dictionary meshDict = new Godot.Collections.Dictionary();
        //meshDict.Add("Subdivisions");
        return dict;
    }
}
