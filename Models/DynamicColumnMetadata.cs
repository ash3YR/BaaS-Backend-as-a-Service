namespace BaaS.Models;

public class DynamicColumnMetadata
{
    public string Name { get; set; } = string.Empty;

    public string DatabaseType { get; set; } = string.Empty;

    public bool IsNullable { get; set; }

    public bool IsPrimaryKey { get; set; }
}
