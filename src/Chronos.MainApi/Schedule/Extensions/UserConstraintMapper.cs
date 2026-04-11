using Chronos.Domain.Schedule;
using Chronos.MainApi.Schedule.Contracts;

namespace Chronos.MainApi.Schedule.Extensions;

public static class UserConstraintMapper
{
    public static UserConstraintResponse ToUserConstraintResponse(this UserConstraint constraint) =>
        new(
            Id: constraint.Id.ToString(),
            OrganizationId: constraint.OrganizationId.ToString(),
            UserId: constraint.UserId.ToString(),
            SchedulingPeriodId: constraint.SchedulingPeriodId.ToString(),
            WeekNum: constraint.WeekNum,
            Key: constraint.Key,
            Value: constraint.Value
        );
}
