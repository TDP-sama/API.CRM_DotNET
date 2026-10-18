namespace API.CRM_DotNET.Domain;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<UserRole> UserRoles { get; set; } = new();
}

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    // Built-in roles cannot be deleted (e.g. Admin).
    public bool IsSystem { get; set; }

    public List<RolePermission> Permissions { get; set; } = new();
    public List<UserRole> UserRoles { get; set; } = new();
}

public class UserRole
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
}

public class RolePermission
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public string Permission { get; set; } = "";
}
