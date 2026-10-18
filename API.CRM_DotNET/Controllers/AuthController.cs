using API.CRM_DotNET.Auth;
using API.CRM_DotNET.Data;
using API.CRM_DotNET.Domain;
using API.CRM_DotNET.Dtos;
using API.CRM_DotNET.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.CRM_DotNET.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(CrmDbContext db, JwtTokenService tokens, IPasswordHasher<User> hasher) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user is null || !user.IsActive ||
            hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });

        var me = await LoadCurrentUserAsync(user.Id);
        var (token, expiresAt) = tokens.Create(user.Id, user.Username, user.FullName, me.Roles, me.Permissions);
        return new LoginResponse(token, expiresAt, me);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<CurrentUserDto> Me() => await LoadCurrentUserAsync(User.GetUserId()!.Value);

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await db.Users.FindAsync(User.GetUserId()!.Value) ?? throw new BusinessException("Không tìm thấy người dùng.");
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            throw new BusinessException("Mật khẩu hiện tại không đúng.");
        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<CurrentUserDto> LoadCurrentUserAsync(int userId)
    {
        var user = await db.Users.AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.Permissions)
            .FirstAsync(u => u.Id == userId);
        var roles = user.UserRoles.Select(ur => ur.Role).ToList();
        return new CurrentUserDto(
            user.Id, user.Username, user.FullName, user.Email,
            roles.Select(r => r.Name).ToArray(),
            roles.SelectMany(r => r.Permissions).Select(p => p.Permission).Distinct().Order().ToArray());
    }
}
