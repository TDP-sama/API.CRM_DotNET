using API.CRM_DotNET.Auth;
using API.CRM_DotNET.Data;
using API.CRM_DotNET.Domain;
using API.CRM_DotNET.Dtos;
using API.CRM_DotNET.Infrastructure;
using API.CRM_DotNET.Services;
using DevExtreme.AspNet.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.CRM_DotNET.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController(CrmDbContext db, InventoryService inventory) : ControllerBase
{
    /// <summary>Stock per product and warehouse. Grid key: [productId, warehouseId].</summary>
    [HttpGet("stock")]
    [Authorize(Policy = Permissions.InventoryView + "|" + Permissions.OrdersView)]
    public async Task<object> Stock(DataSourceLoadOptions loadOptions) =>
        await DataSourceLoader.LoadAsync(db.StockLevels.AsNoTracking().Select(s => new StockLevelDto
        {
            ProductId = s.ProductId,
            Sku = s.Product.Sku,
            ProductName = s.Product.Name,
            CategoryName = s.Product.Category != null ? s.Product.Category.Name : null,
            Unit = s.Product.Unit,
            WarehouseId = s.WarehouseId,
            WarehouseName = s.Warehouse.Name,
            Quantity = s.Quantity,
            Cost = s.Product.Cost,
            StockValue = s.Quantity * s.Product.Cost,
        }), loadOptions);

    /// <summary>Stock per product over all warehouses, with the low-stock flag.</summary>
    [HttpGet("summary")]
    [Authorize(Policy = Permissions.InventoryView)]
    public async Task<object> Summary(DataSourceLoadOptions loadOptions) =>
        await DataSourceLoader.LoadAsync(db.Products.AsNoTracking().Where(p => p.IsActive).Select(p => new
        {
            p,
            qty = db.StockLevels.Where(s => s.ProductId == p.Id).Sum(s => (int?)s.Quantity) ?? 0,
        }).Select(x => new StockSummaryDto
        {
            ProductId = x.p.Id,
            Sku = x.p.Sku,
            ProductName = x.p.Name,
            CategoryName = x.p.Category != null ? x.p.Category.Name : null,
            Unit = x.p.Unit,
            TotalQuantity = x.qty,
            MinStock = x.p.MinStock,
            IsLow = x.qty < x.p.MinStock,
            StockValue = x.qty * x.p.Cost,
        }), loadOptions.WithInMemoryGrouping());

    [HttpGet("transactions")]
    [Authorize(Policy = Permissions.InventoryView)]
    public async Task<object> Transactions(DataSourceLoadOptions loadOptions) =>
        await DataSourceLoader.LoadAsync(db.StockTransactions.AsNoTracking().Select(t => new StockTransactionDto
        {
            Id = t.Id,
            Type = t.Type,
            ProductId = t.ProductId,
            Sku = t.Product.Sku,
            ProductName = t.Product.Name,
            WarehouseId = t.WarehouseId,
            WarehouseName = t.Warehouse.Name,
            Quantity = t.Quantity,
            BalanceAfter = t.BalanceAfter,
            Reference = t.Reference,
            Note = t.Note,
            CreatedAt = t.CreatedAt,
            UserName = t.User != null ? t.User.FullName : null,
        }), loadOptions);

    [HttpPost("import")]
    [Authorize(Policy = Permissions.InventoryManage)]
    public async Task<IActionResult> Import(StockMovementInput input)
    {
        var reference = string.IsNullOrWhiteSpace(input.Reference) ? NewCode("PN") : input.Reference;
        await inventory.RunInTransactionAsync(async () =>
        {
            foreach (var item in input.Items)
                await inventory.MoveAsync(item.ProductId, input.WarehouseId, item.Quantity,
                    StockTransactionType.Import, reference, input.Note, User.GetUserId());
        });
        return Ok(new { reference });
    }

    [HttpPost("export")]
    [Authorize(Policy = Permissions.InventoryManage)]
    public async Task<IActionResult> Export(StockMovementInput input)
    {
        var reference = string.IsNullOrWhiteSpace(input.Reference) ? NewCode("PX") : input.Reference;
        await inventory.RunInTransactionAsync(async () =>
        {
            foreach (var item in input.Items)
                await inventory.MoveAsync(item.ProductId, input.WarehouseId, -item.Quantity,
                    StockTransactionType.Export, reference, input.Note, User.GetUserId());
        });
        return Ok(new { reference });
    }

    [HttpPost("transfer")]
    [Authorize(Policy = Permissions.InventoryManage)]
    public async Task<IActionResult> Transfer(StockTransferInput input)
    {
        if (input.FromWarehouseId == input.ToWarehouseId)
            throw new BusinessException("Kho xuất và kho nhập phải khác nhau.");
        var reference = NewCode("CK");
        await inventory.RunInTransactionAsync(async () =>
        {
            foreach (var item in input.Items)
            {
                await inventory.MoveAsync(item.ProductId, input.FromWarehouseId, -item.Quantity,
                    StockTransactionType.TransferOut, reference, input.Note, User.GetUserId());
                await inventory.MoveAsync(item.ProductId, input.ToWarehouseId, item.Quantity,
                    StockTransactionType.TransferIn, reference, input.Note, User.GetUserId());
            }
        });
        return Ok(new { reference });
    }

    /// <summary>Count sheet for a stocktake: every active product with its current system quantity.</summary>
    [HttpGet("count-sheet")]
    [Authorize(Policy = Permissions.InventoryManage)]
    public async Task<List<CountSheetLineDto>> CountSheet(int warehouseId) =>
        await db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Sku)
            .Select(p => new CountSheetLineDto(p.Id, p.Sku, p.Name, p.Unit,
                db.StockLevels.Where(s => s.ProductId == p.Id && s.WarehouseId == warehouseId)
                    .Select(s => (int?)s.Quantity).FirstOrDefault() ?? 0))
            .ToListAsync();

    /// <summary>Saves a stocktake and posts an Adjustment for every line whose count differs from the system.</summary>
    [HttpPost("counts")]
    [Authorize(Policy = Permissions.InventoryManage)]
    public async Task<IActionResult> CreateCount(StockCountInput input)
    {
        var count = await inventory.RunInTransactionAsync(async () =>
        {
            var count = new StockCount
            {
                Code = NewCode("KK"),
                WarehouseId = input.WarehouseId,
                Note = input.Note,
                UserId = User.GetUserId(),
            };
            foreach (var line in input.Lines.DistinctBy(l => l.ProductId))
            {
                var system = await inventory.GetQuantityAsync(line.ProductId, input.WarehouseId);
                var diff = line.ActualQuantity - system;
                count.Lines.Add(new StockCountLine
                {
                    ProductId = line.ProductId,
                    SystemQuantity = system,
                    ActualQuantity = line.ActualQuantity,
                    Difference = diff,
                });
                if (diff != 0)
                    await inventory.MoveAsync(line.ProductId, input.WarehouseId, diff,
                        StockTransactionType.Adjustment, count.Code, input.Note ?? "Điều chỉnh theo kiểm kho", User.GetUserId());
            }
            db.StockCounts.Add(count);
            await db.SaveChangesAsync();
            return count;
        });
        return Ok(new { count.Id, count.Code, adjusted = count.Lines.Count(l => l.Difference != 0) });
    }

    [HttpGet("counts")]
    [Authorize(Policy = Permissions.InventoryView)]
    public async Task<object> Counts(DataSourceLoadOptions loadOptions) =>
        await DataSourceLoader.LoadAsync(db.StockCounts.AsNoTracking().Select(c => new StockCountDto
        {
            Id = c.Id,
            Code = c.Code,
            WarehouseId = c.WarehouseId,
            WarehouseName = c.Warehouse.Name,
            CountedAt = c.CountedAt,
            Note = c.Note,
            UserName = c.User != null ? c.User.FullName : null,
            LineCount = c.Lines.Count,
            DifferenceCount = c.Lines.Count(l => l.Difference != 0),
        }), loadOptions);

    [HttpGet("counts/{id:int}/lines")]
    [Authorize(Policy = Permissions.InventoryView)]
    public async Task<List<StockCountLineDto>> CountLines(int id) =>
        await db.StockCountLines.AsNoTracking().Where(l => l.StockCountId == id).OrderBy(l => l.Product.Sku)
            .Select(l => new StockCountLineDto(l.Id, l.ProductId, l.Product.Sku, l.Product.Name,
                l.SystemQuantity, l.ActualQuantity, l.Difference))
            .ToListAsync();

    private static string NewCode(string prefix) => $"{prefix}{DateTime.Now:yyMMddHHmmss}{Random.Shared.Next(10, 99)}";
}
