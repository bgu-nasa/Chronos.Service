using Chronos.Domain.Schedule;
using Chronos.MainApi.Schedule.Contracts;

namespace Chronos.MainApi.Schedule.Extensions;

public static class AssignmentMapper
{
    public static AssignmentResponse ToAssignmentResponse(this Assignment assignment) =>
        new(
            Id: assignment.Id.ToString(),
            OrganizationId: assignment.OrganizationId.ToString(),
            SlotId: assignment.SlotId.ToString(),
            ResourceId: assignment.ResourceId.ToString(),
            ActivityId: assignment.ActivityId.ToString(),
            WeekNum: assignment.WeekNum
        );
}
