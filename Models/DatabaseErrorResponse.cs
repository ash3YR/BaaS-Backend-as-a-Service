namespace BaaS.Models;

public class DatabaseErrorResponse
{
    public string Status { get; set; } = "Database unavailable";

    public string Error { get; set; } = string.Empty;
}
