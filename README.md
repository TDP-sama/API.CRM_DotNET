# CRM API

Đây là backend của hệ thống CRM. Nó cung cấp REST API cho giao diện web (repo CRM Web), lưu dữ liệu vào SQL Server và sinh các chứng từ PDF/Word.

Lần chạy đầu, API tự tạo database bằng migration và nạp dữ liệu mẫu. Tài khoản mẫu là `admin/Admin@123`, `sales/Sales@123` và `kho/Kho@123`.

## Công nghệ

- ASP.NET Core 8 Web API, Swagger (Swashbuckle)
- Entity Framework Core 8, SQL Server 2022
- Đăng nhập bằng JWT; phân quyền bằng policy, mỗi chức năng một quyền
- DevExtreme.AspNet.Data để các bảng lọc, sắp xếp, phân trang ngay trong SQL
- DevExpress Reporting 25.1 cho đơn đặt hàng, hoá đơn, hợp đồng (trên Linux dùng Skia)
- Docker

## API làm được gì

- **Khách hàng:** CRUD khách hàng, người liên hệ, lịch sử chăm sóc. Nhập từ Excel (trùng mã KH thì cập nhật, mã mới thì thêm).
- **Cơ hội bán hàng:** quản lý theo giai đoạn. Khi đổi giai đoạn, xác suất chốt được gợi ý lại.
- **Đơn hàng:** đi qua các trạng thái nháp → xác nhận → hoàn thành, hoặc huỷ. Lúc xác nhận thì trừ kho, lúc huỷ thì hoàn kho.
- **Sản phẩm, danh mục, kho:** CRUD, nhập sản phẩm từ Excel.
- **Tồn kho:** nhập, xuất, chuyển kho, kiểm kho. Mọi thay đổi đều ghi vào sổ kho, và tồn kho không bao giờ bị âm, kể cả khi nhiều người thao tác cùng lúc.
- **Dashboard:** số liệu tổng hợp cho trang tổng quan.
- **Chứng từ:** đơn đặt hàng, hoá đơn (có VAT, số tiền bằng chữ), hợp đồng; xuất PDF hoặc Word.
- **Người dùng và vai trò:** gán quyền cho vai trò, gán vai trò cho người dùng.

Danh sách endpoint đầy đủ xem trên Swagger.

## Cấu hình chính (`appsettings.json`)

| Khoá | Ý nghĩa |
|---|---|
| `ConnectionStrings:Default` | Chuỗi kết nối SQL Server |
| `Jwt:Key` | Khoá ký token, dài ít nhất 32 ký tự. Nhớ đổi khi đưa lên server thật |
| `Company:*` | Thông tin công ty mình, in lên chứng từ |
| `Documents:TemplatePath` | Thư mục chứa file Word mẫu hợp đồng (`DevexReport/Template`) |
| `Seed:DemoData` | Có nạp dữ liệu mẫu khi database còn trống hay không |

## Cấu trúc thư mục

```
API.CRM_DotNET/
├── Controllers/     REST API (/api/...)
├── Domain/          Entity
├── Data/            DbContext, migrations, dữ liệu mẫu
├── Auth/            Danh sách quyền, tạo JWT
├── Services/        InventoryService: nơi duy nhất được thay đổi tồn kho
├── Documents/       Gom dữ liệu cho chứng từ
└── DevexReport/
    ├── Design/      Các report DevExpress: đơn đặt hàng, hoá đơn, hợp đồng
    └── Template/    File Word mẫu phần điều khoản hợp đồng
```

## Chứng từ và mẫu hợp đồng

Muốn sửa bố cục chứng từ, mở file trong `DevexReport/Design` bằng Visual Studio, chọn *View Designer* rồi kéo thả như một report bình thường. Các trường dữ liệu lấy từ `Documents/DocumentData.cs`. Cần thêm thông tin nào thì thêm property vào class này, gán giá trị trong `DocumentService`.

Phần điều khoản của hợp đồng không nằm trong report mà lấy từ file Word trong `DevexReport/Template`:

- `default.docx` dùng chung cho mọi khách.
- Khách nào có điều khoản riêng thì đặt thêm một file tên theo mã khách, ví dụ `KH00002.docx`; hệ thống sẽ tự chọn file đó.
- Trong file Word có thể gõ các biến như `[TenBenA]`, `[GiaTri]`, `[GiaTriBangChu]`. Khi xuất, các biến này được thay bằng dữ liệu thật. Danh sách biến xem tại `GET /api/documents/placeholders`.

Gói DevExpress không có trên nuget.org, và phiên bản đang dùng ghi ở `<DevExpressVersion>` trong csproj. Docker không lấy được gói từ máy bạn, nên trước khi build image phải chép sẵn ra:

```powershell
dotnet restore
./tools/collect-devexpress-packages.ps1   # chép gói vào dxpackages/ (thư mục này không commit)
```

Nếu build mà không có license DevExpress, chứng từ in ra sẽ có dòng chữ "evaluation" ở chân trang. Có license thì truyền vào lúc build bằng `--secret id=devexpress_license,src=DevExpress_License.txt`.

## Cần nhớ

- Danh sách quyền (`Auth/Permissions.cs`) và các enum trong `Domain/` được chép sang repo Web. Sửa bên này thì sửa luôn bên kia.
- Lỗi nghiệp vụ trả về `400` kèm `{ message }`. Trùng mã, hoặc xoá dữ liệu đang được dùng ở chỗ khác, trả về `409`.
