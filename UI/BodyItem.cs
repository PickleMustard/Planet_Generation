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

    public override void _Ready()
    {
        // Hook remove
        var remove = GetNodeOrNull<Button>("Header/RemoveItem");
        if (remove != null)
        {
            remove.Pressed += () => OnRemoveRequested?.Invoke(this);
        }

        // Populate body types
        var ob = GetNodeOrNull<OptionButton>("Content/BodyTypeContent/OptionButton");
        if (ob != null)
        {
            ob.Clear();
            foreach (var name in System.Enum.GetNames(typeof(CelestialBodyType)))
            {
                ob.AddItem(name);
            }
            ob.ItemSelected += idx => UpdateHeaderFromBodyType(ob.GetItemText((int)idx));
        }
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
        return new CelestialBodyParams
        {
            Position = new Vector3(
                (float)GetNode<SpinBox>("Content/PositionContent/X").Value,
                (float)GetNode<SpinBox>("Content/PositionContent/Y").Value,
                (float)GetNode<SpinBox>("Content/PositionContent/Z").Value
            ),
            Velocity = new Vector3(
                (float)GetNode<SpinBox>("Content/VelocityContent/velX").Value,
                (float)GetNode<SpinBox>("Content/VelocityContent/velY").Value,
                (float)GetNode<SpinBox>("Content/VelocityContent/velZ").Value
            ),
            Mass = (float)GetNode<SpinBox>("Content/MassContent/mass").Value,
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
