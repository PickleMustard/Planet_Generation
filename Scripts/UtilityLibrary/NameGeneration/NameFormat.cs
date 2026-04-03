namespace UtilityLibrary.NameGeneration;

/// <summary>
/// Defines different formatting options for generated names.
/// </summary>
public enum NameFormat
{
    /// <summary>
    /// Name as-is from the name list
    /// </summary>
    Default,
    
    /// <summary>
    /// Name converted to lowercase
    /// </summary>
    LowerCase,
    
    /// <summary>
    /// Name formatted in title case (first letter of each word capitalized)
    /// </summary>
    TitleCase,
    
    /// <summary>
    /// Remove all whitespace from the name
    /// </summary>
    NoSpaces,
    
    /// <summary>
    /// Replace spaces with hyphens
    /// </summary>
    Hyphenated,
    
    /// <summary>
    /// Remove all whitespace and convert to lowercase
    /// </summary>
    NoSpacesLowerCase
}