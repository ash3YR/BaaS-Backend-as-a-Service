using System.Security.Cryptography;
using BaaS.Data;
using BaaS.Models;
using Microsoft.EntityFrameworkCore;

namespace BaaS.Services;

public class UserAccountService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly PasswordHashService _passwordHashService;

    public UserAccountService(ApplicationDbContext dbContext, PasswordHashService passwordHashService)
    {
        _dbContext = dbContext;
        _passwordHashService = passwordHashService;
    }

    public async Task<UserSessionResponse> RegisterAsync(AuthRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        ValidatePassword(request.Password);

        var exists = await _dbContext.AppUsers.AnyAsync(user => user.Email == email, cancellationToken);
        if (exists)
        {
            throw new ArgumentException("An account with this email already exists.");
        }

        var (hash, salt) = _passwordHashService.HashPassword(request.Password);
        var user = new AppUser
        {
            Email = email,
            PasswordHash = hash,
            PasswordSalt = salt,
            AdminApiKey = GenerateApiKey(),
            ReadOnlyApiKey = GenerateApiKey()
        };

        _dbContext.AppUsers.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToSession(user);
    }

    public async Task<UserSessionResponse> LoginAsync(AuthRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _dbContext.AppUsers.FirstOrDefaultAsync(u => u.Email == email, cancellationToken)
            ?? throw new ArgumentException("Invalid email or password.");

        if (!_passwordHashService.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            throw new ArgumentException("Invalid email or password.");
        }

        return ToSession(user);
    }

    public async Task<AppUser?> FindByApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AppUsers.FirstOrDefaultAsync(
            user => user.AdminApiKey == apiKey || user.ReadOnlyApiKey == apiKey,
            cancellationToken);
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.");
        }

        return email.Trim().ToLowerInvariant();
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            throw new ArgumentException("Password must be at least 6 characters long.");
        }
    }

    private static string GenerateApiKey()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
    }

    private static UserSessionResponse ToSession(AppUser user)
    {
        return new UserSessionResponse
        {
            UserId = user.Id,
            Email = user.Email,
            AdminApiKey = user.AdminApiKey,
            ReadOnlyApiKey = user.ReadOnlyApiKey
        };
    }
}
