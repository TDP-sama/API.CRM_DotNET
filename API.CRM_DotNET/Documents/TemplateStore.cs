using System.Text.RegularExpressions;
using API.CRM_DotNET.Infrastructure;
using Microsoft.Extensions.Options;

namespace API.CRM_DotNET.Documents;

/// <summary>
/// Word/RTF contract templates kept in one folder. A file named after a customer code
/// (e.g. KH00002.docx) is that customer's template; default.docx is used otherwise.
/// </summary>
public partial class TemplateStore(IOptions<DocumentOptions> options, IWebHostEnvironment env)
{
    public const long MaxSize = 10 * 1024 * 1024;
    private static readonly string[] Extensions = [".docx", ".rtf"];

    public record TemplateInfo(string Name, long Size, DateTime ModifiedAt);

    public string Folder
    {
        get
        {
            var path = options.Value.TemplatePath;
            var folder = Path.IsPathRooted(path) ? path : Path.Combine(env.ContentRootPath, path);
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public List<TemplateInfo> List() =>
        new DirectoryInfo(Folder).EnumerateFiles()
            .Where(f => Extensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => new TemplateInfo(f.Name, f.Length, f.LastWriteTimeUtc))
            .ToList();

    /// <summary>Full path of an existing template, or null. Rejects anything that is not a plain file name.</summary>
    public string? Find(string name)
    {
        if (!IsValidName(name)) return null;
        var path = Path.Combine(Folder, name);
        return File.Exists(path) ? path : null;
    }

    /// <summary>The customer's own template if there is one, otherwise the default one, otherwise null.</summary>
    public string? DefaultFor(string? customerCode)
    {
        var files = List().Select(t => t.Name).ToList();
        string? Pick(string stem) => files.FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), stem, StringComparison.OrdinalIgnoreCase));
        return (customerCode is null ? null : Pick(customerCode)) ?? Pick("default");
    }

    public async Task<string> SaveAsync(string name, Stream content)
    {
        if (!IsValidName(name))
            throw new BusinessException("Tên file mẫu không hợp lệ. Chỉ dùng chữ, số, dấu gạch và đuôi .docx hoặc .rtf (ví dụ KH00002.docx).");
        var path = Path.Combine(Folder, name);
        await using var file = File.Create(path);
        await content.CopyToAsync(file);
        return name;
    }

    public bool Delete(string name)
    {
        var path = Find(name);
        if (path is null) return false;
        File.Delete(path);
        return true;
    }

    public static bool IsValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= 100 && SafeName().IsMatch(name)
        && Extensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"^[\p{L}\p{N}_\-. ]+$")]
    private static partial Regex SafeName();
}
