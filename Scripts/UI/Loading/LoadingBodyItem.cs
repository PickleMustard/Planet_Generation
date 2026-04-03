using System;
using Godot;

namespace UI.Loading
{
    /// <summary>
    /// UI component for displaying a celestial body's generation status in the loading screen.
    /// Shows body name, current stage, and generation time.
    /// </summary>
    public partial class LoadingBodyItem : PanelContainer
    {
        // UI node references - these will be set via [Export] in the Godot editor
        [Export]
        private TextureRect? _icon;

        [Export]
        private Label? _bodyNameLabel;

        [Export]
        private Label? _stageLabel;

        [Export]
        private Label? _timeLabel;

        // State
        private string _bodyName = string.Empty;
        private string _stage = string.Empty;
        private bool _isComplete = false;
        private float _generationTime = 0f;
        private int _indentLevel = 0;

        /// <summary>
        /// Gets or sets the body name displayed in the UI.
        /// </summary>
        public string BodyName
        {
            get => _bodyName;
            set
            {
                _bodyName = value;
                if (_bodyNameLabel != null)
                    _bodyNameLabel.Text = value;
            }
        }

        /// <summary>
        /// Gets or sets the current generation stage.
        /// </summary>
        public string Stage
        {
            get => _stage;
            set
            {
                _stage = value;
                if (_stageLabel != null)
                    _stageLabel.Text = value;
            }
        }

        /// <summary>
        /// Gets or sets whether the body generation is complete.
        /// When set to true, shows checkmark icon and generation time.
        /// </summary>
        public bool IsComplete
        {
            get => _isComplete;
            set
            {
                _isComplete = value;
                UpdateIcon();
                UpdateTimeDisplay();
            }
        }

        /// <summary>
        /// Gets or sets the total generation time in seconds.
        /// Only displayed when IsComplete is true.
        /// </summary>
        public float GenerationTime
        {
            get => _generationTime;
            set
            {
                _generationTime = value;
                UpdateTimeDisplay();
            }
        }

        /// <summary>
        /// Gets or sets the indent level for hierarchical display.
        /// Each level adds left margin to visually nest satellites under parents.
        /// </summary>
        public int IndentLevel
        {
            get => _indentLevel;
            set
            {
                _indentLevel = value;
                ApplyIndent();
            }
        }

        /// <summary>
        /// Called when the node enters the scene tree.
        /// Initializes the UI components.
        /// </summary>
        public override void _EnterTree()
        {
            // Sync properties that may have been set before the node entered the tree
            if (_bodyNameLabel != null)
                _bodyNameLabel.Text = _bodyName;
            if (_stageLabel != null)
                _stageLabel.Text = _stage;

            // Initialize UI with current state
            UpdateIcon();
            UpdateTimeDisplay();

            // Set up initial styling
            ApplyStyling();
        }

        /// <summary>
        /// Updates the icon based on completion status.
        /// Shows checkmark when complete, otherwise shows a spinner or empty.
        /// </summary>
        private void UpdateIcon()
        {
            if (_icon == null)
                return;

            if (_isComplete)
            {
                // Load checkmark texture
                var checkmarkTexture = GD.Load<Texture2D>("res://UI/checkmark.svg");
                if (checkmarkTexture != null)
                {
                    _icon.Texture = checkmarkTexture;
                    _icon.Modulate = new Color(0.2f, 0.8f, 0.2f); // Green tint
                }
            }
            else
            {
                // Clear icon or show spinner (could be animated later)
                _icon.Texture = null;
                _icon.Modulate = Colors.White;
            }
        }

        /// <summary>
        /// Updates the time display based on completion status.
        /// Shows time when complete, otherwise shows empty or stage indicator.
        /// </summary>
        private void UpdateTimeDisplay()
        {
            if (_timeLabel == null)
                return;

            if (_isComplete && _generationTime > 0)
            {
                _timeLabel.Text = $"{_generationTime:F1}s";
                _timeLabel.Modulate = new Color(0.8f, 0.8f, 0.8f); // Light gray
            }
            else
            {
                _timeLabel.Text = string.Empty;
            }
        }

        /// <summary>
        /// Applies visual styling to the component.
        /// </summary>
        private void ApplyStyling()
        {
            // Create a subtle background style
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.7f);
            styleBox.BorderColor = new Color(0.2f, 0.2f, 0.3f);
            styleBox.BorderWidthLeft = 1;
            styleBox.BorderWidthTop = 1;
            styleBox.BorderWidthRight = 1;
            styleBox.BorderWidthBottom = 1;
            styleBox.CornerRadiusTopLeft = 3;
            styleBox.CornerRadiusTopRight = 3;
            styleBox.CornerRadiusBottomRight = 3;
            styleBox.CornerRadiusBottomLeft = 3;

            AddThemeStyleboxOverride("panel", styleBox);

            ApplyIndent();

            // Configure labels if they exist
            if (_bodyNameLabel != null)
            {
                _bodyNameLabel.AddThemeFontSizeOverride("font_size", 14);
                _bodyNameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 1.0f));
            }

            if (_stageLabel != null)
            {
                _stageLabel.AddThemeFontSizeOverride("font_size", 11);
                _stageLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.9f));
            }

            if (_timeLabel != null)
            {
                _timeLabel.AddThemeFontSizeOverride("font_size", 12);
                _timeLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
                _timeLabel.HorizontalAlignment = HorizontalAlignment.Right;
            }
        }

        /// <summary>
        /// Applies left margin based on indent level for hierarchical display.
        /// </summary>
        private void ApplyIndent()
        {
            if (_indentLevel <= 0)
                return;

            int marginLeft = _indentLevel * 20;

            // Update the panel's stylebox with left content margin
            var existingStyle = GetThemeStylebox("panel") as StyleBoxFlat;
            if (existingStyle != null)
            {
                existingStyle.ContentMarginLeft = marginLeft;
            }
        }

        /// <summary>
        /// Updates all properties at once for efficiency.
        /// </summary>
        /// <param name="bodyName">The name of the celestial body.</param>
        /// <param name="stage">Current generation stage.</param>
        /// <param name="isComplete">Whether generation is complete.</param>
        /// <param name="generationTime">Total generation time in seconds.</param>
        public void UpdateStatus(
            string bodyName,
            string stage,
            bool isComplete = false,
            float generationTime = 0f
        )
        {
            BodyName = bodyName;
            Stage = stage;
            IsComplete = isComplete;
            GenerationTime = generationTime;
        }

        /// <summary>
        /// Sets the body as completed with the given generation time.
        /// </summary>
        /// <param name="generationTime">Total generation time in seconds.</param>
        public void SetComplete(float generationTime)
        {
            IsComplete = true;
            GenerationTime = generationTime;
            // Stage could be updated to "Complete" or left as is
            if (_stageLabel != null && string.IsNullOrEmpty(_stageLabel.Text))
            {
                Stage = "Complete";
            }
        }

        /// <summary>
        /// Sets the body as in progress with the given stage.
        /// </summary>
        /// <param name="stage">Current generation stage.</param>
        public void SetInProgress(string stage)
        {
            IsComplete = false;
            Stage = stage;
            GenerationTime = 0f;
        }
    }
}

