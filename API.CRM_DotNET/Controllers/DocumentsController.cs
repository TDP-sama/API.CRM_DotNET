using API.CRM_DotNET.Auth;
using API.CRM_DotNET.Documents;
using API.CRM_DotNET.Infrastructure;
using DevExpress.XtraReports.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.CRM_DotNET.Controllers;

/// <summary>Printable documents (DevExpress Reporting) and the Word templates used by contracts.</summary>
[ApiController]
[Route("api/documents")]
public class DocumentsController(DocumentService documents, TemplateStore templates) : ControllerBase
{
    private const string ViewDocs = Permissions.OrdersView + "|" + Permissions.OpportunitiesView;
    private const string ManageTemplates = Permissions.OrdersManage + "|" + Permissions.OpportunitiesManage;

    /// <summary>Purchase order for an order. format = pdf | docx.</summary>
    [HttpGet("orders/{id:int}/purchase-order")]
    [Authorize(Policy = Permissions.OrdersView)]
    public async Task<IActionResult> PurchaseOrder(int id, string format = "pdf") =>
        ToFile(await documents.PurchaseOrderAsync(id, format));

    /// <summary>Sales invoice for a confirmed/completed order. vatRate in percent (0 = no VAT line).</summary>
    [HttpGet("orders/{id:int}/invoice")]
    [Authorize(Policy = Permissions.OrdersView)]
    public async Task<IActionResult> Invoice(int id, decimal vatRate = 10, string format = "pdf") =>
        ToFile(await documents.InvoiceAsync(id, vatRate, format));

    /// <summary>Suggested values for the contract dialog (number, requirements, representative, template).</summary>
    [HttpGet("contracts/defaults")]
    [Authorize(Policy = ViewDocs)]
    public async Task<ActionResult<ContractDefaults>> ContractDefaults(string source, int id) =>
        CanUse(source) ? await documents.ContractDefaultsAsync(source, id) : Forbid();

    [HttpPost("contracts")]
    [Authorize(Policy = ViewDocs)]
    public async Task<IActionResult> Contract(ContractRequest request) =>
        CanUse(request.Source) ? ToFile(await documents.ContractAsync(request)) : Forbid();

    /// <summary>[Placeholders] that Word templates may contain.</summary>
    [HttpGet("placeholders")]
    [Authorize(Policy = ViewDocs)]
    public IReadOnlyList<DocumentData.Placeholder> Placeholders() => DocumentData.Placeholders;

    [HttpGet("templates")]
    [Authorize(Policy = ViewDocs)]
    public List<TemplateStore.TemplateInfo> Templates() => templates.List();

    [HttpGet("templates/{name}")]
    [Authorize(Policy = ViewDocs)]
    public IActionResult DownloadTemplate(string name)
    {
        var path = templates.Find(name);
        if (path is null) return NotFound();
        var type = name.EndsWith(".rtf", StringComparison.OrdinalIgnoreCase)
            ? "application/rtf" : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        return PhysicalFile(path, type, name);
    }

    /// <summary>Uploads (or replaces) a template. Name it after a customer code (KH00002.docx) to make it that customer's default.</summary>
    [HttpPost("templates")]
    [Authorize(Policy = ManageTemplates)]
    [RequestSizeLimit(TemplateStore.MaxSize + 64 * 1024)]
    public async Task<TemplateStore.TemplateInfo> UploadTemplate([FromForm] TemplateUploadForm form)
    {
        var (file, name) = (form.File, form.Name);
        if (file.Length == 0) throw new BusinessException("File rỗng.");
        if (file.Length > TemplateStore.MaxSize) throw new BusinessException("File mẫu tối đa 10 MB.");
        var fileName = string.IsNullOrWhiteSpace(name) ? Path.GetFileName(file.FileName) : name.Trim();
        if (!Path.HasExtension(fileName)) fileName += Path.GetExtension(file.FileName);
        if (!TemplateStore.IsValidName(fileName))
            throw new BusinessException("Tên file mẫu không hợp lệ. Chỉ dùng chữ, số, dấu gạch và đuôi .docx hoặc .rtf (ví dụ KH00002.docx).");

        // Make sure the report engine can actually read it before storing (probe a temp copy with the same extension).
        var probePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + Path.GetExtension(fileName));
        try
        {
            await using (var temp = System.IO.File.Create(probePath))
                await file.CopyToAsync(temp);
            using (var probe = new XRRichText())
                probe.LoadFile(probePath);
            await using var content = System.IO.File.OpenRead(probePath);
            await templates.SaveAsync(fileName, content);
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            throw new BusinessException("Không đọc được file. Hãy lưu lại bằng Word dưới dạng .docx (Word Document) hoặc .rtf rồi thử lại.");
        }
        finally
        {
            System.IO.File.Delete(probePath);
        }
        return templates.List().First(t => t.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }

    [HttpDelete("templates/{name}")]
    [Authorize(Policy = ManageTemplates)]
    public IActionResult DeleteTemplate(string name) => templates.Delete(name) ? NoContent() : NotFound();

    /// <summary>Contracts from an order need Orders.View; from an opportunity, Opportunities.View.</summary>
    private bool CanUse(string source) =>
        User.HasClaim(Permissions.ClaimType, source == "opportunity" ? Permissions.OpportunitiesView : Permissions.OrdersView);

    private FileContentResult ToFile(RenderedDocument doc) => File(doc.Content, doc.ContentType, doc.FileName);

    /// <summary>
    /// Multipart form for template uploads. Kept as one class because Swashbuckle cannot describe
    /// a loose [FromForm] IFormFile parameter (swagger.json would fail with a 500).
    /// </summary>
    public class TemplateUploadForm
    {
        /// <summary>The .docx or .rtf file.</summary>
        public IFormFile File { get; set; } = null!;

        /// <summary>Name to store it under (e.g. KH00002.docx); defaults to the uploaded file name.</summary>
        public string? Name { get; set; }
    }
}
