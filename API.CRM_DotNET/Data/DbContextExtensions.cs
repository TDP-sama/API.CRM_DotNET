using Microsoft.EntityFrameworkCore;

namespace API.CRM_DotNET.Data;

public static class DbContextExtensions
{
    /// <summary>
    /// Runs <paramref name="action"/> in one database transaction. Explicit transactions must run
    /// inside the execution strategy when retry-on-failure is on; on a transient failure the whole
    /// action is re-run, so it must not depend on state left behind by a failed attempt.
    /// </summary>
    public static async Task<T> RunInTransactionAsync<T>(this CrmDbContext db, Func<Task<T>> action)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var result = await action();
            await tx.CommitAsync();
            return result;
        });
    }

    public static Task RunInTransactionAsync(this CrmDbContext db, Func<Task> action) =>
        db.RunInTransactionAsync(async () => { await action(); return true; });
}
