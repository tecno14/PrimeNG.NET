using PrimeNG.NET.Tests.TestEntities;

namespace PrimeNG.NET.Tests.Infrastructure;

public static class TestData
{
    public static readonly Guid SkuAlpha = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid SkuBeta = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public static IReadOnlyList<TestProduct> CreateProducts() =>
    [
        new() { Id = 1, Name = "1 alpha", Category = "Sort", Price = 50m, Quantity = 1, IsActive = true, Sku = SkuAlpha },
        new() { Id = 2, Name = "2 beta", Category = "Sort", Price = 30m, Quantity = 2, IsActive = true, Sku = SkuBeta },
        new() { Id = 3, Name = "10 gamma", Category = "Sort", Price = 10m, Quantity = 10, IsActive = false, Sku = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc") },
        new() { Id = 4, Name = "apple", Category = "Sort", Price = 5m, Quantity = 3, IsActive = true, Sku = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd") },
        new() { Id = 5, Name = "09 item", Category = "Sort", Price = 20m, Quantity = 9, IsActive = true, Sku = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") },
        new() { Id = 6, Name = "20 delta", Category = "Sort", Price = 40m, Quantity = 20, IsActive = false, Sku = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff") },
        new() { Id = 7, Name = "Laptop Pro", Category = "Electronics", Price = 999.99m, Quantity = 5, IsActive = true, Sku = Guid.Parse("11111111-1111-1111-1111-111111111111") },
        new() { Id = 8, Name = "Desk Lamp", Category = "Home", Price = 49.99m, Quantity = 12, IsActive = true, Sku = Guid.Parse("22222222-2222-2222-2222-222222222222") },
        new() { Id = 9, Name = "Wireless Mouse", Category = "Electronics", Price = 29.99m, Quantity = 25, IsActive = false, Sku = Guid.Parse("33333333-3333-3333-3333-333333333333") },
        new() { Id = 10, Name = "Notebook", Category = "Office", Price = 9.99m, Quantity = 100, IsActive = true, Sku = Guid.Parse("44444444-4444-4444-4444-444444444444") },
    ];

    public static IReadOnlyList<string> NaturalSortNamesAscending { get; } =
    [
        "1 alpha",
        "2 beta",
        "09 item",
        "10 gamma",
        "20 delta",
        "apple",
    ];
}
