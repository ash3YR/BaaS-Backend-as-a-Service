using BaaS.Models;
using Microsoft.Extensions.Options;

namespace BaaS.Services;

public class SwaggerConfigurationService
{
    private readonly IOptions<ApiKeySettings> _apiKeyOptions;

    public SwaggerConfigurationService(IOptions<ApiKeySettings> apiKeyOptions)
    {
        _apiKeyOptions = apiKeyOptions;
    }

    public bool IsSwaggerEnabled()
    {
        return true;
    }

    public bool HasApiKeySecurity()
    {
        var settings = _apiKeyOptions.Value;
        return !string.IsNullOrWhiteSpace(settings.Admin) && !string.IsNullOrWhiteSpace(settings.ReadOnly);
    }

    public bool HasGroupsConfigured()
    {
        return true;
    }
}
