using Godot;
using System;
using PlanetGeneration;

public partial class BodyItem : VBoxContainer
{
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

    public Action<BodyItem> OnRemoveRequested;

    private const float Limit = 10000f; // constrain within ±10,000 units (mass 0..10,000)

    public override void _Ready()
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
                UpdateHeaderFromBodyType(OptionButton.GetItemText((int)idx));
                ApplyTemplate(type);
            };

            // Ensure a valid initial selection and template
            if (OptionButton.ItemCount > 0)
            {
                if (OptionButton.Selected < 0) OptionButton.Select(0);
                UpdateHeaderFromBodyType(OptionButton.GetItemText(OptionButton.Selected));
                ApplyTemplate((CelestialBodyType)OptionButton.Selected);
            }
        }
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
            mass.MaxValue = Limit;
            mass.AllowGreater = false;
            mass.AllowLesser = false;
        }
    }

    private void ApplyTemplate(CelestialBodyType type)
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

        if (mass != null) mass.Value = Mathf.Clamp(t.Mass, 0f, Limit);
    }

    public void UpdateHeaderFromBodyType(string typeName)
    {
        var headerBtn = GetNodeOrNull<Button>("Header/Toggle");
        if (headerBtn != null)
        {
            headerBtn.Text = typeName;
        }
    }

    public CelestialBodyParams ToParams()
    {
        var ob = GetNode<OptionButton>("Content/BodyTypeContent/OptionButton");
        // Clamp outgoing values as a final safeguard
        float cx = Mathf.Clamp((float)GetNode<SpinBox>("Content/PositionContent/X").Value, -Limit, Limit);
        float cy = Mathf.Clamp((float)GetNode<SpinBox>("Content/PositionContent/Y").Value, -Limit, Limit);
        float cz = Mathf.Clamp((float)GetNode<SpinBox>("Content/PositionContent/Z").Value, -Limit, Limit);

        float cvx = Mathf.Clamp((float)GetNode<SpinBox>("Content/VelocityContent/velX").Value, -Limit, Limit);
        float cvy = Mathf.Clamp((float)GetNode<SpinBox>("Content/VelocityContent/velY").Value, -Limit, Limit);
        float cvz = Mathf.Clamp((float)GetNode<SpinBox>("Content/VelocityContent/velZ").Value, -Limit, Limit);

        float cm = Mathf.Clamp((float)GetNode<SpinBox>("Content/MassContent/mass").Value, 0f, Limit);

        return new CelestialBodyParams
        {
            Position = new Vector3(cx, cy, cz),
            Velocity = new Vector3(cvx, cvy, cvz),
            Mass = cm,
            Type = (CelestialBodyType)ob.Selected
        };
    }
}

public struct CelestialBodyParams
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Mass;
    public CelestialBodyType Type;
}
