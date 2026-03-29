namespace BaaS.Models;

public class AppUser
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string PasswordSalt { get; set; } = string.Empty;

    public string AdminApiKey { get; set; } = string.Empty;

    public string ReadOnlyApiKey { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<ProvisionedTableRecord> ProvisionedTables { get; set; } = [];
}
