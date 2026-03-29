namespace BaaS.Models;

public class DynamicTableOpenApiSpec
{
    public string TableName { get; set; } = string.Empty;

    public List<DynamicColumnMetadata> Columns { get; set; } = [];

    public List<DynamicEndpointSpec> Endpoints { get; set; } = [];
}
