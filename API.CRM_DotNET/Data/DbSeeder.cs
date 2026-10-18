using API.CRM_DotNET.Auth;
using API.CRM_DotNET.Domain;
using API.CRM_DotNET.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.CRM_DotNET.Data;

/// <summary>
/// Runs at startup: applies migrations (retrying while SQL Server boots in Docker), then seeds
/// roles/users on an empty database and, if Seed:DemoData is true, sample business data.
/// </summary>
public static class DbSeeder
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<CrmDbContext>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbSeeder));
        var config = sp.GetRequiredService<IConfiguration>();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                break;
            }
            catch (Exception ex) when (attempt < 20)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        if (await db.Users.AnyAsync()) return;

        logger.LogInformation("Seeding roles and users");
        await SeedIdentityAsync(db, sp.GetRequiredService<IPasswordHasher<User>>());
        if (config.GetValue("Seed:DemoData", true))
        {
            logger.LogInformation("Seeding demo data");
            await SeedDemoDataAsync(db, sp.GetRequiredService<InventoryService>());
        }
    }

    private static async Task SeedIdentityAsync(CrmDbContext db, IPasswordHasher<User> hasher)
    {
        Role MakeRole(string name, string description, bool system, params string[] perms) => new()
        {
            Name = name,
            Description = description,
            IsSystem = system,
            Permissions = perms.Select(p => new RolePermission { Permission = p }).ToList(),
        };

        var admin = MakeRole("Admin", "Toàn quyền hệ thống", true, Permissions.All.Select(p => p.Code).ToArray());
        var sales = MakeRole("Kinh doanh", "Nhân viên kinh doanh", false,
            Permissions.DashboardView, Permissions.CustomersView, Permissions.CustomersManage,
            Permissions.OrdersView, Permissions.OrdersManage, Permissions.OpportunitiesView,
            Permissions.OpportunitiesManage, Permissions.ProductsView, Permissions.InventoryView);
        var warehouse = MakeRole("Thủ kho", "Quản lý kho hàng", false,
            Permissions.DashboardView, Permissions.ProductsView, Permissions.ProductsManage,
            Permissions.InventoryView, Permissions.InventoryManage, Permissions.OrdersView);
        db.Roles.AddRange(admin, sales, warehouse);

        User MakeUser(string username, string fullName, string password, Role role)
        {
            var user = new User { Username = username, FullName = fullName, Email = $"{username}@crm.local" };
            user.PasswordHash = hasher.HashPassword(user, password);
            user.UserRoles.Add(new UserRole { Role = role });
            return user;
        }

        db.Users.AddRange(
            MakeUser("admin", "Quản trị viên", "Admin@123", admin),
            MakeUser("sales", "Nguyễn Văn Bán", "Sales@123", sales),
            MakeUser("kho", "Trần Thị Kho", "Kho@123", warehouse));
        await db.SaveChangesAsync();
    }

    private static async Task SeedDemoDataAsync(CrmDbContext db, InventoryService inventory)
    {
        var rnd = new Random(42);
        var users = await db.Users.ToListAsync();
        var salesUser = users.First(u => u.Username == "sales");

        var categories = new[] { "Laptop", "Màn hình", "Phụ kiện", "Linh kiện" }
            .Select(n => new Category { Name = n }).ToList();
        db.Categories.AddRange(categories);

        var products = new List<Product>
        {
            new() { Sku = "LT-001", Name = "Laptop Dell Latitude 5440", Category = categories[0], Price = 22_500_000, Cost = 19_000_000, MinStock = 5 },
            new() { Sku = "LT-002", Name = "Laptop HP ProBook 450 G10", Category = categories[0], Price = 19_900_000, Cost = 16_800_000, MinStock = 5 },
            new() { Sku = "LT-003", Name = "MacBook Air M3 13\"", Category = categories[0], Price = 27_990_000, Cost = 24_500_000, MinStock = 3 },
            new() { Sku = "MN-001", Name = "Màn hình Dell P2422H 24\"", Category = categories[1], Price = 4_590_000, Cost = 3_700_000, MinStock = 10 },
            new() { Sku = "MN-002", Name = "Màn hình LG 27UP850 27\" 4K", Category = categories[1], Price = 9_890_000, Cost = 8_200_000, MinStock = 4 },
            new() { Sku = "PK-001", Name = "Chuột Logitech MX Master 3S", Category = categories[2], Price = 2_490_000, Cost = 1_900_000, MinStock = 15, Unit = "Con" },
            new() { Sku = "PK-002", Name = "Bàn phím Logitech MX Keys", Category = categories[2], Price = 2_690_000, Cost = 2_050_000, MinStock = 15 },
            new() { Sku = "PK-003", Name = "Tai nghe Jabra Evolve2 65", Category = categories[2], Price = 5_200_000, Cost = 4_100_000, MinStock = 8 },
            new() { Sku = "PK-004", Name = "Dock USB-C Dell WD19S", Category = categories[2], Price = 4_350_000, Cost = 3_500_000, MinStock = 6 },
            new() { Sku = "LK-001", Name = "RAM Kingston 16GB DDR5", Category = categories[3], Price = 1_350_000, Cost = 1_050_000, MinStock = 20, Unit = "Thanh" },
            new() { Sku = "LK-002", Name = "SSD Samsung 990 Pro 1TB", Category = categories[3], Price = 2_890_000, Cost = 2_300_000, MinStock = 12, Unit = "Ổ" },
            new() { Sku = "LK-003", Name = "Ổ cứng WD Red 4TB", Category = categories[3], Price = 3_150_000, Cost = 2_600_000, MinStock = 6, Unit = "Ổ" },
        };
        db.Products.AddRange(products);

        var warehouses = new List<Warehouse>
        {
            new() { Code = "HN", Name = "Kho Hà Nội", Address = "Cầu Giấy, Hà Nội" },
            new() { Code = "HCM", Name = "Kho Hồ Chí Minh", Address = "Quận 7, TP. Hồ Chí Minh" },
        };
        db.Warehouses.AddRange(warehouses);

        var companies = new[]
        {
            "Công ty TNHH Minh Phát", "Công ty CP Công nghệ Sao Việt", "Tập đoàn An Khang", "Công ty TNHH Thương mại Hoàng Long",
            "Công ty CP Giải pháp Số Á Châu", "Ngân hàng TMCP Đông Đô", "Công ty TNHH Logistics Tân Cảng", "Trường ĐH Kỹ thuật Miền Nam",
            "Bệnh viện Đa khoa Hồng Đức", "Công ty CP Dược phẩm Thiên Phúc", "Công ty TNHH Kiến trúc Nhà Xanh", "Công ty CP Bất động sản Phú Mỹ",
        };
        var people = new[] { "Lê Hoàng Nam", "Phạm Thu Hà", "Đỗ Minh Tuấn", "Vũ Ngọc Anh" };
        var lastNames = new[] { "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Võ", "Đặng", "Bùi" };
        var firstNames = new[] { "An", "Bình", "Châu", "Dũng", "Giang", "Hải", "Khánh", "Linh", "My", "Quân", "Trang", "Vy" };
        string RandomName() => $"{lastNames[rnd.Next(lastNames.Length)]} {firstNames[rnd.Next(firstNames.Length)]}";

        var customers = companies.Select((name, i) => new Customer { Name = name, Type = CustomerType.Company, TaxCode = $"0{rnd.Next(100000000, 999999999)}" })
            .Concat(people.Select(name => new Customer { Name = name, Type = CustomerType.Individual }))
            .Select((c, i) =>
            {
                c.Code = $"KH{i + 1:D5}";
                c.Status = i % 7 == 6 ? CustomerStatus.Inactive : i % 4 == 3 ? CustomerStatus.Lead : CustomerStatus.Active;
                c.Email = $"lienhe{i + 1}@example.vn";
                c.Phone = $"09{rnd.Next(10000000, 99999999)}";
                c.Address = i % 2 == 0 ? "Hà Nội" : "TP. Hồ Chí Minh";
                c.AssignedToId = i % 3 == 0 ? users[0].Id : salesUser.Id;
                c.CreatedAt = DateTime.UtcNow.AddDays(-rnd.Next(1, 360));
                if (c.Type == CustomerType.Company)
                {
                    c.Contacts.Add(new Contact { FullName = RandomName(), Position = "Giám đốc", Phone = $"09{rnd.Next(10000000, 99999999)}", IsPrimary = true });
                    c.Contacts.Add(new Contact { FullName = RandomName(), Position = "Phụ trách mua hàng", Phone = $"09{rnd.Next(10000000, 99999999)}" });
                }
                c.Activities.Add(new Activity { Type = ActivityType.Call, Subject = "Gọi điện giới thiệu sản phẩm", Date = c.CreatedAt.AddDays(1), UserId = salesUser.Id });
                c.Activities.Add(new Activity { Type = ActivityType.Meeting, Subject = "Gặp mặt trao đổi nhu cầu", Date = c.CreatedAt.AddDays(7), UserId = salesUser.Id });
                return c;
            }).ToList();
        db.Customers.AddRange(customers);
        await db.SaveChangesAsync();

        // Opening stock, dated a year back so it precedes the sample orders.
        foreach (var w in warehouses)
            foreach (var p in products)
                await inventory.MoveAsync(p.Id, w.Id, rnd.Next(p.MinStock * 3, p.MinStock * 8), StockTransactionType.Import, "PN-DAUKY", "Tồn đầu kỳ", users[0].Id);
        await db.StockTransactions.ExecuteUpdateAsync(s => s.SetProperty(t => t.CreatedAt, DateTime.UtcNow.AddMonths(-12)));

        var activeCustomers = customers.Where(c => c.Status != CustomerStatus.Lead).ToList();
        for (var i = 0; i < 70; i++)
        {
            var order = new Order
            {
                Code = $"DH{i + 1:D6}",
                Customer = activeCustomers[rnd.Next(activeCustomers.Count)],
                WarehouseId = warehouses[rnd.Next(warehouses.Count)].Id,
                OrderDate = DateTime.UtcNow.AddDays(-rnd.Next(0, 360)),
                CreatedById = salesUser.Id,
            };
            foreach (var p in products.OrderBy(_ => rnd.Next()).Take(rnd.Next(1, 4)))
            {
                var qty = rnd.Next(1, 4);
                order.Items.Add(new OrderItem { ProductId = p.Id, Quantity = qty, UnitPrice = p.Price, LineTotal = qty * p.Price });
            }
            order.SubTotal = order.Items.Sum(x => x.LineTotal);
            order.Discount = rnd.Next(4) == 0 ? Math.Round(order.SubTotal * 0.05m, 0) : 0;
            order.Total = order.SubTotal - order.Discount;
            var roll = rnd.Next(10);
            order.Status = roll < 6 ? OrderStatus.Completed : roll < 8 ? OrderStatus.Confirmed : roll < 9 ? OrderStatus.Draft : OrderStatus.Cancelled;
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            if (order.Status is OrderStatus.Confirmed or OrderStatus.Completed)
            {
                foreach (var item in order.Items)
                {
                    try
                    {
                        await inventory.MoveAsync(item.ProductId, order.WarehouseId, -item.Quantity, StockTransactionType.SaleOrder, order.Code, "Xuất bán theo đơn hàng", salesUser.Id);
                    }
                    catch (Infrastructure.BusinessException)
                    {
                        // Not enough opening stock for this sample order: top up first.
                        await inventory.MoveAsync(item.ProductId, order.WarehouseId, 20, StockTransactionType.Import, "PN-BOSUNG", "Nhập bổ sung", users[0].Id);
                        await inventory.MoveAsync(item.ProductId, order.WarehouseId, -item.Quantity, StockTransactionType.SaleOrder, order.Code, "Xuất bán theo đơn hàng", salesUser.Id);
                    }
                }
                await db.StockTransactions.Where(t => t.Reference == order.Code)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.CreatedAt, order.OrderDate));
            }
        }

        // Push a couple of products under their minimum so the low-stock warning has something to show.
        foreach (var p in products.Take(2))
            foreach (var w in warehouses)
            {
                var qty = await inventory.GetQuantityAsync(p.Id, w.Id);
                var target = Math.Max(0, p.MinStock / 2 - 1);
                if (qty > target)
                    await inventory.MoveAsync(p.Id, w.Id, target - qty, StockTransactionType.Export, "PX-DEMO", "Xuất dùng nội bộ", users[0].Id);
            }

        var stages = Enum.GetValues<OpportunityStage>();
        var titles = new[] { "Trang bị laptop cho phòng kinh doanh", "Nâng cấp màn hình văn phòng", "Gói phụ kiện hội nghị trực tuyến",
            "Thay thế máy trạm thiết kế", "Mở rộng hệ thống lưu trữ NAS", "Trang bị thiết bị chi nhánh mới" };
        for (var i = 0; i < 18; i++)
        {
            var stage = stages[rnd.Next(stages.Length)];
            db.Opportunities.Add(new Opportunity
            {
                Title = titles[i % titles.Length],
                CustomerId = customers[rnd.Next(customers.Count)].Id,
                Stage = stage,
                Value = rnd.Next(5, 120) * 5_000_000m,
                Probability = Controllers.OpportunitiesController.DefaultProbability(stage),
                ExpectedCloseDate = DateTime.UtcNow.Date.AddDays(rnd.Next(-30, 120)),
                OwnerId = salesUser.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-rnd.Next(1, 120)),
            });
        }
        await db.SaveChangesAsync();
    }
}
