#if DEBUG
using Godot;

namespace DeveloperTools.Common;

/// <summary>
/// File picker popup constrained to res://Models/Buildings/ and the
/// supported 3D-model extensions. Emits ModelSelected(path) with the res://
/// path on confirm.
/// </summary>
public partial class ModelPathPopup : PopupPanel
{
    [Signal]
    public delegate void ModelSelectedEventHandler(string resPath);

    private Button _browseButton = null!;
    private Label _previewLabel = null!;
    private Button _confirmButton = null!;
    private Button _cancelButton = null!;
    private FileDialog _fileDialog = null!;

    private string? _selectedPath;

    public override void _Ready()
    {
        base._Ready();
        Size = new Vector2I(420, 200);

        var root = new VBoxContainer { CustomMinimumSize = new Vector2(400, 180) };
        AddChild(root);

        _browseButton = new Button { Text = "Browse...", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _browseButton.Pressed += OnBrowsePressed;
        root.AddChild(_browseButton);

        _previewLabel = new Label
        { ThemeTypeVariation = "LabelHighContrast",
            Text = "(no model selected)",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_previewLabel);

        var actions = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _cancelButton = new Button { Text = "Cancel" };
        _cancelButton.Pressed += OnCancelPressed;
        actions.AddChild(_cancelButton);
        _confirmButton = new Button { Text = "Use Model", Disabled = true };
        _confirmButton.Pressed += OnConfirmPressed;
        actions.AddChild(_confirmButton);
        root.AddChild(actions);

        _fileDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Resources,
            CurrentDir = "res://Models/Buildings/",
            Filters = new[] { "*.glb ; GLB scene", "*.gltf ; GLTF scene", "*.tscn ; Godot scene" }
        };
        _fileDialog.FileSelected += OnFileSelected;
        AddChild(_fileDialog);
    }

    private void OnBrowsePressed()
    {
        _fileDialog.PopupCentered(new Vector2I(800, 600));
    }

    private void OnFileSelected(string path)
    {
        _selectedPath = path;
        _previewLabel.Text = path;
        _confirmButton.Disabled = false;
    }

    private void OnConfirmPressed()
    {
        if (!string.IsNullOrEmpty(_selectedPath))
            EmitSignal(SignalName.ModelSelected, _selectedPath);
        Hide();
        QueueFree();
    }

    private void OnCancelPressed()
    {
        Hide();
        QueueFree();
    }
}
#endif
