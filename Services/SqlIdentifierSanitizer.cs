using System.Text.RegularExpressions;

namespace BaaS.Services;

public static class SqlIdentifierSanitizer
{
    private static readonly Regex SafeIdentifierRegex = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    public static string Sanitize(string identifier, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(identifier) || !SafeIdentifierRegex.IsMatch(identifier))
        {
            throw new ArgumentException($"Invalid {parameterName} '{identifier}'. Only letters, numbers, and underscores are allowed.");
        }

        return identifier.Trim();
    }
}
