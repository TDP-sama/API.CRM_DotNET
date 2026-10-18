# CRM API

Backend của hệ thống CRM: ASP.NET Core 8, EF Core + SQL Server, JWT, DevExpress Reporting. Giao diện web nằm ở repo **CRM Web**.

Lần chạy đầu, API tự migrate database và nạp dữ liệu mẫu. Tài khoản mẫu: `admin/Admin@123`, `sales/Sales@123`, `kho/Kho@123`.

## Cấu hình chính (`appsettings.json`)

| Khoá | Ý nghĩa |
|---|---|
| `ConnectionStrings:Default` | Chuỗi kết nối SQL Server |
| `Jwt:Key` | Khoá ký token, ≥ 32 ký tự. **Đổi khi triển khai** |
| `Company:*` | Thông tin công ty mình, in trên tài liệu |
| `Documents:TemplatePath` | Thư mục file Word mẫu hợp đồng (`DevexReport/Template`) |
| `Seed:DemoData` | Nạp dữ liệu mẫu khi database trống |

## Cấu trúc

```
API.CRM_DotNET/
├── Controllers/     REST API (/api/...)
├── Domain/          Entity
├── Data/            DbContext, migrations, seed
├── Auth/            Quyền (Permissions.cs), JWT
├── Services/        InventoryService (nơi duy nhất thay đổi tồn kho)
├── Documents/       Dữ liệu và service cho xuất tài liệu
└── DevexReport/
    ├── Design/      Report DevExpress: đơn đặt hàng, hoá đơn, hợp đồng
    └── Template/    File Word mẫu điều khoản hợp đồng
```

## Xuất tài liệu

- **Sửa bố cục:** mở file trong `DevexReport/Design` bằng Visual Studio → *View Designer*. Field List lấy từ `Documents/DocumentData.cs`.
- **Mẫu hợp đồng:** `DevexReport/Template/default.docx` dùng cho mọi khách hàng. Muốn khách nào có mẫu riêng thì đặt file theo mã khách, ví dụ `KH00002.docx`. Trong file Word có thể dùng biến như `[TenBenA]`, `[GiaTri]`; xem danh sách đầy đủ tại `GET /api/documents/placeholders`.
- **Gói DevExpress** không có trên nuget.org. Phiên bản khai báo ở `<DevExpressVersion>` trong csproj. Trước khi build Docker, chạy:
  ```powershell
  dotnet restore
  ./tools/collect-devexpress-packages.ps1   # chép gói vào dxpackages/ (không commit)
  ```
  Không có license DevExpress thì file xuất ra có dòng chữ "evaluation". Khi có license, truyền vào lúc build: `--secret id=devexpress_license,src=DevExpress_License.txt`.

## Lưu ý

- Danh sách quyền (`Auth/Permissions.cs`) và các enum trong `Domain/` có bản sao ở repo Web. Sửa ở đây thì phải sửa bên Web.
- Lỗi nghiệp vụ trả về `400 { message }`; trùng dữ liệu hoặc dữ liệu đang được dùng trả về `409`.
