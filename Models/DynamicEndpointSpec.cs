namespace BaaS.Models;

public class DynamicEndpointSpec
{
    public string Method { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public object? QuerySchema { get; set; }

    public object? RequestBodySchema { get; set; }

    public object? ResponseSchema { get; set; }
}
