using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Schedule;

namespace Chronos.Tests.Engine.TestFixtures;

/// <summary>
/// In-memory assignment store for specification tests.
/// </summary>
public sealed class FakeAssignmentRepository : IAssignmentRepository
{
    private readonly List<Assignment> _assignments = [];
    private HashSet<Guid> _periodSlotIds = [];

    public IReadOnlyList<Assignment> Assignments => _assignments;

    public void SetSchedulingPeriodSlotIds(IEnumerable<Guid> slotIds) =>
        _periodSlotIds = slotIds.ToHashSet();

    public Task<Assignment?> GetByIdAsync(Guid id) =>
        Task.FromResult(_assignments.FirstOrDefault(a => a.Id == id));

    public Task<List<Assignment>> GetAllAsync(AssignmentQuery? query = null)
    {
        var q = _assignments.AsEnumerable();

        if (query?.OrganizationId is Guid orgId)
            q = q.Where(a => a.OrganizationId == orgId);
        if (query?.SlotId is Guid slotId)
            q = q.Where(a => a.SlotId == slotId);
        if (query?.ResourceId is Guid resourceId)
            q = q.Where(a => a.ResourceId == resourceId);
        if (query?.ActivityId is Guid activityId)
            q = q.Where(a => a.ActivityId == activityId);
        if (query?.WeekNum is int weekNum)
            q = q.Where(a => a.WeekNum == weekNum);

        return Task.FromResult(q.ToList());
    }

    public Task<List<Assignment>> GetBySlotIdAsync(Guid slotId) =>
        Task.FromResult(_assignments.Where(a => a.SlotId == slotId).ToList());

    public Task<List<Assignment>> GetByActivityIdAsync(Guid activityId) =>
        Task.FromResult(_assignments.Where(a => a.ActivityId == activityId).ToList());

    public Task<Assignment?> GetBySlotIdAndResourceIdAsync(Guid slotId, Guid resourceId, int? weekNum = null)
    {
        var q = _assignments.Where(a => a.SlotId == slotId && a.ResourceId == resourceId);
        if (weekNum.HasValue)
            q = q.Where(a => a.WeekNum == weekNum.Value);
        return Task.FromResult(q.FirstOrDefault());
    }

    public Task<List<Assignment>> GetByResourceIdAsync(Guid resourceId) =>
        Task.FromResult(_assignments.Where(a => a.ResourceId == resourceId).ToList());

    public Task<List<Assignment>> GetBySchedulingPeriodIdAsync(Guid schedulingPeriodId) =>
        Task.FromResult(
            _assignments.Where(a => _periodSlotIds.Contains(a.SlotId)).ToList()
        );

    public Task AddAsync(Assignment assignment)
    {
        _assignments.Add(assignment);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Assignment assignment)
    {
        var index = _assignments.FindIndex(a => a.Id == assignment.Id);
        if (index >= 0)
            _assignments[index] = assignment;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Assignment assignment)
    {
        _assignments.RemoveAll(a => a.Id == assignment.Id);
        return Task.CompletedTask;
    }

    public Task DeleteBySchedulingPeriodIdAsync(Guid schedulingPeriodId)
    {
        _assignments.RemoveAll(a => _periodSlotIds.Contains(a.SlotId));
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(Guid id) =>
        Task.FromResult(_assignments.Any(a => a.Id == id));

    public Task<int> DeleteAllByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        var count = _assignments.RemoveAll(a => a.OrganizationId == organizationId);
        return Task.FromResult(count);
    }

    public Task<int> DeleteAllByDepartmentIdAsync(Guid departmentId, CancellationToken ct = default) =>
        Task.FromResult(0);

    public void Clear() => _assignments.Clear();

    public void Seed(IEnumerable<Assignment> assignments) => _assignments.AddRange(assignments);
}
