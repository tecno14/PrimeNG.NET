# Changelog

## Unreleased / next tag

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
