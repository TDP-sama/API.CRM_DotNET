# CRM API

Backend cho hệ thống CRM: ASP.NET Core 8 Web API, EF Core 8, SQL Server, xác thực JWT, phân quyền theo chức năng. Các API dạng danh sách dùng [DevExtreme.AspNet.Data](https://github.com/DevExpress/DevExtreme.AspNet.Data) (MIT) để phân trang, lọc, sắp xếp, nhóm và tính tổng ngay trong SQL.

Giao diện web nằm ở repo riêng (**CRM Web**, ASP.NET Core MVC + DevExtreme) và gọi API này qua reverse proxy.

## Chạy nhanh bằng Docker

```sh
cp .env.example .env        # tuỳ chọn: đổi mật khẩu SQL, JWT key, cổng
docker compose up -d --build
```

- Swagger: http://localhost:5000/swagger
- Health check: http://localhost:5000/health
- SQL Server: `localhost,1433`, tài khoản `sa`, mật khẩu theo `SA_PASSWORD` (mặc định `Crm@Passw0rd!`), database `CrmDb`. Nhớ bật *Trust server certificate*.

Lần chạy đầu, API tự áp dụng migration (thử lại trong lúc SQL Server đang khởi động) và nạp dữ liệu nếu database chưa có người dùng:

| Tài khoản | Mật khẩu | Vai trò |
|---|---|---|
| admin | Admin@123 | Admin (toàn quyền) |
| sales | Sales@123 | Kinh doanh |
| kho | Kho@123 | Thủ kho |

Đặt `SEED_DEMO_DATA=false` để chỉ tạo vai trò và tài khoản, không kèm dữ liệu mẫu. Để xoá sạch rồi nạp lại từ đầu: `docker compose down -v && docker compose up -d --build`.

## Chạy khi phát triển

Yêu cầu: .NET SDK 8 trở lên, Docker (để chạy SQL Server).

```sh
docker compose up -d sqlserver
dotnet run --project API.CRM_DotNET --launch-profile http     # http://localhost:5045/swagger
```

Hoặc mở `API.CRM_DotNET.sln` bằng Visual Studio và chạy profile `http` / `https`. Chuỗi kết nối mặc định trong `appsettings.json` trỏ tới `localhost,1433`.

Thử nhanh API bằng [API.CRM_DotNET.http](API.CRM_DotNET/API.CRM_DotNET.http) (VS / VS Code REST Client): đăng nhập, lấy token rồi gọi các endpoint khác.

### Migration

Dùng `dotnet-ef` 8, đã khai báo trong `.config/dotnet-tools.json`. `dotnet-ef` bản 10 cài toàn cục không chạy được với project net8.

```sh
dotnet tool restore
dotnet ef migrations add <TenMigration> --project API.CRM_DotNET -o Data/Migrations
```

Không cần tự chạy `database update`, vì API tự migrate lúc khởi động.

## Cấu hình

Khi chạy bằng Docker hoặc biến môi trường, dấu `:` trong tên khoá được thay bằng `__`, ví dụ `Jwt__Key`.

| Khoá | Mặc định | Ý nghĩa |
|---|---|---|
| `ConnectionStrings:Default` | `Server=localhost,1433;Database=CrmDb;User Id=sa;...` | Chuỗi kết nối SQL Server |
| `Jwt:Key` | khoá dev | Khoá ký JWT, **tối thiểu 32 ký tự**. Bắt buộc đổi khi triển khai thật |
| `Jwt:Issuer` / `Jwt:Audience` | `crm-api` / `crm-web` | Thông tin issuer/audience của token |
| `Jwt:ExpiryMinutes` | `480` | Thời hạn token, tính bằng phút |
| `Seed:DemoData` | `true` | Nạp dữ liệu mẫu khi database trống |
| `EnableSwagger` | `false` | Bật Swagger ngoài môi trường Development |

> `appsettings.json` đang chứa mật khẩu SQL và JWT key **dành cho môi trường dev**. Khi triển khai, hãy ghi đè bằng biến môi trường hoặc `.env`. File `.env` đã nằm trong `.gitignore`.

## Cấu trúc

```
API.CRM_DotNET/
├── Auth/            Danh sách quyền, tạo JWT, PermissionPolicyProvider
├── Controllers/     Các endpoint REST
├── Data/            DbContext, seed dữ liệu, migrations
├── Domain/          Entity: khách hàng, kho, đơn hàng, người dùng...
├── Dtos/            DTO đọc (dạng phẳng) và DTO ghi (có validate)
├── Infrastructure/  Model binder cho DevExtreme, xử lý lỗi
└── Services/        InventoryService: nơi duy nhất thay đổi tồn kho
```

## Endpoint chính

Mọi endpoint (trừ đăng nhập) đều cần header `Authorization: Bearer <token>`.

| Nhóm | Endpoint |
|---|---|
| Xác thực | `POST /api/auth/login`, `GET /api/auth/me`, `POST /api/auth/change-password` |
| Người dùng & vai trò | `/api/users`, `/api/roles`, `GET /api/roles/permissions` |
| Khách hàng | `/api/customers`, `/api/contacts`, `/api/activities`, `POST /api/customers/import` |
| Sản phẩm & kho | `/api/products`, `/api/categories`, `/api/warehouses`, `POST /api/products/import`, `POST /api/products/by-skus` |
| Tồn kho | `GET /api/inventory/stock`, `GET /api/inventory/summary`, `GET /api/inventory/transactions`, `POST /api/inventory/import`, `POST /api/inventory/export`, `POST /api/inventory/transfer` |
| Kiểm kho | `GET /api/inventory/count-sheet?warehouseId=`, `POST /api/inventory/counts`, `GET /api/inventory/counts`, `GET /api/inventory/counts/{id}/lines` |
| Đơn hàng | `/api/orders`, `POST /api/orders/{id}/confirm`, `.../complete`, `.../cancel` |
| Cơ hội bán hàng | `/api/opportunities` |
| Dashboard | `GET /api/dashboard` |

Xem đầy đủ tham số trên Swagger.

### Truy vấn danh sách

Các `GET` dạng danh sách nhận load options của DevExtreme trên query string. Giá trị là JSON:

```
GET /api/customers?skip=0&take=20&requireTotalCount=true
    &sort=[{"selector":"name","desc":false}]
    &filter=[["status","=",1],"and",["name","contains","minh"]]
    &totalSummary=[{"selector":"id","summaryType":"count"}]
```

Kết quả trả về có dạng `{ data, totalCount, groupCount, summary }`. Tên trường dùng camelCase giống JSON trả về.

### Lỗi

| Mã | Khi nào | Nội dung trả về |
|---|---|---|
| `400` | Vi phạm quy tắc nghiệp vụ | `{ "message": "..." }` (tiếng Việt) |
| `400` | Dữ liệu không hợp lệ | `{ "errors": { "Field": ["..."] } }` |
| `401` | Chưa đăng nhập, hoặc token hết hạn | |
| `403` | Không đủ quyền | |
| `409` | Trùng mã, hoặc xoá dữ liệu đang được tham chiếu | `{ "message": "..." }` |

## Phân quyền

Các quyền được khai báo trong [Auth/Permissions.cs](API.CRM_DotNET/Auth/Permissions.cs), ví dụ `Customers.View`, `Orders.Manage`, `Inventory.Manage`, `Users.Manage`. Quyền được gán cho vai trò, và người dùng có thể giữ nhiều vai trò.

- Lúc đăng nhập, quyền được ghi vào JWT dưới dạng claim `perm`. Vì thế đổi quyền chỉ có hiệu lực từ lần đăng nhập sau.
- `[Authorize(Policy = "Orders.View|Customers.View")]` nghĩa là chỉ cần có một trong các quyền được liệt kê. Một số API đọc dùng cách này, vì màn hình khác cần chúng để tra cứu, ví dụ màn hình đơn hàng cần danh sách khách hàng.

## Quy tắc nghiệp vụ quan trọng

- **Tồn kho** chỉ thay đổi qua `InventoryService.MoveAsync`. Mỗi lần thay đổi đều ghi một dòng vào sổ kho (`StockTransactions`). Thao tác trừ kho là một câu `UPDATE ... WHERE Quantity >= n`, nên tồn kho không bao giờ âm, kể cả khi nhiều request chạy đồng thời.
- **Đơn hàng** đi qua các trạng thái Nháp → Xác nhận (trừ kho) → Hoàn thành. Huỷ một đơn đã xác nhận thì hàng được hoàn lại kho. Chỉ sửa hoặc xoá được đơn ở trạng thái Nháp.
- **Kiểm kho** lưu phiếu kiểm và tự sinh các dòng điều chỉnh (`Adjustment`) cho phần chênh lệch.
- **Nhập Excel** (`/import`) ghi kiểu tất cả hoặc không: chỉ cần một dòng lỗi là không dòng nào được lưu. Dòng có mã khách hàng hoặc mã SP đã tồn tại thì cập nhật, mã mới thì tạo mới.
- **Xoá**: khách hàng, sản phẩm hoặc kho đã phát sinh giao dịch thì không xoá được. Hãy chuyển chúng sang trạng thái ngừng.
- **Thời gian** được lưu theo UTC, và JSON trả về có hậu tố `Z`.

## Đồng bộ với repo CRM Web

Repo Web giữ bản sao của một số định nghĩa. Khi sửa ở đây, phải sửa bên Web tương ứng:

| Ở repo API | Bản sao ở repo Web |
|---|---|
| `Auth/Permissions.cs` (mã quyền) | `Auth/Permissions.cs` |
| Các enum trong `Domain/*.cs` (trạng thái khách hàng, đơn hàng, giai đoạn cơ hội, loại giao dịch kho...) | `crm.enums` trong `wwwroot/js/site.js` |
| Tên trường của DTO | Cấu hình cột của grid và form trong `Views/**` |
