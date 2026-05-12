namespace Chronos.MainApi.Resources.Contracts;

public sealed record CreateResourceTypeRequest(
    Guid OrganizationId,
    string Type);