namespace BaaS.Models;

public class AuthTestResponse
{
    public string AdminAccess { get; set; } = string.Empty;

    public string ReadOnlyGet { get; set; } = string.Empty;

    public string ReadOnlyPost { get; set; } = string.Empty;

    public string InvalidKey { get; set; } = string.Empty;
}
