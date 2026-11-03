# Changelog

## 1.2.1

### Fixed

- **Enum filters** — `Convert.ChangeType` cannot parse enums; filters now use `Enum.TryParse` (name or numeric, case-insensitive) so properties like `UnitOfMeasure` work with PrimeNG `equals` / `notEquals`.
- **Unmapped CLR members** — when the query is EF-backed, filter/sort on properties not in the EF model (e.g. computed `Slug` / `DisplayLabel`) are skipped with a warning instead of failing SQL translation (HTTP 500).

### Notes

- Guid conversion now uses `Guid.TryParse` (invalid values skip the filter, same as other bad conversions).
- Non-EF / projection `IQueryable` types without an entity mapping are unchanged (no unmapped skip).

## 1.2.0

### Breaking

- **String sort default is lexical** for all EF providers (SQL Server, PostgreSQL, SQLite).  
  Previously (≤ 1.1.3), string columns always used SQL Server `PATINDEX` natural-prefix ordering, which **does not translate on PostgreSQL** and pulled in `Microsoft.EntityFrameworkCore.SqlServer` as a library dependency.

### Added

- `naturalStringSort` parameter on `ApplyPrimeNgSorting` and `PrimeNgTableProcessor.ProcessAsync` (default `false`).
- When `naturalStringSort: true`:
  - **SQL Server** — natural sort via `PatIndex` (resolved by reflection; no hard package dependency).
  - **PostgreSQL (Npgsql)** — natural sort via translated `Regex` expressions.
  - **Other providers** — lexical fallback (with optional warning log).

### Removed

- Package dependency on `Microsoft.EntityFrameworkCore.SqlServer` from the library project.
