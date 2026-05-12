namespace Chronos.MainApi.Resources.Contracts;

public sealed record CreateResourceRequest(
    Guid OrganizationId,
    Guid ResourceTypeId,
    string Location,
    string Identifier,
    int? Capacity);