namespace Chronos.MainApi.Resources.Contracts;

public sealed record CreateResourceAttributeRequest(
    Guid OrganizationId,
    string Title,
    string? Description);