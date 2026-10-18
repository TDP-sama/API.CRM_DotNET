using API.CRM_DotNET.Auth;
using API.CRM_DotNET.Data;
using API.CRM_DotNET.Domain;
using API.CRM_DotNET.Dtos;
using API.CRM_DotNET.Infrastructure;
using DevExtreme.AspNet.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.CRM_DotNET.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController(CrmDbContext db) : ControllerBase
{
    private IQueryable<CustomerDto> Query() => db.Customers.AsNoTracking().Select(c => new CustomerDto
    {
        Id = c.Id,
        Code = c.Code,
        Name = c.Name,
        Type = c.Type,
        Status = c.Status,
        Email = c.Email,
        Phone = c.Phone,
        Address = c.Address,
        TaxCode = c.TaxCode,
        Notes = c.Notes,
        AssignedToId = c.AssignedToId,
        AssignedToName = c.AssignedTo != null ? c.AssignedTo.FullName : null,
        CreatedAt = c.CreatedAt,
    });

    /// <summary>
    /// Also used by customer lookups on the order/opportunity screens, so either permission suffices.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.CustomersView + "|" + Permissions.OrdersView + "|" + Permissions.OpportunitiesView)]
    public async Task<object> Get(DataSourceLoadOptions loadOptions) =>
        await DataSourceLoader.LoadAsync(Query(), loadOptions);

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.CustomersView)]
    public async Task<ActionResult<CustomerDto>> GetById(int id) =>
        await Query().FirstOrDefaultAsync(c => c.Id == id) is { } dto ? dto : NotFound();

    [HttpPost]
    [Authorize(Policy = Permissions.CustomersManage)]
    public async Task<IActionResult> Create(CustomerInput input)
    {
        var customer = new Customer();
        Apply(customer, input);
        var autoCode = string.IsNullOrWhiteSpace(input.Code);
        if (autoCode) customer.Code = "TMP-" + Guid.NewGuid().ToString("N");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        if (autoCode)
        {
            customer.Code = $"KH{customer.Id:D5}";
            await db.SaveChangesAsync();
        }
        return Ok(new { customer.Id, customer.Code });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.CustomersManage)]
    public async Task<IActionResult> Update(int id, CustomerInput input)
    {
        var customer = await db.Customers.FindAsync(id);
        if (customer is null) return NotFound();
        Apply(customer, input);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.CustomersManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await db.Customers.FindAsync(id);
        if (customer is null) return NotFound();
        if (await db.Orders.AnyAsync(o => o.CustomerId == id) || await db.Opportunities.AnyAsync(o => o.CustomerId == id))
            throw new BusinessException("Khách hàng đã có đơn hàng hoặc cơ hội bán hàng, hãy chuyển sang trạng thái Ngừng giao dịch thay vì xoá.");
        db.Customers.Remove(customer);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("import")]
    [Authorize(Policy = Permissions.CustomersManage)]
    public async Task<ImportResult> Import(ImportRequest<CustomerImportRow> request)
    {
        var rows = request.Rows;
        var codes = rows.Where(r => !string.IsNullOrWhiteSpace(r.Code)).Select(r => r.Code!.Trim()).ToList();
        var duplicate = codes.GroupBy(c => c, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new BusinessException($"Mã khách hàng \"{duplicate.Key}\" bị lặp trong file.");

        var usernames = rows.Select(r => r.AssignedToUsername?.Trim()).OfType<string>().Where(u => u.Length > 0).Distinct().ToList();
        var userIds = await db.Users.Where(u => usernames.Contains(u.Username))
            .ToDictionaryAsync(u => u.Username, u => u.Id, StringComparer.OrdinalIgnoreCase);
        var unknownUser = usernames.FirstOrDefault(u => !userIds.ContainsKey(u));
        if (unknownUser is not null) throw new BusinessException($"Không tìm thấy người phụ trách \"{unknownUser}\".");

        return await db.RunInTransactionAsync(async () =>
        {
            var existing = await db.Customers.Where(c => codes.Contains(c.Code))
                .ToDictionaryAsync(c => c.Code, StringComparer.OrdinalIgnoreCase);
            var created = new List<Customer>();
            var updated = 0;
            foreach (var row in rows)
            {
                var username = row.AssignedToUsername?.Trim();
                if (!string.IsNullOrWhiteSpace(row.Code) && existing.TryGetValue(row.Code.Trim(), out var customer))
                {
                    row.AssignedToId = string.IsNullOrEmpty(username) ? customer.AssignedToId : userIds[username];
                    Apply(customer, row);
                    updated++;
                }
                else
                {
                    row.AssignedToId = string.IsNullOrEmpty(username) ? null : userIds[username];
                    customer = new Customer();
                    Apply(customer, row);
                    if (string.IsNullOrWhiteSpace(row.Code)) customer.Code = "TMP-" + Guid.NewGuid().ToString("N");
                    db.Customers.Add(customer);
                    created.Add(customer);
                }
            }
            await db.SaveChangesAsync();
            foreach (var c in created.Where(c => c.Code.StartsWith("TMP-")))
                c.Code = $"KH{c.Id:D5}";
            await db.SaveChangesAsync();
            return new ImportResult(created.Count, updated);
        });
    }

    private static void Apply(Customer c, CustomerInput input)
    {
        if (!string.IsNullOrWhiteSpace(input.Code)) c.Code = input.Code.Trim();
        c.Name = input.Name.Trim();
        c.Type = input.Type;
        c.Status = input.Status;
        c.Email = input.Email;
        c.Phone = input.Phone;
        c.Address = input.Address;
        c.TaxCode = input.TaxCode;
        c.Notes = input.Notes;
        c.AssignedToId = input.AssignedToId;
    }
}

[ApiController]
[Route("api/contacts")]
public class ContactsController(CrmDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.CustomersView)]
    public async Task<object> Get(DataSourceLoadOptions loadOptions) =>
        await DataSourceLoader.LoadAsync(db.Contacts.AsNoTracking().Select(c => new ContactDto
        {
            Id = c.Id,
            CustomerId = c.CustomerId,
            CustomerName = c.Customer.Name,
            FullName = c.FullName,
            Position = c.Position,
            Email = c.Email,
            Phone = c.Phone,
            IsPrimary = c.IsPrimary,
        }), loadOptions);

    [HttpPost]
    [Authorize(Policy = Permissions.CustomersManage)]
    public async Task<IActionResult> Create(ContactInput input)
    {
        var contact = new Contact();
        await ApplyAsync(contact, input);
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();
        return Ok(new { contact.Id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.CustomersManage)]
    public async Task<IActionResult> Update(int id, ContactInput input)
    {
        var contact = await db.Contacts.FindAsync(id);
        if (contact is null) return NotFound();
        await ApplyAsync(contact, input);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.CustomersManage)]
    public async Task<IActionResult> Delete(int id)
    {
        await db.Contacts.Where(c => c.Id == id).ExecuteDeleteAsync();
        return NoContent();
    }

    private async Task ApplyAsync(Contact c, ContactInput input)
    {
        // Only one primary contact per customer.
        if (input.IsPrimary)
            await db.Contacts.Where(x => x.CustomerId == input.CustomerId && x.Id != c.Id && x.IsPrimary)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsPrimary, false));
        c.CustomerId = input.CustomerId;
        c.FullName = input.FullName.Trim();
        c.Position = input.Position;
        c.Email = input.Email;
        c.Phone = input.Phone;
        c.IsPrimary = input.IsPrimary;
    }
}

[ApiController]
[Route("api/activities")]
public class ActivitiesController(CrmDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.CustomersView)]
    public async Task<object> Get(DataSourceLoadOptions loadOptions) =>
        await DataSourceLoader.LoadAsync(db.Activities.AsNoTracking().Select(a => new ActivityDto
        {
            Id = a.Id,
            CustomerId = a.CustomerId,
            CustomerName = a.Customer.Name,
            Type = a.Type,
            Subject = a.Subject,
            Content = a.Content,
            Date = a.Date,
            UserId = a.UserId,
            UserName = a.User != null ? a.User.FullName : null,
        }), loadOptions);

    [HttpPost]
    [Authorize(Policy = Permissions.CustomersManage)]
    public async Task<IActionResult> Create(ActivityInput input)
    {
        var activity = new Activity { UserId = User.GetUserId() };
        Apply(activity, input);
        db.Activities.Add(activity);
        await db.SaveChangesAsync();
        return Ok(new { activity.Id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.CustomersManage)]
    public async Task<IActionResult> Update(int id, ActivityInput input)
    {
        var activity = await db.Activities.FindAsync(id);
        if (activity is null) return NotFound();
        Apply(activity, input);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.CustomersManage)]
    public async Task<IActionResult> Delete(int id)
    {
        await db.Activities.Where(a => a.Id == id).ExecuteDeleteAsync();
        return NoContent();
    }

    private static void Apply(Activity a, ActivityInput input)
    {
        a.CustomerId = input.CustomerId;
        a.Type = input.Type;
        a.Subject = input.Subject.Trim();
        a.Content = input.Content;
        a.Date = input.Date ?? DateTime.UtcNow;
    }
}
