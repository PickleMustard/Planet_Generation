using System;
using Godot;
using UtilityLibrary;

namespace UI.Generation
{
    public partial class GenerationProgressItem : HBoxContainer
    {
        private Label? _bodyNameLabel;
        private Label? _stageLabel;
        private Label? _timerLabel;
        private TextureRect? _statusIcon;

        private string _bodyName = "";
        private bool _isComplete = false;
        private double _localElapsedSeconds = 0.0;
        private double _animationTime = 0.0;
        private int _totalSteps = 0;
        private int _currentStep = 0;
        private bool _isConnected = false;

        private static Texture2D? _spinnerTexture;
        private static Texture2D? _checkmarkTexture;

        public string BodyName
        {
            get => _bodyName;
            set
            {
                _bodyName = value;
                if (_bodyNameLabel != null)
                {
                    _bodyNameLabel.Text = value;
                }
            }
        }

        public bool IsComplete => _isComplete;

        public float Progress => _totalSteps > 0 ? (float)_currentStep / _totalSteps : 0f;

        public override void _Ready()
        {
            CreateUI();
            LoadTextures();
        }

        public void ConnectToTimer(string timerName, int totalSteps, string[] stepNames)
        {
            if (_isConnected)
                return;

            _bodyName = timerName;
            _totalSteps = totalSteps;

            if (_bodyNameLabel != null)
            {
                _bodyNameLabel.Text = timerName;
            }

            if (TaskTimer.Instance != null)
            {
                TaskTimer.Instance.TimerStepChanged += OnTimerStepChanged;
                TaskTimer.Instance.TimerCompleted += OnTimerCompleted;
                _isConnected = true;
            }
        }

        public override void _ExitTree()
        {
            DisconnectFromTimer();
        }

        private void DisconnectFromTimer()
        {
            if (!_isConnected)
                return;

            if (TaskTimer.Instance != null)
            {
                TaskTimer.Instance.TimerStepChanged -= OnTimerStepChanged;
                TaskTimer.Instance.TimerCompleted -= OnTimerCompleted;
            }
            _isConnected = false;
        }

        private void CreateUI()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;

            _statusIcon = new TextureRect
            {
                CustomMinimumSize = new Vector2(20, 20),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            AddChild(_statusIcon);

            var infoContainer = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = 1.0f,
            };
            AddChild(infoContainer);

            _bodyNameLabel = new Label
            {
                Text = _bodyName,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            _bodyNameLabel.AddThemeFontSizeOverride("font_size", 14);
            _bodyNameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
            infoContainer.AddChild(_bodyNameLabel);

            _stageLabel = new Label { Text = "", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _stageLabel.AddThemeFontSizeOverride("font_size", 12);
            _stageLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            infoContainer.AddChild(_stageLabel);

            _timerLabel = new Label
            {
                Text = "00:00.000",
                HorizontalAlignment = HorizontalAlignment.Right,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            _timerLabel.AddThemeFontSizeOverride("font_size", 12);
            _timerLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
            AddChild(_timerLabel);

            UpdateVisualState();
        }

        private void LoadTextures()
        {
            if (_spinnerTexture == null)
            {
                _spinnerTexture = GenerateSpinnerTexture();
            }
            if (_checkmarkTexture == null && ResourceLoader.Exists("res://UI/checkmark.svg"))
            {
                _checkmarkTexture = GD.Load<Texture2D>("res://UI/checkmark.svg");
            }
            UpdateVisualState();
        }

        private Texture2D GenerateSpinnerTexture()
        {
            var image = Image.CreateEmpty(20, 20, false, Image.Format.Rgba8);
            image.Fill(Colors.Transparent);
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(0.3f, 0.5f, 0.7f, 1.0f));
            gradient.SetColor(1, new Color(0.3f, 0.5f, 0.7f, 0.0f));
            var texture = ImageTexture.CreateFromImage(image);
            return texture;
        }

        public override void _Process(double delta)
        {
            if (_isComplete)
                return;

            _animationTime += delta;
            _localElapsedSeconds += delta;

            if (_statusIcon != null && _spinnerTexture != null)
            {
                float rotation = (float)(_animationTime * 8.0);
                _statusIcon.Rotation = rotation;
            }

            UpdateTimerDisplay();
        }

        private void OnTimerStepChanged(
            string name,
            int newStep,
            string stepName,
            double elapsedSeconds
        )
        {
            if (name != _bodyName)
                return;

            _currentStep = newStep;

            if (_stageLabel != null)
            {
                _stageLabel.Text = stepName;
            }

            if (Math.Abs(_localElapsedSeconds - elapsedSeconds) > 0.1)
            {
                _localElapsedSeconds = elapsedSeconds;
            }
        }

        private void OnTimerCompleted(string name, double totalSeconds)
        {
            if (name != _bodyName)
                return;

            _isComplete = true;
            _localElapsedSeconds = totalSeconds;
            _currentStep = _totalSteps;

            UpdateVisualState();
            UpdateTimerDisplay();
            DisconnectFromTimer();
        }

        private void UpdateTimerDisplay()
        {
            if (_timerLabel == null)
                return;

            var time = TimeSpan.FromSeconds(_localElapsedSeconds);
            if (time.TotalHours >= 1)
            {
                _timerLabel.Text = time.ToString(@"hh\:mm\:ss\.fff");
            }
            else if (time.TotalMinutes >= 1)
            {
                _timerLabel.Text = time.ToString(@"mm\:ss\.fff");
            }
            else
            {
                _timerLabel.Text = time.ToString(@"ss\.fff");
            }
        }

        private void UpdateVisualState()
        {
            if (_statusIcon == null)
                return;

            if (_isComplete)
            {
                _statusIcon.Rotation = 0;
                _statusIcon.Texture = _checkmarkTexture;
                if (_bodyNameLabel != null)
                {
                    _bodyNameLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.8f, 0.6f));
                }
                if (_stageLabel != null)
                {
                    _stageLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 0.5f));
                    _stageLabel.Text = "Complete";
                }
            }
            else
            {
                _statusIcon.Texture = _spinnerTexture;
                if (_bodyNameLabel != null)
                {
                    _bodyNameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
                }
            }
        }
    }
}
