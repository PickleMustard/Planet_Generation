using System;

namespace Structures.Resources;

public class ResourceValidationError : Exception
{
    public string ResourceId { get; }
    public string BodyConfigName { get; }

    public ResourceValidationError(string message) : base(message)
    {
    }

    public ResourceValidationError(string message, Exception innerException) : base(message, innerException)
    {
    }

    public ResourceValidationError(string resourceId, string bodyConfigName, string message)
        : base($"Resource validation failed for '{resourceId}' in body config '{bodyConfigName}': {message}")
    {
        ResourceId = resourceId;
        BodyConfigName = bodyConfigName;
    }

    public static ResourceValidationError ResourceNotFound(string resourceId, string bodyConfigName)
    {
        return new ResourceValidationError(
            resourceId,
            bodyConfigName,
            $"Resource '{resourceId}' does not exist in the resource database"
        );
    }

    public static ResourceValidationError DuplicateResource(string resourceId)
    {
        return new ResourceValidationError($"Duplicate resource definition found: '{resourceId}'");
    }

    public static ResourceValidationError InvalidResourceDefinition(string resourceId, string reason)
    {
        return new ResourceValidationError($"Invalid resource definition for '{resourceId}': {reason}");
    }
}
