namespace PrimeNG.NET.Tests.TestEntities;

public class TestProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public bool IsActive { get; set; }
    public Guid Sku { get; set; }
}
