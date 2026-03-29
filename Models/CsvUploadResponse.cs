namespace BaaS.Models;

public class CsvUploadResponse
{
    public List<string> Columns { get; set; } = [];

    public List<Dictionary<string, string>> SampleData { get; set; } = [];

    public List<SchemaColumnDefinition> Schema { get; set; } = [];
}
