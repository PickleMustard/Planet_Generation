using System;
using System.Collections.Generic;
using System.Linq;
using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Transfers;
using UtilityLibrary;

namespace UI.TransferPlanning;

/// <summary>
/// Lists valid transfer destinations (other continents + orbital stations).
/// Fires DestinationSelected when the player picks one.
/// </summary>
public partial class EndpointSelectionPanel : Control
{
    public event Action<TransferDestination>? DestinationSelected;

    private VBoxContainer? _destinationList;
    private Label? _headerLabel;
    private Label? _emptyLabel;
    private readonly List<Button> _buttons = new();
    private Button? _selectedButton;

    public override void _Ready()
    {
        _headerLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/HeaderLabel");
        _emptyLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/EmptyLabel");
        _destinationList = GetNodeOrNull<VBoxContainer>(
            "MarginContainer/VBoxContainer/ScrollContainer/DestinationList");
    }

    /// <summary>
    /// Enumerates all valid destinations for transfers from the given continent.
    /// </summary>
    public void Populate(Node3D body, int originContinentIndex)
    {
        Clear();

        if (body is not ISelectableBody selectable)
            return;

        // Add other continents
        if (selectable.Mesh?.Continents != null)
        {
            foreach (var kvp in selectable.Mesh.Continents)
            {
                if (kvp.Key == originContinentIndex)
                    continue;

                var continent = kvp.Value;
                var dest = TransferDestination.ForContinent(kvp.Key);
                AddDestinationButton($"Continent {kvp.Key}", dest);
            }
        }

        // Add completed orbital stations
        Node3D? satellitesContainer = body switch
        {
            CelestialBody cb => cb.SatellitesContainer,
            SatelliteBody sb => sb.SatellitesContainer,
            _ => null,
        };

        if (satellitesContainer != null)
        {
            foreach (var child in satellitesContainer.GetChildren())
            {
                if (child is StationSatellite station && !station.IsUnderConstruction)
                {
                    var dest = TransferDestination.ForStation(station.Id);
                    AddDestinationButton($"Station: {station.Name}", dest);
                }
            }
        }

        bool hasDestinations = _buttons.Count > 0;
        if (_emptyLabel != null)
            _emptyLabel.Visible = !hasDestinations;
    }

    public void Clear()
    {
        foreach (var button in _buttons)
        {
            button.QueueFree();
        }
        _buttons.Clear();
        _selectedButton = null;

        if (_emptyLabel != null)
            _emptyLabel.Visible = true;
    }

    private void AddDestinationButton(string label, TransferDestination destination)
    {
        var button = new Button
        {
            Text = label,
            ToggleMode = true,
            SizeFlagsHorizontal = Control.SizeFlags.Fill,
            CustomMinimumSize = new Vector2(0, 36),
        };

        button.Toggled += (pressed) =>
        {
            if (!pressed) return;

            // Deselect previous
            if (_selectedButton != null && _selectedButton != button)
                _selectedButton.ButtonPressed = false;

            _selectedButton = button;
            DestinationSelected?.Invoke(destination);
        };

        _destinationList?.AddChild(button);
        _buttons.Add(button);
    }
}
