using System.Linq;
using Constructables;
using Godot;
using ProceduralGeneration;
using Structures.Logistics;
using Structures.Resources;
using UI.Components;
using UI.PlanetBoard.Modes;
using UtilityLibrary;

namespace UI.PlanetBoard;

/// <summary>
/// Top-level chrome for the PlanetBoard. Holds the active <see cref="IOrbitalBody"/>
/// and the active <see cref="IPlanetBoardMode"/>; the embedded
/// <see cref="PlanetBoardView"/> does the actual rendering and input.
/// </summary>
public partial class PlanetBoardWindow : Control
{
    [Signal]
    public delegate void WindowCloseRequestedEventHandler();

    [Signal]
    public delegate void BackRequestedEventHandler();

    [Export]
    private PlanetBoardView? _viewBoard;

    [Export]
    private OptionButton? _modeSwitcher;

    [Export]
    private OptionButton? _linkProfilePicker;

    [Export]
    private Label? _titleLabel;

    [Export]
    private Label? _summaryLabel;

    [Export]
    private Button? _closeButton;

    [Export]
    private Button? _backButton;

    [Export]
    private Control? _blockInput;

    [Export]
    private PanelContainer? _infoBar;

    [Export]
    private VBoxContainer? _infoContent;

    public PlanetBoardView View
    {
        get => _viewBoard!;
    }

    private readonly ResourceLinkPlanningMode _linkMode = new();
    private readonly TransferRoutePlanningMode _routeMode = new();
    private readonly OverviewMode _overviewMode = new();

    private readonly System.Collections.Generic.List<string> _linkProfileIds = new();

    private IOrbitalBody? _body;
    private bool _boardEventsBound;

    public override void _Ready()
    {
        AddToGroup("planet_board_window");

        if (_modeSwitcher != null)
        {
            _modeSwitcher.Clear();
            _modeSwitcher.AddItem(_linkMode.DisplayName, 0);
            _modeSwitcher.AddItem(_routeMode.DisplayName, 1);
            _modeSwitcher.AddItem(_overviewMode.DisplayName, 2);
            _modeSwitcher.Selected = 0;
            _modeSwitcher.ItemSelected += OnModeChanged;
        }

        PopulateLinkProfilePicker();
        UpdateLinkProfilePickerVisibility(_linkMode);

        if (_closeButton != null)
            _closeButton.Pressed += Close;

        if (_backButton != null)
            _backButton.Pressed += OnBackPressed;

        if (_blockInput != null)
            _blockInput.GuiInput += OnBackdropInput;

        if (_infoBar != null)
            _infoBar.Visible = false;

        BindBoardEvents();

        _viewBoard?.SetMode(_linkMode);
    }

    private void BindBoardEvents()
    {
        if (_boardEventsBound || _viewBoard == null)
            return;
        var world = _viewBoard.World;
        if (world == null)
            return;
        world.BuildingClicked += OnBoardBuildingClicked;
        world.LinkClicked += OnBoardLinkClicked;
        world.BundleClicked += OnBoardBundleClicked;
        world.SelectionCleared += OnBoardSelectionCleared;
        _boardEventsBound = true;
    }

    public void OpenForBody(IOrbitalBody body, string mode = "ResourceLink")
    {
        _body = body;
        if (_titleLabel != null)
            _titleLabel.Text = $"Planet Board — {body.BodyName}";
        UpdateSummary();
        ClearInfoBar();
        Visible = true;
        _viewBoard?.SetBody(body);
        SelectMode(mode);
    }

    public void Close()
    {
        Visible = false;
        ClearInfoBar();
        EmitSignal(SignalName.WindowCloseRequested);
    }

    private void OnBackPressed()
    {
        EmitSignal(SignalName.BackRequested);
    }

    private void OnBackdropInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb
            && mb.ButtonIndex == MouseButton.Left
            && mb.Pressed)
        {
            Close();
        }
    }

    private void SelectMode(string mode)
    {
        IPlanetBoardMode strategy = mode switch
        {
            "TransferRoute" => _routeMode,
            "Overview" => _overviewMode,
            _ => _linkMode,
        };
        _viewBoard?.SetMode(strategy);
        if (_modeSwitcher != null)
            _modeSwitcher.Selected = strategy switch
            {
                ResourceLinkPlanningMode => 0,
                TransferRoutePlanningMode => 1,
                OverviewMode => 2,
                _ => 0,
            };
        UpdateLinkProfilePickerVisibility(strategy);
    }

    private void OnModeChanged(long index)
    {
        IPlanetBoardMode strategy = index switch
        {
            1 => _routeMode,
            2 => _overviewMode,
            _ => _linkMode,
        };
        _viewBoard?.SetMode(strategy);
        UpdateLinkProfilePickerVisibility(strategy);
    }

    // ── Link profile picker ─────────────────────────────────────────────

    /// <summary>
    /// Fills the picker from the link profile database and seeds the link mode's active profile
    /// with the first entry. Labels read "{id}  T{tier} · {state}".
    /// </summary>
    private void PopulateLinkProfilePicker()
    {
        if (_linkProfilePicker == null)
            return;

        _linkProfilePicker.Clear();
        _linkProfileIds.Clear();

        var db = LinkProfileDatabase.Instance;
        if (db != null)
        {
            foreach (var kv in db.GetAllProfiles())
            {
                var p = kv.Value;
                _linkProfilePicker.AddItem($"{p.IdName}  T{p.Tier} · {p.StateOfMatter}", _linkProfileIds.Count);
                _linkProfileIds.Add(p.IdName);
            }
        }

        if (_linkProfileIds.Count > 0)
        {
            _linkProfilePicker.Selected = 0;
            _linkMode.ActiveProfileId = _linkProfileIds[0];
        }
        else
        {
            _linkMode.ActiveProfileId = null;
        }

        _linkProfilePicker.ItemSelected += OnLinkProfileSelected;
    }

    private void OnLinkProfileSelected(long index)
    {
        if (index < 0 || index >= _linkProfileIds.Count)
            return;
        _linkMode.ActiveProfileId = _linkProfileIds[(int)index];
    }

    private void UpdateLinkProfilePickerVisibility(IPlanetBoardMode strategy)
    {
        if (_linkProfilePicker != null)
            _linkProfilePicker.Visible = strategy is ResourceLinkPlanningMode;
    }

    // ── Header summary ──────────────────────────────────────────────────

    private void UpdateSummary()
    {
        if (_summaryLabel == null || _body == null)
            return;

        var buildings = _body.GetAllBuildings();
        int buildingCount = buildings.Count;
        int linkCount = 0;
        foreach (var b in buildings)
        {
            foreach (var node in b.Nodes)
            {
                if (node.Link != null && node.Link.Source == node)
                    linkCount++;
            }
        }
        int activeTransfers = _body.GetTotalActiveTransfers();
        _summaryLabel.Text = $"{buildingCount} buildings · {linkCount} links · {activeTransfers} active transfers";
    }

    // ── Info bar ────────────────────────────────────────────────────────

    private void OnBoardBuildingClicked(Building building)
    {
        if (_infoBar == null || _infoContent == null)
            return;
        ClearInfoBar();
        PopulateBuildingInfo(building);
        _infoBar.Visible = true;
    }

    private void OnBoardLinkClicked(ResourceLink link)
    {
        if (_infoBar == null || _infoContent == null)
            return;
        ClearInfoBar();
        PopulateLinkInfo(link);
        _infoBar.Visible = true;
    }

    private void OnBoardBundleClicked(ResourceLink link, ResourcePackage pkg)
    {
        if (_infoBar == null || _infoContent == null)
            return;
        ClearInfoBar();
        PopulateBundleInfo(link, pkg);
        _infoBar.Visible = true;
    }

    private void OnBoardSelectionCleared()
    {
        if (_infoBar == null)
            return;
        ClearInfoBar();
        _infoBar.Visible = false;
    }

    private void ClearInfoBar() => DetailRowBuilder.Clear(_infoContent);

    private void PopulateBuildingInfo(Building building)
    {
        if (_infoContent == null)
            return;

        DetailRowBuilder.AddHeader(_infoContent, building.Name);
        DetailRowBuilder.AddRow(_infoContent, "Type", building.Definition?.DisplayName ?? "—");

        AddStorageSection("Input", building.InputStorage);
        AddStorageSection("Output", building.OutputStorage);
        AddStorageSection("Bulk", building.BulkStorage);

        DetailRowBuilder.AddSeparator(_infoContent);
        DetailRowBuilder.AddHeader(_infoContent, "Links");

        bool anyLink = false;
        foreach (var node in building.Nodes)
        {
            string side = $"side {node.SideIndex}.{node.SlotIndex}";
            string kind = node.Kind.ToString();
            var link = node.Link;
            if (link == null)
            {
                DetailRowBuilder.AddRow(_infoContent, $"{side} ({kind})", "—");
                continue;
            }
            anyLink = true;
            var other = link.Source == node ? link.Target : link.Source;
            string otherName = other?.Owner?.Name ?? "?";
            string tier = link.Profile != null ? $"T{link.Profile.Tier}" : "—";
            DetailRowBuilder.AddRow(_infoContent, $"{side} ({kind})", $"→ {otherName} [{tier}]");
        }
        if (!anyLink && building.Nodes.Count == 0)
            DetailRowBuilder.AddRow(_infoContent, "Status", "No ports");
    }

    private void AddStorageSection(string title, Storage? storage)
    {
        if (_infoContent == null || storage == null || storage.Slots.Count == 0)
            return;

        var quantities = storage.GetAllQuantities();
        DetailRowBuilder.AddSeparator(_infoContent);
        DetailRowBuilder.AddHeader(_infoContent, title);

        if (quantities.Count == 0)
        {
            DetailRowBuilder.AddRow(_infoContent, "Status", "Empty");
            return;
        }

        foreach (var kvp in quantities.OrderByDescending(k => k.Value))
            DetailRowBuilder.AddRow(_infoContent, kvp.Key, $"{kvp.Value:F1}");
    }

    private void PopulateLinkInfo(ResourceLink link)
    {
        if (_infoContent == null)
            return;

        string sourceName = link.Source?.Owner?.Name ?? "?";
        string targetName = link.Target?.Owner?.Name ?? "?";
        DetailRowBuilder.AddHeader(_infoContent, "Link");
        DetailRowBuilder.AddRow(_infoContent, "Source", sourceName);
        DetailRowBuilder.AddRow(_infoContent, "Target", targetName);

        if (link.Profile is { } profile)
        {
            DetailRowBuilder.AddSeparator(_infoContent);
            DetailRowBuilder.AddHeader(_infoContent, "Profile");
            DetailRowBuilder.AddRow(_infoContent, "Tier", profile.Tier.ToString());
            DetailRowBuilder.AddRow(_infoContent, "Speed", $"{profile.TransportSpeed:F2}");
            DetailRowBuilder.AddRow(_infoContent, "Pkg Size", profile.PackageSize.ToString());
            DetailRowBuilder.AddRow(_infoContent, "Slots", profile.SlotCapacity.ToString());
            DetailRowBuilder.AddRow(_infoContent, "Bundle", profile.BundleTime.ToString());
        }

        DetailRowBuilder.AddSeparator(_infoContent);
        DetailRowBuilder.AddHeader(_infoContent, $"In Flight ({link.InFlight.Count})");
        if (link.InFlight.Count == 0)
        {
            DetailRowBuilder.AddRow(_infoContent, "Status", "Idle");
        }
        else
        {
            foreach (var pkg in link.InFlight)
            {
                int pct = Mathf.RoundToInt(Mathf.Clamp(pkg.Progress, 0f, 1f) * 100f);
                DetailRowBuilder.AddRow(_infoContent, pkg.ResourceId, $"{pkg.Quantity:F1}  ({pct}%)");
            }
        }

        if (link.ArrivalBuffer.Count > 0)
        {
            DetailRowBuilder.AddSeparator(_infoContent);
            DetailRowBuilder.AddHeader(_infoContent, $"Stuck ({link.ArrivalBuffer.Count})");
            foreach (var pkg in link.ArrivalBuffer)
                DetailRowBuilder.AddRow(_infoContent, pkg.ResourceId, $"{pkg.Quantity:F1}");
        }
    }

    private void PopulateBundleInfo(ResourceLink link, ResourcePackage pkg)
    {
        if (_infoContent == null)
            return;

        bool stuck = pkg.Stuck || link.ArrivalBuffer.Contains(pkg);
        int pct = Mathf.RoundToInt(Mathf.Clamp(pkg.Progress, 0f, 1f) * 100f);

        DetailRowBuilder.AddHeader(_infoContent, "Bundle");
        DetailRowBuilder.AddRow(_infoContent, "Resource", pkg.ResourceId);
        DetailRowBuilder.AddRow(_infoContent, "Quantity", $"{pkg.Quantity:F1}");
        DetailRowBuilder.AddRow(_infoContent, "Progress", $"{pct}%");
        DetailRowBuilder.AddRow(_infoContent, "Stuck", stuck ? "Yes" : "No");

        DetailRowBuilder.AddSeparator(_infoContent);
        DetailRowBuilder.AddHeader(_infoContent, "Route");
        DetailRowBuilder.AddRow(_infoContent, "Origin", link.Source?.Owner?.Name ?? "?");
        DetailRowBuilder.AddRow(_infoContent, "Destination", link.Target?.Owner?.Name ?? "?");
        DetailRowBuilder.AddRow(_infoContent, "Tier", link.Profile != null ? $"T{link.Profile.Tier}" : "—");
    }
}
