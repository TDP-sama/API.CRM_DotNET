using API.CRM_DotNET.Auth;
using API.CRM_DotNET.Data;
using API.CRM_DotNET.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.CRM_DotNET.Controllers;

/// <summary>All dashboard data in one call. Revenue counts Confirmed and Completed orders.</summary>
[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = Permissions.DashboardView)]
public class DashboardController(CrmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<object> Get()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearAgo = monthStart.AddMonths(-11);
        var sales = db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Completed);

        var monthly = await sales.Where(o => o.OrderDate >= yearAgo)
            .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Revenue = g.Sum(o => o.Total), Orders = g.Count() })
            .ToListAsync();
        // Fill months without orders so the chart has a continuous axis.
        var revenueByMonth = Enumerable.Range(0, 12).Select(i => yearAgo.AddMonths(i)).Select(m =>
        {
            var row = monthly.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month);
            return new { month = $"{m.Month:D2}/{m.Year}", revenue = row?.Revenue ?? 0, orders = row?.Orders ?? 0 };
        }).ToList();

        var openStages = new[] { OpportunityStage.New, OpportunityStage.Qualified, OpportunityStage.Proposal, OpportunityStage.Negotiation };
        var opportunitiesByStage = await db.Opportunities.AsNoTracking()
            .GroupBy(o => o.Stage)
            .Select(g => new { stage = g.Key, count = g.Count(), value = g.Sum(o => o.Value) })
            .ToListAsync();

        var topProducts = await db.OrderItems.AsNoTracking()
            .Where(i => (i.Order.Status == OrderStatus.Confirmed || i.Order.Status == OrderStatus.Completed) && i.Order.OrderDate >= yearAgo)
            .GroupBy(i => i.Product.Name)
            .Select(g => new { product = g.Key, quantity = g.Sum(i => i.Quantity), revenue = g.Sum(i => i.LineTotal) })
            .OrderByDescending(x => x.revenue).Take(5)
            .ToListAsync();

        var stockByWarehouse = await db.StockLevels.AsNoTracking()
            .GroupBy(s => s.Warehouse.Name)
            .Select(g => new { warehouse = g.Key, quantity = g.Sum(s => s.Quantity), value = g.Sum(s => s.Quantity * s.Product.Cost) })
            .ToListAsync();

        var customersByStatus = await db.Customers.AsNoTracking()
            .GroupBy(c => c.Status)
            .Select(g => new { status = g.Key, count = g.Count() })
            .ToListAsync();

        var lowStock = await db.Products.AsNoTracking().Where(p => p.IsActive)
            .Select(p => new { p.Id, p.Sku, p.Name, p.Unit, p.MinStock, Quantity = db.StockLevels.Where(s => s.ProductId == p.Id).Sum(s => (int?)s.Quantity) ?? 0 })
            .Where(p => p.Quantity < p.MinStock)
            .OrderBy(p => p.Quantity)
            .Take(10)
            .ToListAsync();

        var kpis = new
        {
            totalCustomers = await db.Customers.CountAsync(),
            newCustomersThisMonth = await db.Customers.CountAsync(c => c.CreatedAt >= monthStart),
            revenueThisMonth = await sales.Where(o => o.OrderDate >= monthStart).SumAsync(o => (decimal?)o.Total) ?? 0,
            revenueLastMonth = await sales.Where(o => o.OrderDate >= monthStart.AddMonths(-1) && o.OrderDate < monthStart).SumAsync(o => (decimal?)o.Total) ?? 0,
            ordersThisMonth = await sales.CountAsync(o => o.OrderDate >= monthStart),
            pendingOrders = await db.Orders.CountAsync(o => o.Status == OrderStatus.Draft),
            openOpportunities = opportunitiesByStage.Where(x => openStages.Contains(x.stage)).Sum(x => x.count),
            pipelineValue = opportunitiesByStage.Where(x => openStages.Contains(x.stage)).Sum(x => x.value),
            stockValue = stockByWarehouse.Sum(x => x.value),
            lowStockCount = await db.Products.CountAsync(p => p.IsActive &&
                (db.StockLevels.Where(s => s.ProductId == p.Id).Sum(s => (int?)s.Quantity) ?? 0) < p.MinStock),
        };

        return new { kpis, revenueByMonth, opportunitiesByStage, topProducts, stockByWarehouse, customersByStatus, lowStock };
    }
}
