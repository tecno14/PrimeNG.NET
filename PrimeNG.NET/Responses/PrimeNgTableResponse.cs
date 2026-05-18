namespace PrimeNG.NET.Responses;

public class PrimeNgTableResponse<T>
{
    public IEnumerable<T> Data { get; set; } = [];
    public int TotalRecords { get; set; }
}
