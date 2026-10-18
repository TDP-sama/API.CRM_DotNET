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
[Route("api/roles")]
[Authorize(Policy = Permissions.UsersManage)]
public class RolesController(CrmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<object> Get(DataSourceLoadOptions loadOptions)
    {
        var roles = await db.Roles.AsNoTracking()
            .Select(r => new RoleDto
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                IsSystem = r.IsSystem,
                UserCount = r.UserRoles.Count,
                Permissions = r.Permissions.Select(p => p.Permission).ToList(),
            })
            .ToListAsync();
        return DataSourceLoader.Load(roles, loadOptions);
    }

    /// <summary>Catalog of all permissions, for the role editor.</summary>
    [HttpGet("permissions")]
    public IEnumerable<Permissions.Definition> GetPermissions() => Permissions.All;

    [HttpPost]
    public async Task<IActionResult> Create(RoleInput input)
    {
        var role = new Role();
        await ApplyAsync(role, input);
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return Ok(new { role.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, RoleInput input)
    {
        var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id);
        if (role is null) return NotFound();
        if (role.IsSystem && !input.Permissions.Contains(Permissions.UsersManage))
            throw new BusinessException("Vai trò hệ thống phải giữ quyền quản lý người dùng.");
        await ApplyAsync(role, input);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var role = await db.Roles.FindAsync(id);
        if (role is null) return NotFound();
        if (role.IsSystem) throw new BusinessException("Không thể xoá vai trò hệ thống.");
        db.Roles.Remove(role);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task ApplyAsync(Role role, RoleInput input)
    {
        if (await db.Roles.AnyAsync(r => r.Name == input.Name && r.Id != role.Id))
            throw new BusinessException($"Vai trò \"{input.Name}\" đã tồn tại.");
        var unknown = input.Permissions.Except(Permissions.All.Select(p => p.Code)).ToList();
        if (unknown.Count > 0) throw new BusinessException($"Quyền không hợp lệ: {string.Join(", ", unknown)}");

        role.Name = input.Name.Trim();
        role.Description = input.Description;
        role.Permissions.RemoveAll(p => !input.Permissions.Contains(p.Permission));
        foreach (var code in input.Permissions.Distinct().Where(c => role.Permissions.All(p => p.Permission != c)))
            role.Permissions.Add(new RolePermission { Permission = code });
    }
}
