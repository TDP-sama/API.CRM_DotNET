using API.CRM_DotNET.Data;
using API.CRM_DotNET.Domain;
using API.CRM_DotNET.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace API.CRM_DotNET.Services;

/// <summary>
/// The only place that changes StockLevel. Every change also writes a StockTransaction.
/// Callers wrap multi-line operations in a transaction (see RunInTransactionAsync) so a failed
/// line rolls back the whole operation.
/// </summary>
public class InventoryService(CrmDbContext db)
{
    public Task<T> RunInTransactionAsync<T>(Func<Task<T>> action) => db.RunInTransactionAsync(action);

    public Task RunInTransactionAsync(Func<Task> action) => db.RunInTransactionAsync(action);

    /// <summary>
    /// Adds <paramref name="delta"/> (negative to remove) to a product's stock in a warehouse.
    /// The decrement is a single conditional UPDATE, so concurrent requests cannot take stock below zero.
    /// </summary>
    public async Task<int> MoveAsync(int productId, int warehouseId, int delta, StockTransactionType type,
        string? reference, string? note, int? userId)
    {
        if (delta == 0) return await GetQuantityAsync(productId, warehouseId);

        var level = db.StockLevels.Where(s => s.ProductId == productId && s.WarehouseId == warehouseId);
        int updated = delta > 0
            ? await level.ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, x => x.Quantity + delta))
            : await level.Where(s => s.Quantity >= -delta)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, x => x.Quantity + delta));

        if (updated == 0)
        {
            if (delta < 0)
            {
                var product = await db.Products.Where(p => p.Id == productId).Select(p => p.Name).FirstOrDefaultAsync();
                var available = await GetQuantityAsync(productId, warehouseId);
                throw new BusinessException($"Không đủ tồn kho cho \"{product}\": còn {available}, cần {-delta}.");
            }
            db.StockLevels.Add(new StockLevel { ProductId = productId, WarehouseId = warehouseId, Quantity = delta });
            await db.SaveChangesAsync();
        }

        var balance = await GetQuantityAsync(productId, warehouseId);
        db.StockTransactions.Add(new StockTransaction
        {
            Type = type,
            ProductId = productId,
            WarehouseId = warehouseId,
            Quantity = delta,
            BalanceAfter = balance,
            Reference = reference,
            Note = note,
            UserId = userId,
        });
        await db.SaveChangesAsync();
        return balance;
    }

    public async Task<int> GetQuantityAsync(int productId, int warehouseId) =>
        await db.StockLevels.AsNoTracking()
            .Where(s => s.ProductId == productId && s.WarehouseId == warehouseId)
            .Select(s => (int?)s.Quantity)
            .FirstOrDefaultAsync() ?? 0;
}
