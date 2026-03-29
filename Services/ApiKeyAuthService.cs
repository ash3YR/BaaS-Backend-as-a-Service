using BaaS.Models;

namespace BaaS.Services;

public class ApiKeyAuthService
{
    private readonly UserAccountService _userAccountService;

    public ApiKeyAuthService(UserAccountService userAccountService)
    {
        _userAccountService = userAccountService;
    }

    public async Task<ApiKeyAuthResult> EvaluateAsync(string? apiKey, string method, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ApiKeyAuthResult.Unauthorized();
        }

        var user = await _userAccountService.FindByApiKeyAsync(apiKey, cancellationToken);
        if (user is null)
        {
            return ApiKeyAuthResult.Unauthorized();
        }

        if (string.Equals(apiKey, user.AdminApiKey, StringComparison.Ordinal))
        {
            return ApiKeyAuthResult.Authorized("ADMIN", user.Id, user.Email);
        }

        if (string.Equals(apiKey, user.ReadOnlyApiKey, StringComparison.Ordinal))
        {
            return string.Equals(method, HttpMethods.Get, StringComparison.OrdinalIgnoreCase)
                ? ApiKeyAuthResult.Authorized("READ_ONLY", user.Id, user.Email)
                : ApiKeyAuthResult.Forbidden(user.Id, user.Email);
        }

        return ApiKeyAuthResult.Unauthorized();
    }
}

public sealed class ApiKeyAuthResult
{
    public bool IsAuthorized { get; init; }

    public bool IsForbidden { get; init; }

    public string? Role { get; init; }

    public int? UserId { get; init; }

    public string? Email { get; init; }

    public static ApiKeyAuthResult Authorized(string role, int userId, string email) => new()
    {
        IsAuthorized = true,
        Role = role,
        UserId = userId,
        Email = email
    };

    public static ApiKeyAuthResult Forbidden(int userId, string email) => new()
    {
        IsForbidden = true,
        UserId = userId,
        Email = email
    };

    public static ApiKeyAuthResult Unauthorized() => new();
}
