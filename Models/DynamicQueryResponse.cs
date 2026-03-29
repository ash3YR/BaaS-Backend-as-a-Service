namespace BaaS.Models;

public class DynamicQueryResponse
{
    public List<Dictionary<string, object?>> Data { get; set; } = [];

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalRows { get; set; }

    public int TotalPages { get; set; }

    public string? SortBy { get; set; }

    public string SortDirection { get; set; } = "asc";

    public string? Search { get; set; }

    public Dictionary<string, string> Filters { get; set; } = [];
}
