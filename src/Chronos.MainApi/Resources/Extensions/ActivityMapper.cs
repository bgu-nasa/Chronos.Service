using Chronos.Domain.Resources;
using Chronos.MainApi.Resources.Contracts;

namespace Chronos.MainApi.Resources.Extensions;

public static class ActivityMapper
{
    public static ActivityResponse ToActivityResponse(this Activity activity) =>
        new(
            Id: activity.Id,
            OrganizationId: activity.OrganizationId,
            SubjectId: activity.SubjectId,
            AssignedUserId: activity.AssignedUserId,
            ActivityType: activity.ActivityType,
            ExpectedStudents: activity.ExpectedStudents,
            Duration: activity.Duration
        );
}

