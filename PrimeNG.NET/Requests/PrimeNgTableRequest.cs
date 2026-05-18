namespace PrimeNG.NET.Requests;

public class PrimeNgTableRequest
{
    public int First { get; set; } = 0;     // starting row index
    public int Rows { get; set; } = 10;     // page size
    public string? SortField { get; set; }
    public int? SortOrder { get; set; }     // 1 = asc, -1 = desc
    public Dictionary<string, PrimeNgFilter>? Filters { get; set; }
}
