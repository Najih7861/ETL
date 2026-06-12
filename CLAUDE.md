# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is
A file-based CSV→CSV ETL tool on **.NET 10**. It scans an input folder for `*.csv`,
applies a config-driven column mapping/conversion, and writes results to an output folder.
Same engine runs two ways: a CLI and a Web API.

## Commands
The solution file is the **new XML format** (`EtlTool.slnx`) — pass it explicitly; there is no `.sln`.

```powershell
dotnet build EtlTool.slnx                 # build all 4 projects
dotnet test  EtlTool.slnx                 # run all tests (xUnit + FluentAssertions)

# run a single test by name substring:
dotnet test --filter "FullyQualifiedName~Run_with_normalizeWhitespace"

# run the CLI (must be run from repo root so storage/ + config/ resolve):
dotnet run --project src/EtlTool.Cli
dotnet run --project src/EtlTool.Cli -- --input storage/input --output storage/output --invalid storage/invalid --config config/mapping.json --on-error skip

# run the API (Swagger UI at http://localhost:5055 -> redirects to /swagger):
dotnet run --project src/EtlTool.Api --launch-profile http
```

**Build gotcha:** a running `EtlTool.Api`/`EtlTool.Cli` process holds a lock on
`EtlTool.Core.dll`, which makes `dotnet build` fail with MSB3027 "file in use". Stop it first:
`Get-Process EtlTool.Api,EtlTool.Cli -ErrorAction SilentlyContinue | Stop-Process -Force`.

## Architecture — Extract → Transform → Load, streamed
The engine lives entirely in **`EtlTool.Core`**, which depends only on **CsvHelper** and the
**`ILogger` abstraction** (no Serilog/ASP.NET). The CLI and API are thin hosts that supply a
logger and the folder paths — this is why the same engine serves both unchanged.

Rows stream one at a time as `IEnumerable<CsvRow>`, so memory stays flat on large files.

Orchestration:
- **`FolderEtlProcessor.ProcessFolder(...)`** — loads the mapping **once**, enumerates
  `*.csv` in the input dir, runs `FileEtlProcessor` per file, aggregates a `BatchEtlResult`.
- **`FileEtlProcessor.ProcessFile(...)`** — the core loop: extract row → global filters →
  `TransformRow` → route to output split(s) → write. Opens one `CsvLoader` per output split
  plus a lazy `InvalidRecordWriter`.
- Stages: `Extract/CsvExtractor` (lazy read) → `Transform/RowTransformer` (filter + map) →
  `Load/CsvLoader` (write). `EtlMapping`/`EtlMappingLoader` hold the config; `Models/` holds
  `CsvRow`, `EtlResult`, `ErrorPolicy`.

## Config-driven mapping (`config/mapping.json`)
**`EtlMappingLoader.LoadFromFile()` is the ONLY place the JSON is read/parsed** (System.Text.Json,
case-insensitive). Everywhere else works with the in-memory `EtlMapping` object. Schema:
- `source` — `delimiter`, `hasHeader`, `encoding`, `normalizeWhitespace` (when true, every
  field is trimmed AND internal whitespace runs collapsed, on read, for all columns).
- `columns` — output columns, in order. `source` (single) OR `sources` + `separator`
  (combine, e.g. firstname+lastname→Fullname); plus `target`, `type`
  (int/decimal/string/date/bool), `transform` (trim/upper/lower/normalizeSpaces),
  `default`, `sourceFormat`/`format` (dates).
- `columns[].validation` — optional per-column rules applied **after** type conversion:
  `required`, `pattern` (regex), `minLength`/`maxLength`, `min`/`max` (numeric),
  `allowedValues`. A failing value throws and routes the row to the invalid folder (empty
  optional values pass; only `required` rejects empties).
- `filters` — global include/exclude rules applied before mapping.
- `outputs` — **file splits**: each `{ suffix, filter }` writes `<name>_<suffix>.csv` with
  only the rows matching its filter. No `outputs` ⇒ single `<name>.out.csv` with all rows.

### Three things that are easy to get wrong
1. **Output columns = exactly what `columns` lists.** To add/remove an output column, edit
   `columns` — there is no separate "select". `RowTransformer.TransformRow` builds the output
   row solely from `mapping.Columns`.
2. **Splits filter on the INPUT row, not the output.** `FileEtlProcessor` calls
   `RowTransformer.RowMatchesFilter(inputRow, split.Filter)`. So a column used for splitting
   (e.g. `gender`) can be absent from the output yet still drive the split.
3. **Invalid rows** (transform/conversion error, failed `validation`, or matched no output
   split) are written to
   `storage/invalid/<name>_invalid.csv` with the **original input columns + an `InvalidReason`**
   column. The invalid file is created lazily (clean inputs produce none). This is distinct
   from `RowsSkipped`, which means intentionally excluded by a global `filter`.

## Extending the engine
- **New transform:** implement `IValueTransform`, register it in `TransformRegistry`. Shared
  whitespace logic is in `Transform/Whitespace.cs`.
- **New filter op:** add a `case` in `RowTransformer.RowMatchesFilter` AND to `KnownFilterOps`
  in `EtlMappingLoader` (validation rejects unknown ops).
- **New column type:** add a `case` in `ValueTypeConverter.ConvertToType` AND to
  `KnownColumnTypes` in `EtlMappingLoader`.
- **New validation rule:** add a field to `ColumnValidation` (in `EtlMapping.cs`), a check in
  `ValueValidator.Validate` (throw `ValueValidationException` on failure), and—if it needs
  config-time checks—a guard in `EtlMappingLoader.ValidateColumnValidation`.

## Hosts
- **CLI** (`EtlTool.Cli/Program.cs`): manual arg parsing (no System.CommandLine), Serilog to
  console + rolling `logs/etl-YYYYMMDD.log`. Defaults: `storage/input`, `storage/output`,
  `storage/invalid`, `config/mapping.json`. Run from repo root (relative paths).
- **API** (`EtlTool.Api`): `POST /api/etl/run` takes **no body** — it just processes the
  configured input folder. Also `GET /api/etl/files`, `GET /api/etl/health`. Layered
  Controller → `IEtlService`/`EtlService` → `FolderEtlProcessor`. Folder paths come from the
  `Etl` section of `appsettings.json` (`EtlOptions`). At startup `Program.cs` calls
  `Directory.SetCurrentDirectory(FindRepoRoot(...))` (walks up to the `.slnx`) so relative
  paths resolve regardless of where the API is launched.

## Conventions
- Class/method names are stage-specific and greppable: `ExtractRows`, `TransformRow`,
  `RowMatchesFilter`, `WriteRow`, `ProcessFile`, `ProcessFolder`, `LoadFromFile`.
- Output CSVs are UTF-8 **without BOM** (`CsvLoader.ResolveEncoding`).
- In `tests/`, the `.gitignore` tracks only `.cs` and `.json` (the `.csproj` is intentionally ignored).
