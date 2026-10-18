using System.ComponentModel.DataAnnotations;
using System.Globalization;
using API.CRM_DotNET.Data;
using API.CRM_DotNET.DevexReport.Design;
using API.CRM_DotNET.Domain;
using API.CRM_DotNET.Infrastructure;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API.CRM_DotNET.Documents;

public class ContractRequest
{
    /// <summary>"order" or "opportunity".</summary>
    [Required] public string Source { get; set; } = "order";
    public int SourceId { get; set; }
    /// <summary>Template file name. null = the customer's own template or default; "" = no template.</summary>
    public string? TemplateName { get; set; }
    public string? ContractNumber { get; set; }
    public DateTime? SignDate { get; set; }
    public string? Requirements { get; set; }
    public string? CustomerRepresentative { get; set; }
    public string? CustomerPosition { get; set; }
    /// <summary>"pdf" or "docx".</summary>
    public string Format { get; set; } = "pdf";
}

public record ContractDefaults(string ContractNumber, DateTime SignDate, string Requirements,
    string? CustomerRepresentative, string? CustomerPosition, string? TemplateName, string CustomerCode, string CustomerName, decimal Value);

public record RenderedDocument(byte[] Content, string ContentType, string FileName);

/// <summary>Builds <see cref="DocumentData"/> from orders/opportunities and renders the reports to PDF or DOCX.</summary>
public class DocumentService(CrmDbContext db, IOptions<CompanyOptions> company, IOptions<DocumentOptions> options, TemplateStore templates)
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public async Task<RenderedDocument> PurchaseOrderAsync(int orderId, string format)
    {
        var order = await LoadOrderAsync(orderId);
        if (order.Status == OrderStatus.Cancelled) throw new BusinessException("Đơn hàng đã huỷ, không xuất được đơn đặt hàng.");
        var data = FromOrder(order, "Đơn đặt hàng", order.Code, ToVietnamTime(order.OrderDate));
        var report = new PurchaseOrderReport();
        report.Bind(data);
        return Render(report, format, $"DonDatHang_{order.Code}");
    }

    public async Task<RenderedDocument> InvoiceAsync(int orderId, decimal vatRate, string format)
    {
        if (vatRate is < 0 or > 100) throw new BusinessException("Thuế suất VAT không hợp lệ.");
        var order = await LoadOrderAsync(orderId);
        if (order.Status is not (OrderStatus.Confirmed or OrderStatus.Completed))
            throw new BusinessException("Chỉ xuất hoá đơn cho đơn hàng đã xác nhận hoặc đã hoàn thành.");
        var data = FromOrder(order, "Hoá đơn bán hàng", "HD" + order.Code, VietnamToday());
        data.VatRate = vatRate;
        data.VatAmount = Math.Round((data.SubTotal - data.Discount) * vatRate / 100, 0, MidpointRounding.AwayFromZero);
        data.Total = data.SubTotal - data.Discount + data.VatAmount;
        var report = new InvoiceReport();
        report.Bind(data);
        return Render(report, format, $"HoaDon_{order.Code}");
    }

    public async Task<ContractDefaults> ContractDefaultsAsync(string source, int id)
    {
        var (data, customer) = await ContractDataAsync(source, id);
        var contact = PrimaryContact(customer);
        return new ContractDefaults(data.Number, data.Date, data.Requirements ?? "", contact?.FullName, contact?.Position,
            templates.DefaultFor(customer.Code), customer.Code, customer.Name, data.Total);
    }

    public async Task<RenderedDocument> ContractAsync(ContractRequest request)
    {
        var (data, customer) = await ContractDataAsync(request.Source, request.SourceId);
        if (!string.IsNullOrWhiteSpace(request.ContractNumber)) data.Number = request.ContractNumber.Trim();
        if (request.SignDate is { } date) data.Date = ToVietnamTime(date).Date;
        if (!string.IsNullOrWhiteSpace(request.Requirements)) data.Requirements = request.Requirements.Trim();
        if (request.CustomerRepresentative is not null) data.Buyer.Representative = request.CustomerRepresentative.Trim();
        if (request.CustomerPosition is not null) data.Buyer.Position = request.CustomerPosition.Trim();

        string? templatePath = null;
        if (request.TemplateName is null)
            templatePath = templates.DefaultFor(customer.Code) is { } name ? templates.Find(name) : null;
        else if (request.TemplateName.Length > 0)
            templatePath = templates.Find(request.TemplateName) ?? throw new BusinessException($"Không tìm thấy file mẫu \"{request.TemplateName}\".");

        data.SigningPlace = options.Value.SigningPlace;
        var report = new ContractReport();
        report.Bind(data);
        report.SetTerms(templatePath);
        var safeNumber = string.Concat(data.Number.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-'));
        return Render(report, request.Format, $"HopDong_{safeNumber}");
    }

    private async Task<(DocumentData Data, Customer Customer)> ContractDataAsync(string source, int id)
    {
        DocumentData data;
        Customer customer;
        switch (source)
        {
            case "order":
                var order = await LoadOrderAsync(id);
                if (order.Status == OrderStatus.Cancelled) throw new BusinessException("Đơn hàng đã huỷ, không lập hợp đồng được.");
                customer = order.Customer;
                data = FromOrder(order, "Hợp đồng mua bán", "HĐ-" + order.Code, VietnamToday());
                data.Requirements = $"Bên bán cung cấp cho bên mua các hàng hoá theo đơn hàng số {order.Code} với số lượng, chủng loại và đơn giá nêu tại Điều 2."
                    + (string.IsNullOrWhiteSpace(order.Note) ? "" : "\nGhi chú: " + order.Note);
                break;
            case "opportunity":
                var opportunity = await db.Opportunities.AsNoTracking()
                    .Include(o => o.Customer).ThenInclude(c => c.Contacts)
                    .FirstOrDefaultAsync(o => o.Id == id) ?? throw new BusinessException("Không tìm thấy cơ hội bán hàng.");
                customer = opportunity.Customer;
                data = NewData("Hợp đồng mua bán", $"HĐ-CH{opportunity.Id:D5}", VietnamToday(), customer);
                data.Requirements = opportunity.Title + (string.IsNullOrWhiteSpace(opportunity.Note) ? "" : "\n" + opportunity.Note);
                data.SubTotal = data.Total = opportunity.Value;
                break;
            default:
                throw new BusinessException("Nguồn lập hợp đồng phải là \"order\" hoặc \"opportunity\".");
        }
        return (data, customer);
    }

    private async Task<Order> LoadOrderAsync(int id) =>
        await db.Orders.AsNoTracking()
            .Include(o => o.Customer).ThenInclude(c => c.Contacts)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id) ?? throw new BusinessException("Không tìm thấy đơn hàng.");

    private DocumentData FromOrder(Order order, string title, string number, DateTime date)
    {
        var data = NewData(title, number, date, order.Customer);
        data.OrderCode = order.Code;
        data.Note = order.Note;
        data.Lines = order.Items.OrderBy(i => i.Id).Select((i, index) => new DocumentLine
        {
            No = index + 1,
            Sku = i.Product.Sku,
            Name = i.Product.Name,
            Unit = i.Product.Unit,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Amount = i.LineTotal,
        }).ToList();
        data.SubTotal = order.SubTotal;
        data.Discount = order.Discount;
        data.Total = order.Total;
        return data;
    }

    private DocumentData NewData(string title, string number, DateTime date, Customer customer)
    {
        var c = company.Value;
        var contact = PrimaryContact(customer);
        var seller = new Party
        {
            Name = c.Name, Address = c.Address, TaxCode = c.TaxCode, Phone = c.Phone, Email = c.Email,
            Representative = c.Representative, Position = c.Position, BankAccount = c.BankAccount, BankName = c.BankName,
        };
        var buyer = new Party
        {
            Name = customer.Name, Address = customer.Address, TaxCode = customer.TaxCode, Phone = customer.Phone, Email = customer.Email,
            Representative = contact?.FullName ?? (customer.Type == CustomerType.Individual ? customer.Name : null),
            Position = contact?.Position,
        };
        var customerIsA = options.Value.CustomerIsPartyA;
        return new DocumentData
        {
            Title = title,
            Number = number,
            Date = date,
            Seller = seller,
            Buyer = buyer,
            PartyA = customerIsA ? buyer : seller,
            PartyB = customerIsA ? seller : buyer,
            PartyARole = customerIsA ? "Bên mua" : "Bên bán",
            PartyBRole = customerIsA ? "Bên bán" : "Bên mua",
        };
    }

    private static Contact? PrimaryContact(Customer customer) =>
        customer.Contacts.OrderByDescending(c => c.IsPrimary).ThenBy(c => c.Id).FirstOrDefault();

    private static RenderedDocument Render(XtraReport report, string format, string fileName)
    {
        // Number formats ("1.250.000") follow the current culture while the document is generated.
        var (culture, uiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = Vi;
        try
        {
            using (report)
            using (var stream = new MemoryStream())
            {
                if (string.Equals(format, "docx", StringComparison.OrdinalIgnoreCase))
                {
                    // Flowing (not page-by-page) layout so the Word file is easy to edit afterwards.
                    report.ExportToDocx(stream, new DocxExportOptions { ExportMode = DocxExportMode.SingleFile, TableLayout = true });
                    return new RenderedDocument(stream.ToArray(), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName + ".docx");
                }
                report.ExportToPdf(stream);
                return new RenderedDocument(stream.ToArray(), "application/pdf", fileName + ".pdf");
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    private static readonly TimeZoneInfo VietnamZone = FindVietnamZone();

    private static TimeZoneInfo FindVietnamZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "ICT", "ICT"); }
    }

    private static DateTime ToVietnamTime(DateTime value) =>
        TimeZoneInfo.ConvertTimeFromUtc(value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc), VietnamZone);

    private static DateTime VietnamToday() => ToVietnamTime(DateTime.UtcNow).Date;
}
