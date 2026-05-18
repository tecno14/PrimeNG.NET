# PrimeNG.NET

Server-side helpers for [PrimeNG](https://primeng.org/) **Table** lazy loading in ASP.NET Core. Map the same `first`, `rows`, `sortField`, `sortOrder`, and `filters` payload your Angular app already sends into **EF Core** queries—filtering, sorting, and paging—without reimplementing that logic in every API project.

## Why use this?

PrimeNG tables with `[lazy]="true"` POST or GET a predictable request shape. Each new backend endpoint usually repeats the same steps: parse filters, build dynamic `Where` clauses, apply `OrderBy`, `Skip`/`Take`, and return `{ data, totalRecords }`. **PrimeNG.NET** centralizes that pipeline so you bind the request DTO once and call a single processor (or compose extension methods yourself).

## Features

- **One-call processing** — `PrimeNgTableProcessor.ProcessAsync` runs filter → count → sort → page → materialize.
- **Composable extensions** — `ApplyPrimeNgFiltering`, `ApplyPrimeNgSorting`, `ApplyPrimeNgPaging` on any `IQueryable<T>`.
- **EF Core–friendly** — string filters use `EF.Functions.Like` so predicates translate to SQL.
- **Type-aware filters** — property lookup is case-insensitive; values are converted for `string`, numeric, `bool`, `Guid`, and nullable variants.
- **Optional logging** — pass `ILogger` to log filter/sort issues without failing the request.

## Requirements

| Dependency | Version (this repo) |
|------------|---------------------|
| .NET | 10.0 |
| [Entity Framework Core](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore) | 10.x |
| [System.Linq.Dynamic.Core](https://www.nuget.org/packages/System.Linq.Dynamic.Core) | 1.7.x |

The library is built for **EF Core** (`CountAsync`, `ToListAsync`, `EF.Property`, `EF.Functions.Like`). In-memory `IQueryable` providers may not support all expressions.

## Installation

After the package is published to NuGet:

```bash
dotnet add package PrimeNG.NET
```

For local development, add a project reference:

```bash
dotnet add reference path/to/PrimeNG.NET/PrimeNG.NET.csproj
```

## Quick start

### 1. Model (EF entity)

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
}
```

### 2. API endpoint (minimal)

```csharp
using Microsoft.AspNetCore.Mvc;
using PrimeNG.NET.Processing;
using PrimeNG.NET.Requests;
using PrimeNG.NET.Responses;

[ApiController]
[Route("api/[controller]")]
public class ProductsController(AppDbContext db) : ControllerBase
{
    [HttpPost("lazy")]
    public async Task<PrimeNgTableResponse<Product>> GetLazy(
        [FromBody] PrimeNgTableRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = db.Products.AsQueryable();

        return await PrimeNgTableProcessor.ProcessAsync(query, request);
    }
}
```

The response matches what PrimeNG expects for lazy tables: a page of rows plus the **total count after filters** (before paging).

### 3. Angular / PrimeNG (lazy table)

Bind the table’s lazy-load event to your API and forward the event object (or map fields explicitly):

```typescript
// component.ts
loadProducts(event: TableLazyLoadEvent) {
  const body = {
    first: event.first ?? 0,
    rows: event.rows ?? 10,
    sortField: event.sortField ?? null,
    sortOrder: event.sortOrder ?? null,
    filters: event.filters ?? null
  };

  this.http.post<PrimeNgTableResponse<Product>>('/api/products/lazy', body)
    .subscribe(res => {
      this.products = res.data;
      this.totalRecords = res.totalRecords;
    });
}
```

```html
<p-table
  [value]="products"
  [lazy]="true"
  (onLazyLoad)="loadProducts($event)"
  [paginator]="true"
  [rows]="10"
  [totalRecords]="totalRecords"
  [loading]="loading">
  <!-- columns -->
</p-table>
```

Use the same JSON property names as your C# models (`PrimeNgTableRequest` / `PrimeNgTableResponse`). Configure `System.Text.Json` naming (camelCase) in `Program.cs` if needed:

```csharp
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
```

## Request and response contract

### `PrimeNgTableRequest`

| Property | PrimeNG source | Description |
|----------|----------------|-------------|
| `First` | `event.first` | Zero-based index of the first row to return. |
| `Rows` | `event.rows` | Page size. See [Paging behavior](#paging-behavior). |
| `SortField` | `event.sortField` | Property name to sort by (must exist on `T`). |
| `SortOrder` | `event.sortOrder` | `1` = ascending, any other value (e.g. `-1`) = descending. |
| `Filters` | `event.filters` | Dictionary: **field name** → `{ value, matchMode }`. |

### `PrimeNgFilter`

```json
{
  "name": { "value": "lap", "matchMode": "contains" },
  "category": { "value": "Electronics", "matchMode": "equals" }
}
```

Filter keys must match a **public instance property** on `T` (case-insensitive). Empty or whitespace values are skipped.

### `PrimeNgTableResponse<T>`

| Property | PrimeNG usage |
|----------|----------------|
| `Data` | Rows for the current page. |
| `TotalRecords` | Total row count **after filtering**, used for the paginator. |

## Advanced usage

### Compose steps manually

```csharp
var filtered = query.ApplyPrimeNgFiltering(request, logger);
var total = await filtered.CountAsync(cancellationToken);

var sorted = filtered.ApplyPrimeNgSorting(request, logger);
var paged = sorted.ApplyPrimeNgPaging(request);

var data = await paged.ToListAsync(cancellationToken);

return new PrimeNgTableResponse<Product>
{
    Data = data,
    TotalRecords = total
};
```

Order matches `ProcessAsync`: always count on the **filtered** query before sort/page.

### Custom page size cap (`TakeLast`)

`ApplyPrimeNgPaging` accepts `maxRows` (default `10`) when `Rows` is negative: it returns the last *N* rows, capped by `maxRows`. This supports non-standard client payloads; typical PrimeNG tables use positive `Rows` with `First`.

## Filter match modes

`MatchMode` is parsed case-insensitively into `PrimeNgMatchMode`.

| Match mode | String properties | Non-string (`int`, `bool`, `Guid`, etc.) |
|------------|-------------------|------------------------------------------|
| `contains` | SQL `LIKE '%value%'` (value lowercased) | — |
| `startsWith` | SQL `LIKE 'value%'` | — |
| `equals` | Exact match (value lowercased) | Equality |
| `notEquals` | — | Inequality |
| `notContains`, `endsWith` | Defined on enum; not applied yet | — |

For **strings**, filter values are compared in lowercase; ensure database collation or normalization matches your expectations for case sensitivity.

For **non-strings**, only `equals` and `notEquals` are applied; other modes are ignored.

## Paging behavior

| `Rows` | Behavior |
|--------|----------|
| `> 0` | `Skip(First).Take(Rows)` |
| `0` | Returns no rows (`Take(0)`) |
| `< 0` | `TakeLast(min(abs(Rows), maxRows))` — default `maxRows` is 10 |

## Project structure

```
PrimeNG.NET/
├── Extensions/PrimeNgQueryableExtensions.cs   # Filter, sort, page
├── Processing/PrimeNgTableProcessor.cs          # End-to-end pipeline
├── Requests/PrimeNgTableRequest.cs
├── Requests/PrimeNgFilter.cs
├── Responses/PrimeNgTableResponse.cs
└── PrimeNgMatchMode.cs
```

## Building locally

```bash
dotnet build PrimeNG.NET/PrimeNG.NET.csproj
```

## License

This project is licensed under the [MIT License](LICENSE).

## Acknowledgments

Inspired by [PrimeNG Table](https://primeng.org/table) lazy-load events. Not affiliated with PrimeTek or the PrimeNG project.
