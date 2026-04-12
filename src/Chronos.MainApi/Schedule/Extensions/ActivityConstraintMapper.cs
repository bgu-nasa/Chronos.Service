using Chronos.Domain.Schedule;
using Chronos.MainApi.Schedule.Contracts;

namespace Chronos.MainApi.Schedule.Extensions;

public static class ActivityConstraintMapper
{
    public static ActivityConstraintResponse ToActivityConstraintResponse(this ActivityConstraint constraint) =>
        new(
            Id: constraint.Id.ToString(),
            ActivityId: constraint.ActivityId.ToString(),
            OrganizationId: constraint.OrganizationId.ToString(),
            WeekNum: constraint.WeekNum,
            Key: constraint.Key,
            Value: constraint.Value
        );
}
