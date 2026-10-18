using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace API.CRM_DotNET.Auth;

public class JwtOptions
{
    public string Issuer { get; set; } = "crm-api";
    public string Audience { get; set; } = "crm-web";

    /// <summary>HMAC-SHA256 signing key, at least 32 characters.</summary>
    public string Key { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 480;

    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Key));
}

public class JwtTokenService(Microsoft.Extensions.Options.IOptions<JwtOptions> options)
{
    private readonly JwtOptions _opt = options.Value;

    public (string Token, DateTime ExpiresAt) Create(int userId, string username, string fullName,
        IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new("name", fullName),
        };
        claims.AddRange(roles.Select(r => new Claim("role", r)));
        claims.AddRange(permissions.Select(p => new Claim(Permissions.ClaimType, p)));

        var expires = DateTime.UtcNow.AddMinutes(_opt.ExpiryMinutes);
        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(_opt.SigningKey, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
