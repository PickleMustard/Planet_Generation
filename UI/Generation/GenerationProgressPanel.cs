using System.Collections.Generic;
using Godot;
using UtilityLibrary;

namespace UI.Generation
{
    public partial class GenerationProgressPanel : PanelContainer
    {
        private VBoxContainer? _mainContainer;
        private Button? _headerButton;
        private Label? _headerLabel;
        private TextureRect? _expandIcon;
        private ScrollContainer? _scrollContainer;
        private VBoxContainer? _itemsContainer;

        private readonly Dictionary<string, GenerationProgressItem> _items = new();
        private PackedScene? _itemScene;

        private bool _isExpanded = true;
        private bool _allComplete = false;
        private double _collapseTimer = 0.0;
        private const double COLLAPSE_DELAY = 3.0;

        private int _completedCount = 0;
        private int _totalCount = 0;

        public override void _Ready()
        {
            CreateUI();
            ConnectSignals();
            LoadItemScene();
        }

        private void CreateUI()
        {
            AnchorLeft = 1.0f;
            AnchorTop = 0.0f;
            AnchorRight = 1.0f;
            AnchorBottom = 0.0f;
            OffsetLeft = -300;
            OffsetTop = 10;
            OffsetRight = -10;
            OffsetBottom = 10;
            SizeFlagsVertical = SizeFlags.ShrinkBegin;
            CustomMinimumSize = new Vector2(280, 0);

            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.12f, 0.12f, 0.14f, 0.95f),
                BorderColor = new Color(0.3f, 0.3f, 0.35f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomRight = 4,
                CornerRadiusBottomLeft = 4,
                ContentMarginLeft = 4,
                ContentMarginTop = 4,
                ContentMarginRight = 4,
                ContentMarginBottom = 4
            };
            AddThemeStyleboxOverride("panel", style);

            _mainContainer = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            AddChild(_mainContainer);

            _headerButton = new Button
            {
                ToggleMode = true,
                ButtonPressed = true,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 28)
            };
            _headerButton.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
            _headerButton.AddThemeColorOverride("font_hover_color", new Color(1.0f, 1.0f, 1.0f));

            var headerNormalStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.15f, 0.15f, 0.18f),
                ContentMarginLeft = 8,
                ContentMarginTop = 4,
                ContentMarginRight = 8,
                ContentMarginBottom = 4
            };
            var headerHoverStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.18f, 0.18f, 0.22f),
                ContentMarginLeft = 8,
                ContentMarginTop = 4,
                ContentMarginRight = 8,
                ContentMarginBottom = 4
            };
            _headerButton.AddThemeStyleboxOverride("normal", headerNormalStyle);
            _headerButton.AddThemeStyleboxOverride("hover", headerHoverStyle);
            _headerButton.AddThemeStyleboxOverride("pressed", headerNormalStyle);

            _expandIcon = new TextureRect
            {
                CustomMinimumSize = new Vector2(16, 16),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsVertical = SizeFlags.ShrinkCenter
            };

            _headerLabel = new Label
            {
                Text = "Generation Progress",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            _headerLabel.AddThemeFontSizeOverride("font_size", 14);
            _headerLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));

            var headerHBox = new HBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkCenter
            };
            headerHBox.AddChild(_expandIcon);
            headerHBox.AddChild(_headerLabel);

            _headerButton.AddChild(headerHBox);
            _mainContainer.AddChild(_headerButton);

            var separator = new HSeparator
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            separator.AddThemeStyleboxOverride("separator", new StyleBoxFlat
            {
                BgColor = new Color(0.25f, 0.25f, 0.28f),
                ContentMarginTop = 1,
                ContentMarginBottom = 1
            });
            _mainContainer.AddChild(separator);

            _scrollContainer = new ScrollContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 200)
            };
            _mainContainer.AddChild(_scrollContainer);

            _itemsContainer = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            _scrollContainer.AddChild(_itemsContainer);

            UpdateExpandIcon();
            UpdateVisibility();
        }

        private void LoadItemScene()
        {
            var path = "res://UI/Generation/GenerationProgressItem.tscn";
            if (ResourceLoader.Exists(path))
            {
                _itemScene = GD.Load<PackedScene>(path);
            }
        }

        private void ConnectSignals()
        {
            _headerButton!.Pressed += OnHeaderPressed;

            if (TaskTimer.Instance != null)
            {
                TaskTimer.Instance.TimerStarted += OnTimerStarted;
                TaskTimer.Instance.TimerCompleted += OnTimerCompleted;
            }
        }

        public override void _ExitTree()
        {
            if (TaskTimer.Instance != null)
            {
                TaskTimer.Instance.TimerStarted -= OnTimerStarted;
                TaskTimer.Instance.TimerCompleted -= OnTimerCompleted;
            }
        }

        private void OnHeaderPressed()
        {
            _isExpanded = _headerButton!.ButtonPressed;
            UpdateExpandIcon();
            UpdateVisibility();
        }

        private void UpdateExpandIcon()
        {
            string iconPath = _isExpanded ? "res://UI/chevron_down.svg" : "res://UI/chevron_right.svg";
            if (ResourceLoader.Exists(iconPath))
            {
                _expandIcon!.Texture = GD.Load<Texture2D>(iconPath);
            }
        }

        private void UpdateVisibility()
        {
            _scrollContainer!.Visible = _isExpanded;
        }

        private void OnTimerStarted(string name, int totalSteps, string[] stepNames)
        {
            if (_items.ContainsKey(name)) return;

            _totalCount++;
            _allComplete = false;
            _collapseTimer = 0.0;

            if (!_isExpanded)
            {
                _isExpanded = true;
                _headerButton!.ButtonPressed = true;
                UpdateExpandIcon();
                UpdateVisibility();
            }

            AddItem(name, totalSteps, stepNames);
            UpdateHeaderLabel();
            Show();
        }

        private void OnTimerCompleted(string name, double totalSeconds)
        {
            _completedCount++;

            if (_completedCount >= _totalCount && _totalCount > 0)
            {
                _allComplete = true;
            }

            UpdateHeaderLabel();
        }

        private void AddItem(string bodyName, int totalSteps, string[] stepNames)
        {
            if (_items.ContainsKey(bodyName)) return;

            GenerationProgressItem item;
            if (_itemScene != null)
            {
                item = _itemScene.Instantiate<GenerationProgressItem>();
            }
            else
            {
                item = new GenerationProgressItem();
            }

            _itemsContainer!.AddChild(item);
            item.ConnectToTimer(bodyName, totalSteps, stepNames);
            _items[bodyName] = item;
        }

        private void ClearItems()
        {
            foreach (var item in _items.Values)
            {
                item.QueueFree();
            }
            _items.Clear();
            _completedCount = 0;
            _totalCount = 0;
        }

        private void UpdateHeaderLabel()
        {
            if (_allComplete)
            {
                _headerLabel!.Text = $"Generation Complete ({_totalCount})";
                _headerLabel!.AddThemeColorOverride("font_color", new Color(0.6f, 0.8f, 0.6f));
            }
            else if (_totalCount > 0)
            {
                _headerLabel!.Text = $"Generating ({_completedCount}/{_totalCount})";
                _headerLabel!.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.4f));
            }
            else
            {
                _headerLabel!.Text = "Generation Progress";
                _headerLabel!.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
            }
        }

        public override void _Process(double delta)
        {
            if (_allComplete && _isExpanded)
            {
                _collapseTimer += delta;
                if (_collapseTimer >= COLLAPSE_DELAY)
                {
                    _isExpanded = false;
                    _headerButton!.ButtonPressed = false;
                    UpdateExpandIcon();
                    UpdateVisibility();
                    _collapseTimer = 0.0;
                }
            }
        }

        public void Reset()
        {
            ClearItems();
            _allComplete = false;
            _collapseTimer = 0.0;
            UpdateHeaderLabel();
        }
    }
}
