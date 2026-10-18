using API.CRM_DotNET.Auth;
using API.CRM_DotNET.Data;
using API.CRM_DotNET.Domain;
using API.CRM_DotNET.Dtos;
using DevExtreme.AspNet.Data;
using API.CRM_DotNET.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.CRM_DotNET.Controllers;

// GET endpoints also accept the inventory/order permissions because those screens use
// products, categories and warehouses as lookups.

[ApiController]
[Route("api/products")]
public class ProductsController(CrmDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.ProductsView + "|" + Permissions.InventoryView + "|" + Permissions.OrdersView)]
    public async Task<object> Get(DataSourceLoadOptions loadOptions) =>
        await DataSourceLoader.LoadAsync(db.Products.AsNoTracking().Select(p => new ProductDto
        {
            Id = p.Id,
            Sku = p.Sku,
            Name = p.Name,
            CategoryId = p.CategoryId,
            CategoryName = p.Category != null ? p.Category.Name : null,
            Unit = p.Unit,
            Price = p.Price,
            Cost = p.Cost,
            MinStock = p.MinStock,
            IsActive = p.IsActive,
            TotalStock = db.StockLevels.Where(s => s.ProductId == p.Id).Sum(s => (int?)s.Quantity) ?? 0,
        }), loadOptions.WithInMemoryGrouping());

    [HttpPost]
    [Authorize(Policy = Permissions.ProductsManage)]
    public async Task<IActionResult> Create(ProductInput input)
    {
        var product = new Product();
        Apply(product, input);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return Ok(new { product.Id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.ProductsManage)]
    public async Task<IActionResult> Update(int id, ProductInput input)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return NotFound();
        Apply(product, input);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.ProductsManage)]
    public async Task<IActionResult> Delete(int id)
    {
        if (await db.StockTransactions.AnyAsync(t => t.ProductId == id) || await db.OrderItems.AnyAsync(i => i.ProductId == id))
            throw new BusinessException("Sản phẩm đã phát sinh giao dịch, hãy chuyển sang Ngừng kinh doanh thay vì xoá.");
        await db.StockLevels.Where(s => s.ProductId == id).ExecuteDeleteAsync();
        await db.Products.Where(p => p.Id == id).ExecuteDeleteAsync();
        return NoContent();
    }

    [HttpPost("import")]
    [Authorize(Policy = Permissions.ProductsManage)]
    public async Task<ImportResult> Import(ImportRequest<ProductImportRow> request)
    {
        var rows = request.Rows;
        var duplicate = rows.GroupBy(r => r.Sku.Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new BusinessException($"Mã sản phẩm \"{duplicate.Key}\" bị lặp trong file.");
        var skus = rows.Select(r => r.Sku.Trim()).ToList();
        var categoryNames = rows.Select(r => r.CategoryName?.Trim()).OfType<string>().Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        return await db.RunInTransactionAsync(async () =>
        {
            var categories = await db.Categories.Where(c => categoryNames.Contains(c.Name))
                .ToDictionaryAsync(c => c.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var name in categoryNames.Where(n => !categories.ContainsKey(n)))
                db.Categories.Add(categories[name] = new Category { Name = name });
            await db.SaveChangesAsync();

            var existing = await db.Products.Where(p => skus.Contains(p.Sku))
                .ToDictionaryAsync(p => p.Sku, StringComparer.OrdinalIgnoreCase);
            int created = 0, updated = 0;
            foreach (var row in rows)
            {
                var category = row.CategoryName?.Trim();
                row.CategoryId = string.IsNullOrEmpty(category) ? null : categories[category].Id;
                if (existing.TryGetValue(row.Sku.Trim(), out var product))
                    updated++;
                else
                {
                    db.Products.Add(product = new Product());
                    created++;
                }
                Apply(product, row);
            }
            await db.SaveChangesAsync();
            return new ImportResult(created, updated);
        });
    }

    /// <summary>Resolves SKUs typed in an Excel file to products (unknown SKUs are simply absent from the result).</summary>
    [HttpPost("by-skus")]
    [Authorize(Policy = Permissions.ProductsView + "|" + Permissions.InventoryView + "|" + Permissions.OrdersView)]
    public async Task<object> BySkus(List<string> skus)
    {
        var wanted = skus.Select(s => s.Trim()).Where(s => s.Length > 0).Distinct().Take(5000).ToList();
        return await db.Products.AsNoTracking().Where(p => wanted.Contains(p.Sku))
            .Select(p => new { p.Id, p.Sku, p.Name, p.Unit, p.Price, p.IsActive })
            .ToListAsync();
    }

    private static void Apply(Product p, ProductInput input)
    {
        p.Sku = input.Sku.Trim();
        p.Name = input.Name.Trim();
        p.CategoryId = input.CategoryId;
        p.Unit = input.Unit.Trim();
        p.Price = input.Price;
        p.Cost = input.Cost;
        p.MinStock = input.MinStock;
        p.IsActive = input.IsActive;
    }
}

[ApiController]
[Route("api/categories")]
public class CategoriesController(CrmDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.ProductsView + "|" + Permissions.InventoryView)]
    public async Task<object> Get(DataSourceLoadOptions loadOptions) =>
        await DataSourceLoader.LoadAsync(db.Categories.AsNoTracking().Select(c => new
        {
            c.Id,
            c.Name,
            c.Description,
            ProductCount = db.Products.Count(p => p.CategoryId == c.Id),
        }), loadOptions.WithInMemoryGrouping());

    [HttpPost]
    [Authorize(Policy = Permissions.ProductsManage)]
    public async Task<IActionResult> Create(CategoryInput input)
    {
        var category = new Category { Name = input.Name.Trim(), Description = input.Description };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return Ok(new { category.Id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.ProductsManage)]
    public async Task<IActionResult> Update(int id, CategoryInput input)
    {
        var category = await db.Categories.FindAsync(id);
        if (category is null) return NotFound();
        category.Name = input.Name.Trim();
        category.Description = input.Description;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.ProductsManage)]
    public async Task<IActionResult> Delete(int id)
    {
        await db.Categories.Where(c => c.Id == id).ExecuteDeleteAsync();
        return NoContent();
    }
}

[ApiController]
[Route("api/warehouses")]
public class WarehousesController(CrmDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.ProductsView + "|" + Permissions.InventoryView + "|" + Permissions.OrdersView)]
    public async Task<object> Get(DataSourceLoadOptions loadOptions) =>
        await DataSourceLoader.LoadAsync(db.Warehouses.AsNoTracking().Select(w => new
        {
            w.Id,
            w.Code,
            w.Name,
            w.Address,
            w.IsActive,
            TotalQuantity = db.StockLevels.Where(s => s.WarehouseId == w.Id).Sum(s => (int?)s.Quantity) ?? 0,
            StockValue = db.StockLevels.Where(s => s.WarehouseId == w.Id).Sum(s => (decimal?)(s.Quantity * s.Product.Cost)) ?? 0,
        }), loadOptions.WithInMemoryGrouping());

    [HttpPost]
    [Authorize(Policy = Permissions.ProductsManage)]
    public async Task<IActionResult> Create(WarehouseInput input)
    {
        var warehouse = new Warehouse();
        Apply(warehouse, input);
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();
        return Ok(new { warehouse.Id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.ProductsManage)]
    public async Task<IActionResult> Update(int id, WarehouseInput input)
    {
        var warehouse = await db.Warehouses.FindAsync(id);
        if (warehouse is null) return NotFound();
        Apply(warehouse, input);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.ProductsManage)]
    public async Task<IActionResult> Delete(int id)
    {
        if (await db.StockTransactions.AnyAsync(t => t.WarehouseId == id) || await db.Orders.AnyAsync(o => o.WarehouseId == id))
            throw new BusinessException("Kho đã phát sinh giao dịch, hãy ngừng sử dụng thay vì xoá.");
        await db.StockLevels.Where(s => s.WarehouseId == id).ExecuteDeleteAsync();
        await db.Warehouses.Where(w => w.Id == id).ExecuteDeleteAsync();
        return NoContent();
    }

    private static void Apply(Warehouse w, WarehouseInput input)
    {
        w.Code = input.Code.Trim();
        w.Name = input.Name.Trim();
        w.Address = input.Address;
        w.IsActive = input.IsActive;
    }
}
