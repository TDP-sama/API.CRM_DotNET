namespace API.CRM_DotNET.Documents;

/// <summary>Our own company: the seller on every document. Section "Company" in appsettings.</summary>
public class CompanyOptions
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
}

/// <summary>Section "Documents" in appsettings.</summary>
public class DocumentOptions
{
    /// <summary>Folder (relative to the app root, or absolute) holding contract templates (.docx / .rtf).</summary>
    public string TemplatePath { get; set; } = "DevexReport/Template";

    /// <summary>
    /// In contracts, the customer is Bên A (buyer) and we are Bên B (seller) — the usual Vietnamese
    /// convention. Set to false to swap.
    /// </summary>
    public bool CustomerIsPartyA { get; set; } = true;

    /// <summary>Where the contract is signed, printed in "…, ngày … tháng … năm …".</summary>
    public string? SigningPlace { get; set; }
}
