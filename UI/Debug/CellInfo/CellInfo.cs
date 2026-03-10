#if DEBUG
using System;
using System.Text;
using Godot;
using PlayerInteraction.CellSelection;
using Structures.GameState;
using Structures.Resources;
using UI.Debug;

namespace UI.Debug.CellInfo
{
    public partial class CellInfo : BaseDebugModule
    {
        public override string ModuleName => "Cell Info";

        private VBoxContainer? _rootContainer;
        private Button? _clearButton;
        private Label? _noSelectionLabel;

        private VBoxContainer? _cellSection;
        private Label? _cellIndexLabel;
        private Label? _cellHeightLabel;
        private Label? _cellStressLabel;
        private Label? _cellMovementLabel;
        private Label? _cellBorderLabel;
        private Label? _cellInteriornessLabel;
        private Label? _cellCenterLabel;

        private VBoxContainer? _resourcesSection;
        private VBoxContainer? _resourcesList;

        private VBoxContainer? _continentSection;
        private Label? _continentIndexLabel;
        private Label? _continentCrustLabel;
        private Label? _continentVelocityLabel;
        private Label? _continentRotationLabel;
        private Label? _continentAvgHeightLabel;
        private Label? _continentAvgMoistureLabel;
        private Label? _continentNeighborsLabel;
        private VBoxContainer? _continentResourcesList;

        public override void _Ready()
        {
            base._Ready();
            BuildUI();
            ConnectSignals();
        }

        private void BuildUI()
        {
            AnchorRight = 1f;
            AnchorBottom = 1f;
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;

            var scrollContainer = new ScrollContainer
            {
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                AnchorRight = 1f,
                AnchorBottom = 1f,
                OffsetRight = 0,
                OffsetBottom = 0,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
            };
            AddChild(scrollContainer);

            _rootContainer = new VBoxContainer
            {
                AnchorRight = 1f,
                AnchorBottom = 1f,
                OffsetRight = 0,
                OffsetBottom = 0,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
            };
            scrollContainer.AddChild(_rootContainer);

            var headerPanel = new PanelContainer();
            var headerStyle = new StyleBoxFlat { BgColor = new Color(0.15f, 0.15f, 0.18f) };
            headerPanel.AddThemeStyleboxOverride("panel", headerStyle);
            _rootContainer.AddChild(headerPanel);

            var headerHBox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            headerPanel.AddChild(headerHBox);

            var titleLabel = new Label
            {
                Text = "Cell Info",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            titleLabel.AddThemeFontSizeOverride("font_size", 16);
            titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
            headerHBox.AddChild(titleLabel);

            _clearButton = new Button { Text = "Clear" };
            headerHBox.AddChild(_clearButton);

            _noSelectionLabel = new Label
            {
                Text = "Select a cell by pressing R while aiming at a celestial body",
                HorizontalAlignment = HorizontalAlignment.Center,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 200),
            };
            _noSelectionLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            _rootContainer.AddChild(_noSelectionLabel);

            BuildCellSection();
            BuildResourcesSection();
            BuildContinentSection();

            SetInitialVisibility();
        }

        private void BuildCellSection()
        {
            _cellSection = CreateSection("CELL");
            _rootContainer!.AddChild(_cellSection);

            _cellIndexLabel = AddInfoRow(_cellSection, "Index", "-");
            _cellHeightLabel = AddInfoRow(_cellSection, "Height", "-");
            _cellStressLabel = AddInfoRow(_cellSection, "Stress", "-");
            _cellMovementLabel = AddInfoRow(_cellSection, "Movement", "-");
            _cellBorderLabel = AddInfoRow(_cellSection, "Is Border", "-");
            _cellInteriornessLabel = AddInfoRow(_cellSection, "Interiorness", "-");
            _cellCenterLabel = AddInfoRow(_cellSection, "Center", "-");
        }

        private void BuildResourcesSection()
        {
            _resourcesSection = CreateSection("RESOURCES");
            _rootContainer!.AddChild(_resourcesSection);

            _resourcesList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _resourcesSection.AddChild(_resourcesList);
        }

        private void BuildContinentSection()
        {
            _continentSection = CreateSection("CONTINENT");
            _rootContainer!.AddChild(_continentSection);

            _continentIndexLabel = AddInfoRow(_continentSection, "Index", "-");
            _continentCrustLabel = AddInfoRow(_continentSection, "Crust Type", "-");
            _continentVelocityLabel = AddInfoRow(_continentSection, "Velocity", "-");
            _continentRotationLabel = AddInfoRow(_continentSection, "Rotation", "-");
            _continentAvgHeightLabel = AddInfoRow(_continentSection, "Avg Height", "-");
            _continentAvgMoistureLabel = AddInfoRow(_continentSection, "Avg Moisture", "-");
            _continentNeighborsLabel = AddInfoRow(_continentSection, "Neighbors", "-");

            var resourcesHeader = new Label { Text = "Continental Resources:" };
            resourcesHeader.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            _continentSection.AddChild(resourcesHeader);

            _continentResourcesList = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            _continentSection.AddChild(_continentResourcesList);
        }

        private VBoxContainer CreateSection(string title)
        {
            var section = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };

            var panel = new PanelContainer();
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.12f, 0.12f, 0.14f),
                ContentMarginLeft = 8,
                ContentMarginTop = 4,
                ContentMarginRight = 8,
                ContentMarginBottom = 4,
            };
            panel.AddThemeStyleboxOverride("panel", style);
            section.AddChild(panel);

            var headerLabel = new Label { Text = title };
            headerLabel.AddThemeFontSizeOverride("font_size", 14);
            headerLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.4f));
            panel.AddChild(headerLabel);

            var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            content.AddThemeConstantOverride("separation", 2);
            section.AddChild(content);

            return section;
        }

        private Label AddInfoRow(VBoxContainer container, string label, string value)
        {
            var hbox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            container.AddChild(hbox);

            var labelText = new Label
            {
                Text = $"{label}:",
                CustomMinimumSize = new Vector2(120, 0),
            };
            labelText.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            hbox.AddChild(labelText);

            var valueLabel = new Label { Text = value, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            valueLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
            hbox.AddChild(valueLabel);

            return valueLabel;
        }

        private void ConnectSignals()
        {
            _clearButton!.Pressed += OnClearPressed;

            if (CellSelectionManager.Instance != null)
            {
                CellSelectionManager.Instance.CellSelected += OnCellSelected;
                CellSelectionManager.Instance.SelectionCleared += OnSelectionCleared;
            }
        }

        private void OnClearPressed()
        {
            CellSelectionManager.Instance?.ClearSelection();
        }

        private void OnCellSelected(
            VoronoiCell cell,
            ProceduralGeneration.PlanetGeneration.CelestialBody body,
            Continent continent
        )
        {
            GD.Print("OnCellSelected");
            UpdateCellInfo(cell, body, continent);
        }

        private void OnSelectionCleared()
        {
            SetInitialVisibility();
        }

	private void SetInitialVisibility()
	{
		_noSelectionLabel!.Visible = true;
		_cellSection!.Visible = false;
		_resourcesSection!.Visible = false;
		_continentSection!.Visible = false;
	}

        private void UpdateCellInfo(
            VoronoiCell cell,
            ProceduralGeneration.PlanetGeneration.CelestialBody body,
            Continent continent
        )
        {
		GD.Print("UpdateCellInfo");
		_noSelectionLabel!.Visible = true;
		_cellSection!.Visible = true;
		_resourcesSection!.Visible = true;

		_cellIndexLabel!.Text = cell.Index.ToString();
		_cellHeightLabel!.Text = cell.Height.ToString("F4");
		_cellStressLabel!.Text = cell.Stress.ToString("F4");
		_cellMovementLabel!.Text = cell.MovementDirection.ToString();
		_cellBorderLabel!.Text = cell.IsBorderTile ? "Yes" : "No";
		_cellInteriornessLabel!.Text =
			cell.Interiorness == int.MaxValue ? "Max" : cell.Interiorness.ToString();
		_cellCenterLabel!.Text = cell.Center.ToString("F2");

		UpdateResourcesList(cell);

		if (continent != null)
		{
			_continentSection!.Visible = true;
			UpdateContinentInfo(cell.ContinentIndex, continent);
		}
		else
		{
			_continentSection!.Visible = false;
		}
        }

        private void UpdateResourcesList(VoronoiCell cell)
        {
            foreach (var child in _resourcesList!.GetChildren())
            {
                child.QueueFree();
            }

            if (cell.Resources == null || cell.Resources.Count == 0)
            {
                var noResourcesLabel = new Label { Text = "No resources in this cell" };
                noResourcesLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
                _resourcesList.AddChild(noResourcesLabel);
                return;
            }

            foreach (var kvp in cell.Resources)
            {
                var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                _resourcesList.AddChild(row);

                string resourceId = kvp.Key;
                float abundance = kvp.Value;

                Color resourceColor = Colors.White;
                string displayName = FormatResourceName(resourceId);
                string tierText = "";

                if (
                    ResourceDatabase.Instance != null
                    && ResourceDatabase.Instance.TryGetResource(resourceId, out var definition)
                )
                {
                    resourceColor = definition!.DisplayColor;
                    tierText = $" [Tier {definition!.ResourceTier}]";
                }

                var colorIndicator = new ColorRect
                {
                    Color = resourceColor,
                    CustomMinimumSize = new Vector2(12, 12),
                };
                row.AddChild(colorIndicator);

                var nameLabel = new Label
                {
                    Text = $" {displayName}{tierText}",
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                };
                row.AddChild(nameLabel);

                var abundanceLabel = new Label { Text = $"Abundance: {abundance:F2}" };
                abundanceLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.8f, 0.6f));
                row.AddChild(abundanceLabel);
            }
        }

        private void UpdateContinentInfo(int continentIndex, Continent continent)
        {
            _continentIndexLabel!.Text = continentIndex.ToString();
            _continentCrustLabel!.Text = continent.elevation.ToString();
            _continentVelocityLabel!.Text = continent.velocity.ToString("F2");
            _continentRotationLabel!.Text = continent.rotation.ToString("F3");
            _continentAvgHeightLabel!.Text = continent.averageHeight.ToString("F3");
            _continentAvgMoistureLabel!.Text = continent.averageMoisture.ToString("F3");

            var neighbors =
                continent.neighborContinents != null
                    ? string.Join(", ", continent.neighborContinents)
                    : "None";
            _continentNeighborsLabel!.Text = neighbors;

            UpdateContinentResourcesList(continent);
        }

        private void UpdateContinentResourcesList(Continent continent)
        {
            foreach (var child in _continentResourcesList!.GetChildren())
            {
                child.QueueFree();
            }

            if (continent.ResourceAbundance == null || continent.ResourceAbundance.Count == 0)
            {
                var noResourcesLabel = new Label { Text = "No continental resources" };
                noResourcesLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
                _continentResourcesList.AddChild(noResourcesLabel);
                return;
            }

            var resourcesText = new StringBuilder();
            bool first = true;
            foreach (var kvp in continent.ResourceAbundance)
            {
                if (!first)
                    resourcesText.Append(", ");
                resourcesText.Append($"{FormatResourceName(kvp.Key)}: {kvp.Value:F2}");
                first = false;
            }

            var label = new Label
            {
                Text = resourcesText.ToString(),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            label.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
            _continentResourcesList.AddChild(label);
        }

        private string FormatResourceName(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId))
                return resourceId;

            var parts = resourceId.Split('_');
            var result = new StringBuilder();
            foreach (var part in parts)
            {
                if (part.Length > 0)
                {
                    result.Append(char.ToUpper(part[0]));
                    if (part.Length > 1)
                        result.Append(part.Substring(1));
                    result.Append(' ');
                }
            }
            return result.ToString().Trim();
        }

        public override void _ExitTree()
        {
            if (CellSelectionManager.Instance != null)
            {
                CellSelectionManager.Instance.CellSelected -= OnCellSelected;
                CellSelectionManager.Instance.SelectionCleared -= OnSelectionCleared;
            }
            base._ExitTree();
        }
    }
}
#endif
