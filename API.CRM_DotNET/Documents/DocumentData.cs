using System.Globalization;

namespace API.CRM_DotNET.Documents;

public class Party
{
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? TaxCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Representative { get; set; }
    public string? Position { get; set; }
    public string? BankAccount { get; set; }
    public string? BankName { get; set; }

    /// <summary>Non-empty detail lines ("Địa chỉ: …"), for the party blocks printed on documents.</summary>
    public string Details(bool includeRepresentative)
    {
        var lines = new List<string>();
        void Add(string label, string? value) { if (!string.IsNullOrWhiteSpace(value)) lines.Add($"{label}: {value}"); }
        Add("Địa chỉ", Address);
        Add("Mã số thuế", TaxCode);
        Add("Điện thoại", Phone);
        Add("Email", Email);
        if (includeRepresentative)
        {
            if (!string.IsNullOrWhiteSpace(Representative))
                lines.Add("Đại diện: " + Representative + (string.IsNullOrWhiteSpace(Position) ? "" : "        Chức vụ: " + Position));
            Add("Số tài khoản", BankAccount);
            Add("Tại ngân hàng", BankName);
        }
        return string.Join("\n", lines);
    }
}

public class DocumentLine
{
    public int No { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>
/// Everything a printed document needs. It is the report's data source; the Vietnamese-named string
/// properties are also the [placeholders] usable inside Word contract templates (see Placeholders).
/// </summary>
public class DocumentData
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public string Title { get; set; } = "";
    public string Number { get; set; } = "";
    public DateTime Date { get; set; }
    public string? OrderCode { get; set; }
    public string? Note { get; set; }

    public Party Seller { get; set; } = new();
    public Party Buyer { get; set; } = new();
    /// <summary>Contract parties (Bên A / Bên B); which of buyer/seller is A comes from DocumentOptions.</summary>
    public Party PartyA { get; set; } = new();
    public Party PartyB { get; set; } = new();
    public string PartyARole { get; set; } = "";
    public string PartyBRole { get; set; } = "";

    public List<DocumentLine> Lines { get; set; } = new();
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    /// <summary>VAT percentage (invoices only); 0 = no VAT line.</summary>
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }
    public string? Requirements { get; set; }
    /// <summary>Contracts: where it is signed ("Hôm nay, ngày … tại …").</summary>
    public string? SigningPlace { get; set; }

    // ----- Derived text used by report layouts -----
    public string DateLong => $"ngày {Date:dd} tháng {Date:MM} năm {Date:yyyy}";
    public string DateTitle => $"Ngày {Date:dd} tháng {Date:MM} năm {Date:yyyy}";
    public string TotalText => Money(Total);
    public string TotalInWords => VietnameseNumber.ToWords(Total);
    public string SellerDetails => Seller.Details(false);
    public string BuyerDetails => Buyer.Details(true);
    public string PartyADetails => PartyA.Details(true);
    public string PartyBDetails => PartyB.Details(true);
    public bool HasLines => Lines.Count > 0;

    // ----- Placeholders for Word templates: [TenBenA], [GiaTri], ... -----
    public string SoHopDong => Number;
    public string NgayHopDong => DateLong;
    public string MaDonHang => OrderCode ?? "";
    public string YeuCau => Requirements ?? "";
    public string GiaTri => TotalText;
    public string GiaTriBangChu => TotalInWords;
    public string ThueSuat => VatRate > 0 ? VatRate.ToString("0.##", Vi) + "%" : "";

    public string TenBenA => PartyA.Name;
    public string DiaChiBenA => PartyA.Address ?? "";
    public string MaSoThueBenA => PartyA.TaxCode ?? "";
    public string DienThoaiBenA => PartyA.Phone ?? "";
    public string EmailBenA => PartyA.Email ?? "";
    public string DaiDienBenA => PartyA.Representative ?? "";
    public string ChucVuBenA => PartyA.Position ?? "";
    public string TaiKhoanBenA => PartyA.BankAccount ?? "";
    public string NganHangBenA => PartyA.BankName ?? "";

    public string TenBenB => PartyB.Name;
    public string DiaChiBenB => PartyB.Address ?? "";
    public string MaSoThueBenB => PartyB.TaxCode ?? "";
    public string DienThoaiBenB => PartyB.Phone ?? "";
    public string EmailBenB => PartyB.Email ?? "";
    public string DaiDienBenB => PartyB.Representative ?? "";
    public string ChucVuBenB => PartyB.Position ?? "";
    public string TaiKhoanBenB => PartyB.BankAccount ?? "";
    public string NganHangBenB => PartyB.BankName ?? "";

    public static string Money(decimal value) => value.ToString("#,##0", Vi) + " đồng";

    public record Placeholder(string Name, string Description);

    /// <summary>Catalog shown to users who write templates; keep in sync with the properties above.</summary>
    public static readonly IReadOnlyList<Placeholder> Placeholders =
    [
        new("SoHopDong", "Số hợp đồng"),
        new("NgayHopDong", "Ngày ký, dạng \"ngày 18 tháng 10 năm 2026\""),
        new("MaDonHang", "Mã đơn hàng (nếu lập từ đơn hàng)"),
        new("YeuCau", "Nội dung / yêu cầu của hợp đồng"),
        new("GiaTri", "Giá trị hợp đồng, ví dụ \"125.000.000 đồng\""),
        new("GiaTriBangChu", "Giá trị hợp đồng bằng chữ"),
        new("TenBenA", "Tên Bên A"), new("DiaChiBenA", "Địa chỉ Bên A"), new("MaSoThueBenA", "Mã số thuế Bên A"),
        new("DienThoaiBenA", "Điện thoại Bên A"), new("EmailBenA", "Email Bên A"),
        new("DaiDienBenA", "Người đại diện Bên A"), new("ChucVuBenA", "Chức vụ người đại diện Bên A"),
        new("TaiKhoanBenA", "Số tài khoản Bên A"), new("NganHangBenA", "Ngân hàng Bên A"),
        new("TenBenB", "Tên Bên B"), new("DiaChiBenB", "Địa chỉ Bên B"), new("MaSoThueBenB", "Mã số thuế Bên B"),
        new("DienThoaiBenB", "Điện thoại Bên B"), new("EmailBenB", "Email Bên B"),
        new("DaiDienBenB", "Người đại diện Bên B"), new("ChucVuBenB", "Chức vụ người đại diện Bên B"),
        new("TaiKhoanBenB", "Số tài khoản Bên B"), new("NganHangBenB", "Ngân hàng Bên B"),
    ];
}
