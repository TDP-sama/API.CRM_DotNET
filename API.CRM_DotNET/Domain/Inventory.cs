namespace API.CRM_DotNET.Domain;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public string Unit { get; set; } = "Cái";
    public decimal Price { get; set; }
    public decimal Cost { get; set; }

    /// <summary>Stock (summed over all warehouses) below this level is reported as low.</summary>
    public int MinStock { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Warehouse
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Current quantity of a product in a warehouse. Only changed through InventoryService.</summary>
public class StockLevel
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public int Quantity { get; set; }
}

public enum StockTransactionType
{
    Import = 0,
    Export = 1,
    Adjustment = 2,
    TransferIn = 3,
    TransferOut = 4,
    SaleOrder = 5,
    SaleReturn = 6,
}

/// <summary>Stock ledger entry. Quantity is signed: positive adds stock, negative removes it.</summary>
public class StockTransaction
{
    public int Id { get; set; }
    public StockTransactionType Type { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public int Quantity { get; set; }
    public int BalanceAfter { get; set; }
    public string? Reference { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? UserId { get; set; }
    public User? User { get; set; }
}

/// <summary>A stocktake ("phiếu kiểm kho"): counted quantities compared with system quantities.</summary>
public class StockCount
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public DateTime CountedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public List<StockCountLine> Lines { get; set; } = new();
}

public class StockCountLine
{
    public int Id { get; set; }
    public int StockCountId { get; set; }
    public StockCount StockCount { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int SystemQuantity { get; set; }
    public int ActualQuantity { get; set; }
    public int Difference { get; set; }
}
