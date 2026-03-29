namespace BaaS.Models;

public class DynamicCrudTestResponse
{
    public string? TableName { get; set; }

    public List<Dictionary<string, object?>> InitialRows { get; set; } = [];

    public Dictionary<string, object?>? SingleRow { get; set; }

    public Dictionary<string, object?>? AfterInsert { get; set; }

    public Dictionary<string, object?>? AfterUpdate { get; set; }

    public string? AfterDelete { get; set; }

    public string? Status { get; set; }

    public string? Error { get; set; }
}
