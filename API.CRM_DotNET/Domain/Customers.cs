namespace API.CRM_DotNET.Domain;

public enum CustomerType { Company = 0, Individual = 1 }

public enum CustomerStatus { Lead = 0, Active = 1, Inactive = 2 }

public class Customer
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public CustomerType Type { get; set; }
    public CustomerStatus Status { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? TaxCode { get; set; }
    public string? Notes { get; set; }
    public int? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Contact> Contacts { get; set; } = new();
    public List<Activity> Activities { get; set; } = new();
}

public class Contact
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string FullName { get; set; } = "";
    public string? Position { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
}

public enum ActivityType { Call = 0, Email = 1, Meeting = 2, Note = 3, Task = 4 }

/// <summary>An interaction with a customer (call, meeting, email...).</summary>
public class Activity
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public ActivityType Type { get; set; }
    public string Subject { get; set; } = "";
    public string? Content { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public int? UserId { get; set; }
    public User? User { get; set; }
}
