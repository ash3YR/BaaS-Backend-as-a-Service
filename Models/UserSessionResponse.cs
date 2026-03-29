namespace BaaS.Models;

public class UserSessionResponse
{
    public int UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string AdminApiKey { get; set; } = string.Empty;

    public string ReadOnlyApiKey { get; set; } = string.Empty;
}
