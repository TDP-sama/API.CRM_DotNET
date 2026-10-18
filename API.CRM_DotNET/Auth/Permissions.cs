namespace API.CRM_DotNET.Auth;

/// <summary>
/// Permission codes. Each one is registered as an authorization policy of the same name and
/// issued as a "perm" claim in the JWT. The frontend keeps a copy of these strings
/// (FE.CRM_DotNet/Auth/Permissions.cs); keep both files in sync.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "perm";

    public const string DashboardView = "Dashboard.View";

    public const string CustomersView = "Customers.View";
    public const string CustomersManage = "Customers.Manage";

    public const string ProductsView = "Products.View";
    public const string ProductsManage = "Products.Manage";

    public const string InventoryView = "Inventory.View";
    public const string InventoryManage = "Inventory.Manage";

    public const string OrdersView = "Orders.View";
    public const string OrdersManage = "Orders.Manage";

    public const string OpportunitiesView = "Opportunities.View";
    public const string OpportunitiesManage = "Opportunities.Manage";

    public const string UsersManage = "Users.Manage";

    public record Definition(string Code, string Group, string Name);

    public static readonly IReadOnlyList<Definition> All =
    [
        new(DashboardView, "Tổng quan", "Xem dashboard"),
        new(CustomersView, "Khách hàng", "Xem khách hàng"),
        new(CustomersManage, "Khách hàng", "Thêm/sửa/xoá khách hàng, liên hệ, hoạt động"),
        new(ProductsView, "Sản phẩm", "Xem sản phẩm, danh mục, kho"),
        new(ProductsManage, "Sản phẩm", "Thêm/sửa/xoá sản phẩm, danh mục, kho"),
        new(InventoryView, "Kho hàng", "Xem tồn kho, lịch sử nhập xuất"),
        new(InventoryManage, "Kho hàng", "Nhập/xuất/chuyển kho, kiểm kho"),
        new(OrdersView, "Đơn hàng", "Xem đơn hàng"),
        new(OrdersManage, "Đơn hàng", "Tạo/sửa/duyệt/huỷ đơn hàng"),
        new(OpportunitiesView, "Cơ hội bán hàng", "Xem cơ hội"),
        new(OpportunitiesManage, "Cơ hội bán hàng", "Thêm/sửa/xoá cơ hội"),
        new(UsersManage, "Hệ thống", "Quản lý người dùng và phân quyền"),
    ];
}
