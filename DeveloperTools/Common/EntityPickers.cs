#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Resources;
using Structures.Logistics;
using Logistics.Resources;
using UtilityLibrary;

namespace DeveloperTools.Common;

/// <summary>
/// Factory + adapters that map each definition database onto a configured
/// <see cref="EntityPickerPopup"/>. Each method returns a ready popup but does NOT add it to
/// the scene tree — the caller does (e.g. <c>btn.GetTree().Root.AddChild(popup); popup.PopupCentered();</c>).
/// </summary>
public static class EntityPickers
{
    // ── Resources ─────────────────────────────────────────────────────────

    public static EntityPickerPopup Resource()
    {
        var popup = EntityPickerPopup.Create();
        ConfigureResource(popup);
        return popup;
    }

    /// <summary>
    /// Shared resource adapter so both <see cref="Resource"/> and the back-compat
    /// <c>ResourcePickerPopup</c> subclass use one implementation.
    /// </summary>
    internal static void ConfigureResource(EntityPickerPopup popup)
    {
        var items = new List<PickerItem>();
        try
        {
            var db = ResourceDatabase.Instance;
            if (db != null && db.IsLoaded)
            {
                foreach (var r in db.GetAllResources().Values)
                    items.Add(new PickerItem
                    {
                        Id = r.IdName ?? "",
                        DisplayName = r.IdName ?? "",
                        Category = r.ResourceType,
                        Tier = r.ResourceTier,
                        Tags = r.Tags,
                        IconTexture = r.Icon?.Texture,
                        IconTint = r.GetEffectiveIconTint(),
                    });
            }
        }
        catch (Exception ex) { GameLogger.Warning($"EntityPickers.Resource load failed: {ex.Message}"); }

        popup.Configure(items, new PickerConfig
        {
            Title = "Pick Resource",
            SearchPlaceholder = "Search resources…",
        });
    }

    // ── Recipes ───────────────────────────────────────────────────────────

    public static EntityPickerPopup Recipe(bool multi = false)
    {
        var items = new List<PickerItem>();
        try
        {
            foreach (var r in RecipeDatabase.Instance.GetAllRecipes().Values)
                items.Add(new PickerItem
                {
                    Id = r.RecipeId ?? "",
                    DisplayName = r.DisplayName ?? r.RecipeId ?? "",
                    Category = r.Category,
                    Tier = null,
                    Tags = r.Tags,
                    IconTexture = r.Icon?.Texture,
                    IconTint = r.Icon?.Tint ?? Colors.White,
                });
        }
        catch (Exception ex) { GameLogger.Warning($"EntityPickers.Recipe load failed: {ex.Message}"); }

        var popup = EntityPickerPopup.Create();
        popup.Configure(items, new PickerConfig
        {
            Title = "Pick Recipe",
            SearchPlaceholder = "Search recipes…",
            AllowGroupByTier = false,
            ShowTierFilter = false,
            MultiSelect = multi,
        });
        return popup;
    }

    // ── Buildings / Stations / Ships ──────────────────────────────────────
    // Built and ready, but currently UNWIRED — no dev-tool field references a building,
    // station, or ship by instance yet. Available for future call sites.

    public static EntityPickerPopup Building()
    {
        var items = new List<PickerItem>();
        try
        {
            foreach (var b in BuildingDatabase.Instance.GetAllBuildings().Values)
                items.Add(new PickerItem
                {
                    Id = b.IdName ?? "",
                    DisplayName = b.DisplayName ?? b.IdName ?? "",
                    Category = b.Category,
                    Tier = b.MaxResourceTier,
                    IconTexture = b.Icon?.Texture,
                    IconTint = b.Icon?.Tint ?? Colors.White,
                });
        }
        catch (Exception ex) { GameLogger.Warning($"EntityPickers.Building load failed: {ex.Message}"); }

        var popup = EntityPickerPopup.Create();
        popup.Configure(items, new PickerConfig
        {
            Title = "Pick Building",
            SearchPlaceholder = "Search buildings…",
            ShowTagsFilter = false,
        });
        return popup;
    }

    public static EntityPickerPopup Station()
    {
        var items = new List<PickerItem>();
        try
        {
            foreach (var s in StationDatabase.Instance.GetAllStations().Values)
                items.Add(new PickerItem
                {
                    Id = s.Name,
                    DisplayName = s.Name,
                    Category = s.StationType,
                    Tier = null,
                    IconTexture = s.Icon?.Texture,
                    IconTint = s.Icon?.Tint ?? Colors.White,
                });
        }
        catch (Exception ex) { GameLogger.Warning($"EntityPickers.Station load failed: {ex.Message}"); }

        var popup = EntityPickerPopup.Create();
        popup.Configure(items, new PickerConfig
        {
            Title = "Pick Station",
            SearchPlaceholder = "Search stations…",
            AllowGroupByTier = false,
            ShowTierFilter = false,
            ShowTagsFilter = false,
        });
        return popup;
    }

    public static EntityPickerPopup Ship()
    {
        var items = new List<PickerItem>();
        try
        {
            foreach (var s in ShipDatabase.Instance.GetAllShips().Values)
                items.Add(new PickerItem
                {
                    Id = s.Name,
                    DisplayName = s.Name,
                    Category = s.EngineCategory,
                    Tier = s.ShipLevel,
                    IconTexture = s.Icon?.Texture,
                    IconTint = s.Icon?.Tint ?? Colors.White,
                });
        }
        catch (Exception ex) { GameLogger.Warning($"EntityPickers.Ship load failed: {ex.Message}"); }

        var popup = EntityPickerPopup.Create();
        popup.Configure(items, new PickerConfig
        {
            Title = "Pick Ship",
            SearchPlaceholder = "Search ships…",
            ShowTagsFilter = false,
        });
        return popup;
    }
}
#endif
