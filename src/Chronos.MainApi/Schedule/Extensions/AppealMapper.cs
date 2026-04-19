using Chronos.Domain.Schedule;
using Chronos.MainApi.Schedule.Contracts;

namespace Chronos.MainApi.Schedule.Extensions;

public static class AppealMapper
{
    public static AppealResponse ToAppealResponse(this Appeal appeal) =>
        new(
            Id: appeal.Id.ToString(),
            OrganizationId: appeal.OrganizationId.ToString(),
            AssignmentId: appeal.AssignmentId.ToString(),
            Title: appeal.Title,
            Description: appeal.Description,
            CreatedAt: appeal.CreatedAt,
            UpdatedAt: appeal.UpdatedAt
        );
}
