namespace PrimeNG.NET.Tests.TestEntities;

public enum TestProductKind
{
    Standard = 0,
    Premium = 1,
    Clearance = 2,
}

public class TestProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public bool IsActive { get; set; }
    public Guid Sku { get; set; }
    public TestProductKind Kind { get; set; }

    /// <summary>CLR-only computed member — not mapped by EF (used to assert skip behavior).</summary>
    public string DisplayLabel => $"{Name} ({Kind})";
}
