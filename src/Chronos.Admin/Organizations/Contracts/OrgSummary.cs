namespace Chronos.Admin.Organizations.Contracts;

public record OrgSummary(
    Guid OrganizationId,
    string Name,
    IReadOnlyList<string> AdminEmails,
    int UserCount,
    DateTime CreatedAt);
