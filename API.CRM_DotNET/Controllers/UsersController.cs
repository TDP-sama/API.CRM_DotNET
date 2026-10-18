using API.CRM_DotNET.Auth;
using API.CRM_DotNET.Data;
using API.CRM_DotNET.Domain;
using API.CRM_DotNET.Dtos;
using API.CRM_DotNET.Infrastructure;
using DevExtreme.AspNet.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.CRM_DotNET.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController(CrmDbContext db, IPasswordHasher<User> hasher) : ControllerBase
{
    /// <summary>Any signed-in user may load the user list (it feeds "assigned to" lookups).</summary>
    [Authorize]
    [HttpGet]
    public async Task<object> Get(DataSourceLoadOptions loadOptions)
    {
        // Few rows and a collection projection: load in memory, then let DataSourceLoader shape it.
        var users = await db.Users.AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .ToListAsync();
        var dtos = users.Select(u => new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            FullName = u.FullName,
            Email = u.Email,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
            RoleIds = u.UserRoles.Select(r => r.RoleId).ToList(),
            RoleNames = string.Join(", ", u.UserRoles.Select(r => r.Role.Name)),
        }).ToList();
        return DataSourceLoader.Load(dtos, loadOptions);
    }

    [Authorize(Policy = Permissions.UsersManage)]
    [HttpPost]
    public async Task<IActionResult> Create(UserInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Password))
            throw new BusinessException("Vui lòng nhập mật khẩu cho người dùng mới.");
        var user = new User();
        await ApplyAsync(user, input);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return Ok(new { user.Id });
    }

    [Authorize(Policy = Permissions.UsersManage)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UserInput input)
    {
        var user = await db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        if (id == User.GetUserId() && !input.IsActive)
            throw new BusinessException("Không thể tự khoá tài khoản của chính mình.");
        await ApplyAsync(user, input);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = Permissions.UsersManage)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (id == User.GetUserId()) throw new BusinessException("Không thể xoá tài khoản đang đăng nhập.");
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        db.Users.Remove(user);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new BusinessException("Người dùng đã phát sinh dữ liệu, hãy khoá tài khoản thay vì xoá.");
        }
        return NoContent();
    }

    private async Task ApplyAsync(User user, UserInput input)
    {
        if (!string.IsNullOrEmpty(input.Password) && input.Password.Length < 6)
            throw new BusinessException("Mật khẩu tối thiểu 6 ký tự.");
        if (await db.Users.AnyAsync(u => u.Username == input.Username && u.Id != user.Id))
            throw new BusinessException($"Tên đăng nhập \"{input.Username}\" đã tồn tại.");

        user.Username = input.Username.Trim();
        user.FullName = input.FullName.Trim();
        user.Email = input.Email;
        user.IsActive = input.IsActive;
        if (!string.IsNullOrWhiteSpace(input.Password))
            user.PasswordHash = hasher.HashPassword(user, input.Password);

        user.UserRoles.RemoveAll(ur => !input.RoleIds.Contains(ur.RoleId));
        foreach (var roleId in input.RoleIds.Distinct().Where(r => user.UserRoles.All(ur => ur.RoleId != r)))
            user.UserRoles.Add(new UserRole { RoleId = roleId });
    }
}
