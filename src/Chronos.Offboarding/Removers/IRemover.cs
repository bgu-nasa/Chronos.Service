namespace Chronos.Offboarding.Removers;

public interface IRemover
{
    /// <summary>
    /// Atomically hard-deletes an entity and all of its related data.
    /// Returns the total number of rows deleted.
    /// </summary>
    Task<int> RemoveAsync(Guid id, CancellationToken ct = default);
}