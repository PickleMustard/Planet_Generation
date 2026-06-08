using System.Collections.Generic;
using System.Linq;
using UtilityLibrary.SaveLoad.Dto;

namespace UtilityLibrary.SaveLoad.Migrations;

/// <summary>
/// One step in the save-format migration chain. A migration transforms a save written at version
/// <see cref="From"/> into one at <c>From + 1</c>. Migrations are applied in order until the save
/// reaches <see cref="SaveYaml.CurrentVersion"/>.
/// </summary>
public interface ISaveMigration
{
    int From { get; }
    SaveFileDto Apply(SaveFileDto dto);
}

/// <summary>
/// Applies the ordered migration chain to bring a deserialized save up to the current version.
/// </summary>
public static class SaveMigrator
{
    // Register future migrations here, ordered by From ascending.
    private static readonly List<ISaveMigration> _migrations = new()
    {
        new V1ToV2Migration(),
        new V2ToV3Migration(),
        new V3ToV4Migration(),
        new V4ToV5Migration(),
        new V5ToV6Migration(),
        new V6ToV7Migration(),
        new V7ToV8Migration(),
    };

    /// <summary>
    /// Migrates the save in place. Returns false when the file is newer than this build supports
    /// or a required migration step is missing.
    /// </summary>
    public static bool TryMigrate(SaveFileDto dto, out string? error)
    {
        int version = dto.Meta.SaveVersion;
        if (version > SaveYaml.CurrentVersion)
        {
            error = $"Save version {version} is newer than supported version {SaveYaml.CurrentVersion}.";
            return false;
        }

        while (version < SaveYaml.CurrentVersion)
        {
            var step = _migrations.FirstOrDefault(m => m.From == version);
            if (step == null)
            {
                error = $"No migration registered from save version {version}.";
                return false;
            }
            step.Apply(dto);
            version++;
            dto.Meta.SaveVersion = version;
        }

        error = null;
        return true;
    }
}

/// <summary>
/// v1 → v2: introduces logistics-unit persistence. v1 saves predate logistics serialization, so
/// there is nothing to transform — the absent <see cref="SaveFileDto.LogisticsUnits"/> list simply
/// stays null and the loader restores no units.
/// </summary>
public sealed class V1ToV2Migration : ISaveMigration
{
    public int From => 1;

    public SaveFileDto Apply(SaveFileDto dto) => dto;
}

/// <summary>
/// v2 → v3: introduces building/station + transfer persistence. v2 saves predate structure
/// serialization, so the absent <see cref="SaveFileDto.Buildings"/> / <see cref="SaveFileDto.Stations"/>
/// lists simply stay null and the loader restores no structures.
/// </summary>
public sealed class V2ToV3Migration : ISaveMigration
{
    public int From => 2;

    public SaveFileDto Apply(SaveFileDto dto) => dto;
}

/// <summary>
/// v3 → v4: introduces orbital-schedule persistence. v3 saves predate schedule
/// serialization, so the absent <see cref="LogisticsUnitDto.Schedule"/> simply stays
/// null and the loader restores no schedules.
/// </summary>
public sealed class V3ToV4Migration : ISaveMigration
{
    public int From => 3;

    public SaveFileDto Apply(SaveFileDto dto) => dto;
}

/// <summary>
/// v4 → v5: introduces Market Station persistence. v4 saves predate market state, so the absent
/// <see cref="StationDto.MarketState"/> simply stays null and the loader restores no market state.
/// </summary>
public sealed class V4ToV5Migration : ISaveMigration
{
    public int From => 4;

    public SaveFileDto Apply(SaveFileDto dto) => dto;
}

/// <summary>
/// v5 → v6: introduces the game-instance clock (TimeKeeper). v5 saves predate it, so the absent
/// <see cref="SaveFileDto.Time"/> simply defaults to <c>TotalTicks = 0</c> and the clock starts at the
/// beginning of Year 0, Q1.
/// </summary>
public sealed class V5ToV6Migration : ISaveMigration
{
    public int From => 5;

    public SaveFileDto Apply(SaveFileDto dto) => dto;
}

/// <summary>
/// v6 → v7: introduces quarterly revenue tracking on the company. v6 saves have no quarter snapshot, so
/// seed it from the loaded Budget — otherwise the first post-load rollover would report the entire Budget
/// as that quarter's revenue. <see cref="CompanyDto.LastQuarterRevenue"/> stays 0 until a quarter closes.
/// </summary>
public sealed class V6ToV7Migration : ISaveMigration
{
    public int From => 6;

    public SaveFileDto Apply(SaveFileDto dto)
    {
        dto.Company.QuarterOpeningBudget = dto.Company.Budget;
        return dto;
    }
}

/// <summary>
/// v7 → v8: introduces player-assignable extraction slots. v7 saves have no
/// <see cref="BuildingDto.ExtractionSlots"/>, so the absent list stays null and extraction
/// buildings restore their auto-filled (highest-abundance) primary slots.
/// </summary>
public sealed class V7ToV8Migration : ISaveMigration
{
    public int From => 7;

    public SaveFileDto Apply(SaveFileDto dto) => dto;
}
