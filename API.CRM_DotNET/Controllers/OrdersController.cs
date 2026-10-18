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

/// <summary>
/// Order lifecycle: Draft (editable) → Confirmed (stock deducted) → Completed.
/// Draft or Confirmed orders can be Cancelled; cancelling a Confirmed order returns its stock.
/// </summary>
[ApiController]
[Route("api/orders")]
public class OrdersController(CrmDbContext db, InventoryService inventory) : ControllerBase
{
    private IQueryable<OrderDto> Query() => db.Orders.AsNoTracking().Select(o => new OrderDto
    {
        Id = o.Id,
        Code = o.Code,
        CustomerId = o.CustomerId,
        CustomerName = o.Customer.Name,
        WarehouseId = o.WarehouseId,
        WarehouseName = o.Warehouse.Name,
        OrderDate = o.OrderDate,
        Status = o.Status,
        SubTotal = o.SubTotal,
        Discount = o.Discount,
        Total = o.Total,
        Note = o.Note,
        CreatedByName = o.CreatedBy != null ? o.CreatedBy.FullName : null,
        ItemCount = o.Items.Count,
    });

    [HttpGet]
    [Authorize(Policy = Permissions.OrdersView + "|" + Permissions.CustomersView)]
    public async Task<object> Get(DataSourceLoadOptions loadOptions) =>
        await DataSourceLoader.LoadAsync(Query(), loadOptions);

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.OrdersView)]
    public async Task<ActionResult<OrderDetailDto>> GetById(int id)
    {
        var order = await Query().FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();
        var detail = new OrderDetailDto
        {
            Id = order.Id, Code = order.Code, CustomerId = order.CustomerId, CustomerName = order.CustomerName,
            WarehouseId = order.WarehouseId, WarehouseName = order.WarehouseName, OrderDate = order.OrderDate,
            Status = order.Status, SubTotal = order.SubTotal, Discount = order.Discount, Total = order.Total,
            Note = order.Note, CreatedByName = order.CreatedByName, ItemCount = order.ItemCount,
            Items = await db.OrderItems.AsNoTracking().Where(i => i.OrderId == id).OrderBy(i => i.Id)
                .Select(i => new OrderItemDto(i.Id, i.ProductId, i.Product.Sku, i.Product.Name, i.Product.Unit,
                    i.Quantity, i.UnitPrice, i.LineTotal))
                .ToListAsync(),
        };
        return detail;
    }

    [HttpPost]
    [Authorize(Policy = Permissions.OrdersManage)]
    public async Task<IActionResult> Create(OrderInput input)
    {
        var order = new Order { Code = "TMP-" + Guid.NewGuid().ToString("N"), CreatedById = User.GetUserId() };
        Apply(order, input);
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        order.Code = $"DH{order.Id:D6}";
        await db.SaveChangesAsync();
        return Ok(new { order.Id, order.Code });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.OrdersManage)]
    public async Task<IActionResult> Update(int id, OrderInput input)
    {
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();
        if (order.Status != OrderStatus.Draft) throw new BusinessException("Chỉ sửa được đơn hàng ở trạng thái Nháp.");
        db.OrderItems.RemoveRange(order.Items);
        order.Items.Clear();
        Apply(order, input);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.OrdersManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var order = await db.Orders.FindAsync(id);
        if (order is null) return NotFound();
        if (order.Status != OrderStatus.Draft) throw new BusinessException("Chỉ xoá được đơn hàng ở trạng thái Nháp, hãy huỷ đơn thay vì xoá.");
        db.Orders.Remove(order);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/confirm")]
    [Authorize(Policy = Permissions.OrdersManage)]
    public async Task<IActionResult> Confirm(int id)
    {
        await inventory.RunInTransactionAsync(async () =>
        {
            var order = await LoadAsync(id, OrderStatus.Draft);
            foreach (var item in order.Items)
                await inventory.MoveAsync(item.ProductId, order.WarehouseId, -item.Quantity,
                    StockTransactionType.SaleOrder, order.Code, "Xuất bán theo đơn hàng", User.GetUserId());
            order.Status = OrderStatus.Confirmed;
            await db.SaveChangesAsync();
        });
        return NoContent();
    }

    [HttpPost("{id:int}/complete")]
    [Authorize(Policy = Permissions.OrdersManage)]
    public async Task<IActionResult> Complete(int id)
    {
        var order = await LoadAsync(id, OrderStatus.Confirmed);
        order.Status = OrderStatus.Completed;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = Permissions.OrdersManage)]
    public async Task<IActionResult> Cancel(int id)
    {
        await inventory.RunInTransactionAsync(async () =>
        {
            var order = await LoadAsync(id, OrderStatus.Draft, OrderStatus.Confirmed);
            if (order.Status == OrderStatus.Confirmed)
                foreach (var item in order.Items)
                    await inventory.MoveAsync(item.ProductId, order.WarehouseId, item.Quantity,
                        StockTransactionType.SaleReturn, order.Code, "Hoàn kho do huỷ đơn", User.GetUserId());
            order.Status = OrderStatus.Cancelled;
            await db.SaveChangesAsync();
        });
        return NoContent();
    }

    private async Task<Order> LoadAsync(int id, params OrderStatus[] allowed)
    {
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new BusinessException("Không tìm thấy đơn hàng.");
        if (!allowed.Contains(order.Status))
            throw new BusinessException("Trạng thái hiện tại của đơn hàng không cho phép thao tác này.");
        return order;
    }

    private static void Apply(Order order, OrderInput input)
    {
        order.CustomerId = input.CustomerId;
        order.WarehouseId = input.WarehouseId;
        order.OrderDate = input.OrderDate ?? DateTime.UtcNow;
        order.Note = input.Note;
        foreach (var item in input.Items)
            order.Items.Add(new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                LineTotal = item.Quantity * item.UnitPrice,
            });
        order.SubTotal = order.Items.Sum(i => i.LineTotal);
        order.Discount = Math.Min(input.Discount, order.SubTotal);
        order.Total = order.SubTotal - order.Discount;
    }
}
