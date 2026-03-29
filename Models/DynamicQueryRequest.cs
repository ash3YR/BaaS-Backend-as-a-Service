namespace BaaS.Models;

public class DynamicQueryRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    public string? SortBy { get; set; }

    public string SortDirection { get; set; } = "asc";

    public string? Search { get; set; }

    public Dictionary<string, string>? Filters { get; set; }
}
