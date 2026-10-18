using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace API.CRM_DotNET.Auth;

/// <summary>
/// Builds policies on the fly from their names: [Authorize(Policy = "Orders.View")] requires the
/// "perm" claim Orders.View, and "A|B" requires any one of A or B.
/// </summary>
public class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.Contains('.')) return _fallback.GetPolicyAsync(policyName);

        var codes = policyName.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireAssertion(ctx => codes.Any(c => ctx.User.HasClaim(Permissions.ClaimType, c)))
            .Build();
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}
