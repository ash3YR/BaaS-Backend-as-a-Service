namespace BaaS.Models;

public class ProvisionedTableRecord
{
    public int Id { get; set; }

    public int AppUserId { get; set; }

    public AppUser? AppUser { get; set; }

    public string TableName { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
