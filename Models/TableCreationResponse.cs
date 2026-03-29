namespace BaaS.Models;

public class TableCreationResponse
{
    public string? TableName { get; set; }

    public int RowsInserted { get; set; }

    public List<SchemaColumnDefinition> Schema { get; set; } = [];

    public string Status { get; set; } = string.Empty;

    public string? Error { get; set; }
}
