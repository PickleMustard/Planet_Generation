using System.Collections.Generic;
using Constructables;
using Godot;

namespace UI;

/// <summary>
/// Manages Label3D billboard labels for continents and stations when the orbital body
/// window is open. Labels face the camera, are hidden when occluded by the body,
/// and have Area3D collision shapes for click detection.
/// </summary>
public partial class BillboardLabelManager : Node
{
    private readonly List<LabelEntry> _labels = new();
    private IOrbitalBody? _body;
    private Camera3D? _camera;

    /// <summary>
    /// Creates billboard labels for all continents and stations on the body.
    /// </summary>
    public void Initialize(IOrbitalBody body, Camera3D camera)
    {
        Cleanup();
        _body = body;
        _camera = camera;

        var bodyNode = (Node3D)body;

        // Continent labels
        if (body.Mesh?.Continents != null)
        {
            foreach (var kvp in body.Mesh.Continents)
            {
                var continent = kvp.Value;
                var label = CreateLabel3D(
                    $"Continent {kvp.Key}",
                    continent.averagedCenter.Normalized() * (body.Radius * 1.08f),
                    body.Radius
                );

                bodyNode.AddChild(label.Label);
                if (label.ClickArea != null)
                    bodyNode.AddChild(label.ClickArea);

                label.ItemType = "continent";
                label.ItemIndex = kvp.Key;
                _labels.Add(label);
            }
        }

        // Station labels
        if (body.SatellitesContainer != null)
        {
            int stationIdx = 0;
            foreach (var child in body.SatellitesContainer.GetChildren())
            {
                if (child is StationSatellite station)
                {
                    var stationNode = (Node3D)station;
                    var label = CreateLabel3D(
                        station.Name,
                        Vector3.Up * (body.Radius * 0.15f), // slightly above station
                        body.Radius * 0.5f
                    );

                    // Parent to station so it tracks orbital motion
                    stationNode.AddChild(label.Label);
                    if (label.ClickArea != null)
                        stationNode.AddChild(label.ClickArea);

                    label.ItemType = "station";
                    label.ItemIndex = stationIdx;
                    _labels.Add(label);
                    stationIdx++;
                }
            }
        }

        SetProcess(true);
    }

    /// <summary>
    /// Removes all created labels and collision shapes.
    /// </summary>
    public void Cleanup()
    {
        foreach (var entry in _labels)
        {
            if (IsInstanceValid(entry.Label))
                entry.Label.QueueFree();
            if (entry.ClickArea != null && IsInstanceValid(entry.ClickArea))
                entry.ClickArea.QueueFree();
        }

        _labels.Clear();
        _body = null;
        _camera = null;
        SetProcess(false);
    }

    public override void _Ready()
    {
        SetProcess(false);
    }

    /// <summary>
    /// Occlusion test: hide labels that are on the far side of the body from the camera.
    /// </summary>
    public override void _Process(double delta)
    {
        if (_body == null || _camera == null)
            return;

        var bodyCenter = ((Node3D)_body).GlobalPosition;
        var cameraPos = _camera.GlobalPosition;
        var camToBody = (bodyCenter - cameraPos).Normalized();

        foreach (var entry in _labels)
        {
            if (!IsInstanceValid(entry.Label))
                continue;

            var labelDir = (entry.Label.GlobalPosition - bodyCenter).Normalized();
            // dot > 0 means label is on the far side (same direction as cam-to-body)
            // threshold of ~0.15 allows slightly past-equator labels to stay visible
            float dot = camToBody.Dot(labelDir);
            entry.Label.Visible = dot < 0.15f;

            if (entry.ClickArea != null && IsInstanceValid(entry.ClickArea))
            {
                // Disable collision for occluded labels
                entry.ClickArea.InputRayPickable = dot < 0.15f;
            }
        }
    }

    /// <summary>
    /// Check if a raycast hit one of our click areas and return the item info.
    /// </summary>
    public bool TryGetClickedItem(Node3D hitCollider, out string itemType, out int itemIndex)
    {
        foreach (var entry in _labels)
        {
            if (entry.ClickArea != null && entry.ClickArea == hitCollider)
            {
                itemType = entry.ItemType;
                itemIndex = entry.ItemIndex;
                return true;
            }
        }

        itemType = "";
        itemIndex = -1;
        return false;
    }

    // ───────── Helpers ─────────

    private static LabelEntry CreateLabel3D(string text, Vector3 localPosition, float bodyRadius)
    {
        var label = new Label3D
        {
            Text = text,
            Position = localPosition,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FixedSize = true,
            PixelSize = 0.001f * Mathf.Max(bodyRadius, 0.5f),
            FontSize = 48,
            OutlineSize = 8,
            Modulate = new Color(1f, 1f, 1f, 0.9f),
            OutlineModulate = new Color(0f, 0f, 0f, 0.7f),
            NoDepthTest = false,
        };

        // Create a click area for interaction
        var area = new Area3D
        {
            Position = localPosition,
            InputRayPickable = true,
        };

        var collision = new CollisionShape3D();
        var shape = new SphereShape3D
        {
            Radius = bodyRadius * 0.08f,
        };
        collision.Shape = shape;
        area.AddChild(collision);

        return new LabelEntry
        {
            Label = label,
            ClickArea = area,
            ItemType = "",
            ItemIndex = -1,
        };
    }

    private class LabelEntry
    {
        public required Label3D Label { get; set; }
        public Area3D? ClickArea { get; set; }
        public string ItemType { get; set; } = "";
        public int ItemIndex { get; set; } = -1;
    }
}
