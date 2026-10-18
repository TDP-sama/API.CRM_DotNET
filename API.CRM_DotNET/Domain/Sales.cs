namespace API.CRM_DotNET.Domain;

public enum OrderStatus { Draft = 0, Confirmed = 1, Completed = 2, Cancelled = 3 }

public class Order
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public string? Note { get; set; }
    public int? CreatedById { get; set; }
    public User? CreatedBy { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public enum OpportunityStage { New = 0, Qualified = 1, Proposal = 2, Negotiation = 3, Won = 4, Lost = 5 }

public class Opportunity
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public OpportunityStage Stage { get; set; }
    public decimal Value { get; set; }
    public int Probability { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public int? OwnerId { get; set; }
    public User? Owner { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
