using System.ComponentModel.DataAnnotations;
using API.CRM_DotNET.Domain;

namespace API.CRM_DotNET.Dtos;

// Read DTOs are flat so DevExtreme grids can sort/filter/group on any column server-side.
// Input DTOs carry only editable fields; extra JSON properties sent by the grid are ignored.

// ---------- Excel import ----------

/// <summary>Rows parsed from an Excel file by the frontend. Imports are all-or-nothing.</summary>
public class ImportRequest<T>
{
    [MinLength(1), MaxLength(5000)] public List<T> Rows { get; set; } = new();
}

public record ImportResult(int Created, int Updated);

/// <summary>Upserts by Code: an existing code updates that customer, an empty code creates one with an auto code.</summary>
public class CustomerImportRow : CustomerInput
{
    /// <summary>Username of the account manager. Empty keeps the current one when updating.</summary>
    public string? AssignedToUsername { get; set; }
}

/// <summary>Upserts by Sku. CategoryName is matched by name; missing categories are created.</summary>
public class ProductImportRow : ProductInput
{
    public string? CategoryName { get; set; }
}

// ---------- Auth & users ----------

public record LoginRequest([Required] string Username, [Required] string Password);

public record ChangePasswordRequest([Required] string CurrentPassword, [Required, MinLength(6)] string NewPassword);

public record CurrentUserDto(int Id, string Username, string FullName, string? Email, string[] Roles, string[] Permissions);

public record LoginResponse(string Token, DateTime ExpiresAt, CurrentUserDto User);

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<int> RoleIds { get; set; } = new();
    public string RoleNames { get; set; } = "";
}

public class UserInput
{
    [Required, StringLength(100)] public string Username { get; set; } = "";
    [Required, StringLength(200)] public string FullName { get; set; } = "";
    [OptionalEmail] public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public List<int> RoleIds { get; set; } = new();

    /// <summary>Required when creating; when updating, leave empty to keep the current password.</summary>
    public string? Password { get; set; }
}

public class RoleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public int UserCount { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public class RoleInput
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = new();
}

// ---------- Customers ----------

public class CustomerDto
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public CustomerType Type { get; set; }
    public CustomerStatus Status { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? TaxCode { get; set; }
    public string? Notes { get; set; }
    public int? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CustomerInput
{
    public string? Code { get; set; }
    [Required, StringLength(300)] public string Name { get; set; } = "";
    public CustomerType Type { get; set; }
    public CustomerStatus Status { get; set; }
    [OptionalEmail] public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? TaxCode { get; set; }
    public string? Notes { get; set; }
    public int? AssignedToId { get; set; }
}

public class ContactDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Position { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
}

public class ContactInput
{
    [Required] public int CustomerId { get; set; }
    [Required, StringLength(200)] public string FullName { get; set; } = "";
    public string? Position { get; set; }
    [OptionalEmail] public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
}

public class ActivityDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public ActivityType Type { get; set; }
    public string Subject { get; set; } = "";
    public string? Content { get; set; }
    public DateTime Date { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
}

public class ActivityInput
{
    [Required] public int CustomerId { get; set; }
    public ActivityType Type { get; set; }
    [Required, StringLength(300)] public string Subject { get; set; } = "";
    public string? Content { get; set; }
    public DateTime? Date { get; set; }
}

// ---------- Products & warehouses ----------

public class CategoryInput
{
    [Required, StringLength(200)] public string Name { get; set; } = "";
    public string? Description { get; set; }
}

public class ProductDto
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string Unit { get; set; } = "";
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public int MinStock { get; set; }
    public bool IsActive { get; set; }
    public int TotalStock { get; set; }
}

public class ProductInput
{
    [Required, StringLength(50)] public string Sku { get; set; } = "";
    [Required, StringLength(300)] public string Name { get; set; } = "";
    public int? CategoryId { get; set; }
    [Required, StringLength(50)] public string Unit { get; set; } = "Cái";
    [Range(0, double.MaxValue)] public decimal Price { get; set; }
    [Range(0, double.MaxValue)] public decimal Cost { get; set; }
    [Range(0, int.MaxValue)] public int MinStock { get; set; }
    public bool IsActive { get; set; } = true;
}

public class WarehouseInput
{
    [Required, StringLength(50)] public string Code { get; set; } = "";
    [Required, StringLength(200)] public string Name { get; set; } = "";
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

// ---------- Inventory ----------

public class StockLevelDto
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string? CategoryName { get; set; }
    public string Unit { get; set; } = "";
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Cost { get; set; }
    public decimal StockValue { get; set; }
}

public class StockSummaryDto
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string? CategoryName { get; set; }
    public string Unit { get; set; } = "";
    public int TotalQuantity { get; set; }
    public int MinStock { get; set; }
    public bool IsLow { get; set; }
    public decimal StockValue { get; set; }
}

public class StockTransactionDto
{
    public int Id { get; set; }
    public StockTransactionType Type { get; set; }
    public int ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string ProductName { get; set; } = "";
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = "";
    public int Quantity { get; set; }
    public int BalanceAfter { get; set; }
    public string? Reference { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? UserName { get; set; }
}

public record StockLineInput([Required] int ProductId, [Range(1, int.MaxValue)] int Quantity);

public class StockMovementInput
{
    [Required] public int WarehouseId { get; set; }
    public string? Reference { get; set; }
    public string? Note { get; set; }
    [MinLength(1)] public List<StockLineInput> Items { get; set; } = new();
}

public class StockTransferInput
{
    [Required] public int FromWarehouseId { get; set; }
    [Required] public int ToWarehouseId { get; set; }
    public string? Note { get; set; }
    [MinLength(1)] public List<StockLineInput> Items { get; set; } = new();
}

public record CountSheetLineDto(int ProductId, string Sku, string ProductName, string Unit, int SystemQuantity);

public record StockCountLineInput([Required] int ProductId, [Range(0, int.MaxValue)] int ActualQuantity);

public class StockCountInput
{
    [Required] public int WarehouseId { get; set; }
    public string? Note { get; set; }
    [MinLength(1)] public List<StockCountLineInput> Lines { get; set; } = new();
}

public class StockCountDto
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = "";
    public DateTime CountedAt { get; set; }
    public string? Note { get; set; }
    public string? UserName { get; set; }
    public int LineCount { get; set; }
    public int DifferenceCount { get; set; }
}

public record StockCountLineDto(int Id, int ProductId, string Sku, string ProductName, int SystemQuantity, int ActualQuantity, int Difference);

// ---------- Orders & opportunities ----------

public class OrderDto
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public OrderStatus Status { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public string? Note { get; set; }
    public string? CreatedByName { get; set; }
    public int ItemCount { get; set; }
}

public record OrderItemDto(int Id, int ProductId, string Sku, string ProductName, string Unit, int Quantity, decimal UnitPrice, decimal LineTotal);

public class OrderDetailDto : OrderDto
{
    public List<OrderItemDto> Items { get; set; } = new();
}

public record OrderItemInput([Required] int ProductId, [Range(1, int.MaxValue)] int Quantity, [Range(0, double.MaxValue)] decimal UnitPrice);

public class OrderInput
{
    [Required] public int CustomerId { get; set; }
    [Required] public int WarehouseId { get; set; }
    public DateTime? OrderDate { get; set; }
    [Range(0, double.MaxValue)] public decimal Discount { get; set; }
    public string? Note { get; set; }
    [MinLength(1)] public List<OrderItemInput> Items { get; set; } = new();
}

public class OpportunityDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public OpportunityStage Stage { get; set; }
    public decimal Value { get; set; }
    public int Probability { get; set; }
    public decimal WeightedValue { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public int? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OpportunityInput
{
    [Required, StringLength(300)] public string Title { get; set; } = "";
    [Required] public int CustomerId { get; set; }
    public OpportunityStage Stage { get; set; }
    [Range(0, double.MaxValue)] public decimal Value { get; set; }
    [Range(0, 100)] public int? Probability { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public int? OwnerId { get; set; }
    public string? Note { get; set; }
}

/// <summary>Like [EmailAddress] but accepts an empty string (grids send "" when a field is cleared).</summary>
public class OptionalEmailAttribute : ValidationAttribute
{
    private static readonly EmailAddressAttribute Inner = new();

    public OptionalEmailAttribute() : base("Email không hợp lệ.") { }

    public override bool IsValid(object? value) => value is not string s || s.Length == 0 || Inner.IsValid(s);
}
