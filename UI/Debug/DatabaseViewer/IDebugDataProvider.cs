#if DEBUG
namespace UI.Debug.DatabaseViewer;

/// <summary>
/// Extended interface for objects tagged with [DebugData] attribute.
/// Provides additional methods specific to tagged debug objects.
/// </summary>
public interface IDebugDataProvider : IDataProvider
{
    /// <summary>
    /// The source object that provides this debug data.
    /// Used for targeting instance-specific operations.
    /// </summary>
    object SourceObject { get; }

    /// <summary>
    /// The unique namespace identifier for this provider instance.
    /// Format: TypeName.InstanceName or TypeName.SequentialId
    /// </summary>
    string InstanceNamespace { get; }

    /// <summary>
    /// Whether this provider's source object is still valid.
    /// Used to detect if the source object has been disposed or destroyed.
    /// </summary>
    bool IsSourceValid { get; }
}
#endif
