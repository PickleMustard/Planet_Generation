using Godot;
using ProceduralGeneration.PlanetGeneration;
using ProceduralGeneration.TextureGeneration;
using UtilityLibrary;

namespace UI.Generation;

/// <summary>
/// Panel that displays the three billboard texture resolutions for a celestial body.
/// Uses tabs to switch between 64x64, 256x256, and 1024x1024 textures.
/// Displays texture metadata in a details table below each texture.
/// </summary>
public partial class TextureViewerPanel : Control
{
    [ExportCategory("UI References")]
    [Export]
    public TabContainer? TabContainer { get; set; }

    [Export]
    public TextureRect? DistantTextureRect { get; set; }

    [Export]
    public TextureRect? FarTextureRect { get; set; }

    [Export]
    public TextureRect? CloseTextureRect { get; set; }

    [Export]
    public VBoxContainer? DistantDetailsPanel { get; set; }

    [Export]
    public VBoxContainer? FarDetailsPanel { get; set; }

    [Export]
    public VBoxContainer? CloseDetailsPanel { get; set; }

    [Export]
    public Label? StatusLabel { get; set; }

    [Export]
    public Label? HeaderLabel { get; set; }

    // Texture data references
    private ImageTexture? _distantTexture;
    private ImageTexture? _farTexture;
    private ImageTexture? _closeTexture;

    public override void _Ready()
    {
        // Initialize any runtime setup if needed
        if (StatusLabel != null)
        {
            StatusLabel.Visible = true;
        }
        if (TabContainer != null)
        {
            TabContainer.Visible = false;
        }
    }

    /// <summary>
    /// Updates the panel to display textures from the given celestial body.
    /// Called by IndividualPlanetGenerator when generation completes.
    /// </summary>
    public void UpdateForBody(CelestialBody body)
    {
        if (body == null || body.BillboardTextures == null)
        {
            ShowNoTexturesMessage();
            return;
        }

        var textures = body.BillboardTextures;

        // Check if textures are ready
        if (!textures.AreTexturesReady)
        {
            ShowNoTexturesMessage("Textures still generating...");
            return;
        }

        _distantTexture = textures.DistantTextureImage;
        _farTexture = textures.FarTextureImage;
        _closeTexture = textures.CloseTextureImage;

        // Update texture displays
        if (DistantTextureRect != null)
            DistantTextureRect.Texture = _distantTexture;
        if (FarTextureRect != null)
            FarTextureRect.Texture = _farTexture;
        if (CloseTextureRect != null)
            CloseTextureRect.Texture = _closeTexture;

        // Update details tables
        if (DistantDetailsPanel != null)
            UpdateDetailsTable(DistantDetailsPanel, _distantTexture, "64x64", body);
        if (FarDetailsPanel != null)
            UpdateDetailsTable(FarDetailsPanel, _farTexture, "256x256", body);
        if (CloseDetailsPanel != null)
            UpdateDetailsTable(CloseDetailsPanel, _closeTexture, "1024x1024", body);

        // Show the tab container
        if (StatusLabel != null)
            StatusLabel.Visible = false;
        if (TabContainer != null)
            TabContainer.Visible = true;

        GameLogger.Info("TextureViewerPanel updated with new body textures");
    }

    private void UpdateDetailsTable(
        VBoxContainer detailsPanel,
        ImageTexture? texture,
        string resolutionLabel,
        CelestialBody body
    )
    {
        // Clear existing rows
        foreach (var child in detailsPanel.GetChildren())
        {
            child.QueueFree();
        }

        if (texture == null)
        {
            AddDetailRow(detailsPanel, "Status", "Not available");
            return;
        }

        // Add header
        var header = new Label { Text = "Texture Details" };
        header.AddThemeFontSizeOverride("font_size", 12);
        header.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.4f));
        detailsPanel.CallDeferred("add_child", header);

        // Resolution
        var image = texture.GetImage();
        AddDetailRow(detailsPanel, "Resolution", $"{image.GetWidth()}x{image.GetHeight()}");

        // Format
        AddDetailRow(detailsPanel, "Format", image.GetFormat().ToString());

        // Memory size estimate
        int memoryBytes = image.GetData().Length;
        string memorySize =
            memoryBytes < 1024 ? $"{memoryBytes} B"
            : memoryBytes < 1024 * 1024 ? $"{memoryBytes / 1024} KB"
            : $"{memoryBytes / (1024 * 1024)} MB";
        AddDetailRow(detailsPanel, "Memory", memorySize);

        // Body info
        AddDetailRow(detailsPanel, "Body", body.BodyName);
        AddDetailRow(detailsPanel, "Type", body.Classification?.TypeName ?? "Unknown");
    }

    private void AddDetailRow(VBoxContainer container, string label, string value)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };

        var labelControl = new Label
        {
            Text = $"{label}:",
            CustomMinimumSize = new Vector2(100, 0),
        };
        labelControl.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));

        var valueControl = new Label { Text = value, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        valueControl.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));

        row.CallDeferred("add_child", labelControl);
        row.CallDeferred("add_child", valueControl);
        container.CallDeferred("add_child", row);
    }

    private void ShowNoTexturesMessage(string message = "Generate a planet to view textures")
    {
        if (StatusLabel != null)
        {
            StatusLabel.Text = message;
            StatusLabel.Visible = true;
        }
        if (TabContainer != null)
        {
            TabContainer.Visible = false;
        }
    }

    /// <summary>
    /// Clears the displayed textures and resets to initial state.
    /// </summary>
    public void Clear()
    {
        _distantTexture = null;
        _farTexture = null;
        _closeTexture = null;

        if (DistantTextureRect != null)
            DistantTextureRect.Texture = null;
        if (FarTextureRect != null)
            FarTextureRect.Texture = null;
        if (CloseTextureRect != null)
            CloseTextureRect.Texture = null;

        ShowNoTexturesMessage();
    }
}
