namespace BaaS.Models;

public class ProvisioningResult
{
    public string? TableName { get; set; }

    public List<string> Columns { get; set; } = [];

    public List<Dictionary<string, string>> SampleData { get; set; } = [];

    public List<SchemaColumnDefinition> Schema { get; set; } = [];

    public int RowsInserted { get; set; }

    public List<Dictionary<string, object?>> PreviewRows { get; set; } = [];

    public GeneratedApiOperations Api { get; set; } = new();

    public string Status { get; set; } = string.Empty;

    public string? Error { get; set; }
}
