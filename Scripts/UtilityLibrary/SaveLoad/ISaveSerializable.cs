namespace UtilityLibrary.SaveLoad;

/// <summary>
/// Save side — implemented by ALL savable objects (category A singletons + category B scene-graph
/// entities). <see cref="Serialize"/> returns one of the existing POCO DTO types; the saver discovers
/// implementors (via the "save_serializable" Godot group for node singletons) and slots each result
/// into the <c>SaveFileDto</c> by <see cref="SaveKey"/>. No on-disk format change.
/// </summary>
public interface ISaveSerializable
{
    /// <summary>Stable section/type discriminator used to route the serialized DTO into the save file.</summary>
    string SaveKey { get; }

    /// <summary>Snapshots this object's state into an existing DTO type.</summary>
    object Serialize();
}

/// <summary>
/// Load side — implemented ONLY by self-contained singletons that restore into a pre-existing live
/// instance (e.g. trackers created by GameScene._Ready). Scene-graph entities deliberately do NOT
/// implement this: they are topologically rebuilt from DTOs by <c>SaveLoader.BuildSystem</c>, which an
/// instance method cannot do (it cannot instantiate its own subtype, enforce pass ordering, or resolve
/// cross-references). Restore runs in <c>SaveLoader.RestoreSession</c>, after the ordered build.
/// </summary>
public interface ISaveRestorable
{
    /// <summary>Stable section/type discriminator matching the saved DTO this object consumes.</summary>
    string SaveKey { get; }

    /// <summary>Overwrites this object's live state from a previously serialized DTO.</summary>
    void Restore(object dto);
}
