using Godot;

public partial class ToggleSection : Button
{
    // Keep name 'content_path' to match existing .tscn serialization
    [Export]
    public NodePath content_path;

    public override void _Ready()
    {
        ToggleMode = true;

        if (!string.IsNullOrEmpty(content_path.ToString()))
        {
            var content = GetNodeOrNull<CanvasItem>(content_path);
            if (content != null)
            {
                ButtonPressed = true;
                content.Visible = true;
            }
        }

        Toggled += OnToggled;
    }

    private void OnToggled(bool pressed)
    {
        if (!string.IsNullOrEmpty(content_path.ToString()))
        {
            var content = GetNodeOrNull<CanvasItem>(content_path);
            if (content != null)
            {
                content.Visible = pressed;
            }
        }
    }
}
