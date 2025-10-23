using System;
using Godot;
using PlanetGeneration;
using UtilityLibrary;

namespace UI;

public partial class BodyItem : VBoxContainer
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

    // Satellites UI
    [Export]
    public Button AddSatellite;

    [Export]
    public Button RemoveSatellite;

    [Export]
    public Label SatellitesCountLabel;

    [Export]
    public VBoxContainer SatellitesList;

    public Action<BodyItem> OnRemoveRequested;

    private PackedScene _satelliteItemScene;
    private PackedScene _satelliteBeltItemScene;
    private Godot.Collections.Dictionary templateDict = new Godot.Collections.Dictionary();

    private Godot.Collections.Array individualSatelliteHolder;
    private Godot.Collections.Array satelliteBeltHolder;

    public void SetTemplateDict(Godot.Collections.Dictionary dict)
    {
        templateDict = dict;
    }

    private const float Limit = 10000f; // constrain within ±10,000 units (mass 0..10,000)
    private const float MassLimit = 100000000f; // constrain within ±100,000,000,000 units (mass 0..100,000,000,000)
    private const float SizeLimit = 10000f; // constrain within ±10,000 units (size 0..10,000)

    public override void _EnterTree()
    {
        // Hook remove
        var remove = GetNodeOrNull<Button>("Header/RemoveItem");
        individualSatelliteHolder = new Godot.Collections.Array();
        satelliteBeltHolder = new Godot.Collections.Array();
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
        size ??= GetNodeOrNull<SpinBox>("Content/SizeContent/size");

        // Satellites UI
        AddSatellite ??= GetNodeOrNull<Button>(
            "Content/SatellitesContent/SatellitesHeader/AddSatellite"
        );
        RemoveSatellite ??= GetNodeOrNull<Button>(
            "Content/SatellitesContent/SatellitesHeader/RemoveSatellite"
        );
        SatellitesCountLabel ??= GetNodeOrNull<Label>(
            "Content/SatellitesContent/SatellitesHeader/SatellitesCountLabel"
        );
        SatellitesList ??= GetNodeOrNull<VBoxContainer>(
            "Content/SatellitesContent/SatellitesScroll/SatellitesList"
        );

        _satelliteItemScene = GD.Load<PackedScene>("res://UI/SatelliteItem.tscn");
        _satelliteBeltItemScene = GD.Load<PackedScene>("res://UI/SatelliteBeltItem.tscn");

        // Apply input constraints to fields
        ApplyConstraints();

        if (X != null)
        {
            X.GuiInput += idx => EmitSignal(SignalName.ItemUpdate);
        }
        if (Y != null)
        {
            Y.GuiInput += idx => EmitSignal(SignalName.ItemUpdate);
        }
        if (Z != null)
        {
            Z.GuiInput += idx => EmitSignal(SignalName.ItemUpdate);
        }
        if (velX != null)
        {
            velX.GuiInput += idx => EmitSignal(SignalName.ItemUpdate);
        }
        if (velY != null)
        {
            velY.GuiInput += idx => EmitSignal(SignalName.ItemUpdate);
        }
        if (velZ != null)
        {
            velZ.GuiInput += idx => EmitSignal(SignalName.ItemUpdate);
        }

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
                PropogateChangeDown(type);
                ApplyTemplate(type);
                EmitSignal(SignalName.ItemUpdate);
            };

            // Ensure a valid initial selection and template
            if (OptionButton.ItemCount > 0)
            {
                if (OptionButton.Selected < 0)
                    OptionButton.Select(0);
                UpdateHeaderFromBodyType(OptionButton.GetItemText(OptionButton.Selected));
                ApplyTemplate((CelestialBodyType)OptionButton.Selected);
            }
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

        UpdateSatellitesCountLabel();
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
        // Mass: [0, Limit]
        if (mass != null)
        {
            mass.MinValue = 0.0;
            mass.MaxValue = MassLimit;
            mass.AllowGreater = true;
            mass.AllowLesser = false;
        }
    }

    public void PropogateChangeDown(CelestialBodyType type)
    {
        foreach (var si in SatellitesList.GetChildren())
        {
            SatellitesList.RemoveChild(si);
        }
        if (type == CelestialBodyType.BlackHole || type == CelestialBodyType.Star)
        {
            if (satelliteBeltHolder.Count > 0)
            {
                foreach (SatelliteBeltItem item in satelliteBeltHolder)
                {
                    SatellitesList.AddChild((SatelliteBeltItem)item);
                }
            }
        }
        else
        {
            if (individualSatelliteHolder.Count > 0)
            {
                foreach (SatelliteItem item in individualSatelliteHolder)
                {
                    SatellitesList.AddChild((SatelliteItem)item);
                }
            }
        }
        UpdateSatellitesCountLabel();
    }

    public void ApplyTemplate(CelestialBodyType type)
    {
        GD.Print($"ApplyTemplate: {type}");
        // Read defaults from TOML in Configuration/SystemGen with safe fallbacks
        var t = SystemGenTemplates.GetCelestialBodyDefaults(type);
        var template = (Godot.Collections.Dictionary)t["Template"];

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
        templateDict = t;
    }

    public void UpdateBody() { }

    public void UpdateHeaderFromBodyType(string typeName)
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

    public Vector3 GetVelocity()
    {
        float vx = Mathf.Clamp((float)velX.Value, -Limit, Limit);
        float vy = Mathf.Clamp((float)velY.Value, -Limit, Limit);
        float vz = Mathf.Clamp((float)velZ.Value, -Limit, Limit);
        return new Vector3(vx, vy, vz);
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

    public void SetSize(float size)
    {
        if (this.size != null)
            this.size.Value = Mathf.Clamp(size, 0f, SizeLimit);
    }

    public Godot.Collections.Dictionary ToParams()
    {
        var ob = GetNode<OptionButton>("Content/BodyTypeContent/OptionButton");
        // Clamp outgoing values as a final safeguard
        float cx = Mathf.Clamp(
            (float)GetNode<SpinBox>("Content/PositionContent/X").Value,
            -Limit,
            Limit
        );
        float cy = Mathf.Clamp(
            (float)GetNode<SpinBox>("Content/PositionContent/Y").Value,
            -Limit,
            Limit
        );
        float cz = Mathf.Clamp(
            (float)GetNode<SpinBox>("Content/PositionContent/Z").Value,
            -Limit,
            Limit
        );

        float cvx = Mathf.Clamp(
            (float)GetNode<SpinBox>("Content/VelocityContent/velX").Value,
            -Limit,
            Limit
        );
        float cvy = Mathf.Clamp(
            (float)GetNode<SpinBox>("Content/VelocityContent/velY").Value,
            -Limit,
            Limit
        );
        float cvz = Mathf.Clamp(
            (float)GetNode<SpinBox>("Content/VelocityContent/velZ").Value,
            -Limit,
            Limit
        );

        float cm = Mathf.Clamp(
            (float)GetNode<SpinBox>("Content/MassContent/mass").Value,
            0f,
            MassLimit
        );
        float cs = Mathf.Clamp(
            (float)GetNode<SpinBox>("Content/SizeContent/size").Value,
            0f,
            SizeLimit
        );

        templateDict["Type"] = Enum.GetName(
            typeof(CelestialBodyType),
            (CelestialBodyType)ob.Selected
        );
        ((Godot.Collections.Dictionary)templateDict["Template"])["position"] = new Vector3(
            cx,
            cy,
            cz
        );
        ((Godot.Collections.Dictionary)templateDict["Template"])["velocity"] = new Vector3(
            cvx,
            cvy,
            cvz
        );
        ((Godot.Collections.Dictionary)templateDict["Template"])["mass"] = cm;
        ((Godot.Collections.Dictionary)templateDict["Template"])["size"] = cs;

        // Add satellites
        var satellitesList = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (
            (CelestialBodyType)ob.Selected == CelestialBodyType.Star
            || (CelestialBodyType)ob.Selected == CelestialBodyType.BlackHole
        )
        {
            foreach (Node child in SatellitesList.GetChildren())
            {
                if (child is SatelliteBeltItem sbi)
                {
                    satellitesList.Add(sbi.ToParams());
                }
            }
            if (templateDict.ContainsKey("Satellites"))
            {
                templateDict["Satellites"] = satellitesList;
            }
            else
            {
                templateDict.Add("Satellites", satellitesList);
            }
            GD.Print($"Satellites List Size: {satellitesList.Count}");
        }
        else
        {
            foreach (Node child in SatellitesList.GetChildren())
            {
                if (child is SatelliteItem si)
                {
                    satellitesList.Add(si.ToParams());
                }
            }
            if (templateDict.ContainsKey("Satellites"))
            {
                templateDict["Satellites"] = satellitesList;
            }
            else
            {
                templateDict.Add("Satellites", satellitesList);
            }
        }

        return templateDict;
    }

    private void AddSatelliteItem()
    {
        var ob = GetNode<OptionButton>("Content/BodyTypeContent/OptionButton");
        if (SatellitesList == null || _satelliteItemScene == null)
            return;
        if (
            (CelestialBodyType)ob.Selected == CelestialBodyType.BlackHole
            || (CelestialBodyType)ob.Selected == CelestialBodyType.Star
        )
        {
            var satelliteBeltItem = _satelliteBeltItemScene.Instantiate<SatelliteBeltItem>();
            satelliteBeltItem.SetParentType((CelestialBodyType)ob.Selected);
            satelliteBeltItem.OnRemoveRequested += RemoveSatelliteItem;
            satelliteBeltItem.ItemUpdate += OnSatelliteItemUpdate;
            SatellitesList.AddChild(satelliteBeltItem);
            satelliteBeltItem.SubscribeEvents();
            satelliteBeltHolder.Add(satelliteBeltItem);
        }
        else
        {
            var satelliteItem = _satelliteItemScene.Instantiate<SatelliteItem>();
            satelliteItem.SetParentType((CelestialBodyType)ob.Selected);
            satelliteItem.OnRemoveRequested += RemoveSatelliteItem;
            satelliteItem.ItemUpdate += OnSatelliteItemUpdate;
            SatellitesList.AddChild(satelliteItem);
            satelliteItem.SubscribeEvents();
            individualSatelliteHolder.Add(satelliteItem);
        }
        UpdateSatellitesCountLabel();
    }

    private void RemoveLastSatelliteItem()
    {
        var ob = GetNode<OptionButton>("Content/BodyTypeContent/OptionButton");
        if (SatellitesList.GetChildCount() <= 0)
            return; // No negatives
        var index = SatellitesList.GetChildCount() - 1;
        var last = SatellitesList.GetChild(index);
        if (
            (CelestialBodyType)ob.Selected == CelestialBodyType.BlackHole
            || (CelestialBodyType)ob.Selected == CelestialBodyType.Star
        )
        {
            satelliteBeltHolder.RemoveAt(index);
        }
        else
        {
            individualSatelliteHolder.RemoveAt(index);
        }
        SatellitesList.RemoveChild(last);
        last.QueueFree();
        UpdateSatellitesCountLabel();
    }

    private void RemoveSatelliteItem(Node item)
    {
        if (IsInstanceValid(item) && item.GetParent() == SatellitesList)
        {
            if (individualSatelliteHolder.Contains(item))
            {
                individualSatelliteHolder.Remove(item);
            }
            if (satelliteBeltHolder.Contains(item))
            {
                satelliteBeltHolder.Remove(item);
            }
            SatellitesList.RemoveChild(item);
            item.QueueFree();
        }
        UpdateSatellitesCountLabel();
    }

    private void UpdateSatellitesCountLabel()
    {
        if (SatellitesCountLabel != null)
        {
            int count = SatellitesList.GetChildCount();
            SatellitesCountLabel.Text = count <= 1 ? $"{count} satellite" : $"{count} satellites";
        }
    }

    private void OnSatelliteItemUpdate()
    {
        EmitSignal(SignalName.ItemUpdate);
    }

    private void RemoveSatelliteItem(SatelliteItem item)
    {
        if (
            IsInstanceValid(item)
            && item.GetParent() == SatellitesList
            && SatellitesList.GetChildCount() > 1
        )
        {
            if (individualSatelliteHolder.Contains(item))
            {
                individualSatelliteHolder.Remove(item);
            }
            if (satelliteBeltHolder.Contains(item))
            {
                satelliteBeltHolder.Remove(item);
            }
            item.QueueFree();
            UpdateSatellitesCountLabel();
        }
    }
}
