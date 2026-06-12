# EtlTool — File-Based CSV → CSV ETL

A small, config-driven ETL tool built on **.NET 10**. It scans a folder of CSV files,
applies column mapping and value conversion defined in a JSON file, and writes the results
back out as CSV. The same engine runs two ways: a **command-line tool** and a **Web API**.

It streams rows one at a time, so memory stays flat even on large files.

## Features
- **Folder batch** — processes every `*.csv` in the input folder in one run.
- **Config-driven mapping** — rename, reorder, and select columns from a JSON file; no code changes.
- **Combine columns** — e.g. `firstname` + `lastname` → `Fullname` with a separator.
- **Type conversion** — `int`, `decimal`, `date` (with parse/output formats), `bool`, `string`.
- **Value transforms** — `trim`, `upper`, `lower`, `normalizeSpaces`.
- **Data validation** — per-column rules (`required`, `pattern`, `min`/`max`, length, allowed values); failing rows go to the invalid folder with the reason.
- **Whitespace cleanup** — `normalizeWhitespace` trims and collapses extra spaces in every field.
- **Row filters** — keep/drop rows by a condition (e.g. only `active`).
- **Output splitting** — write multiple files from one input (e.g. a male file and a female file).
- **Invalid records** — rows that can't be processed go to a separate `invalid` folder with a reason.
- **Logging** — console + rolling daily log files.

## Requirements
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Project layout
```
EtlTool.slnx                 Solution (new XML format)
config/mapping.json          The mapping/conversion rules (edit this)
storage/input/               Put source *.csv files here
storage/output/              Results are written here
storage/invalid/             Rows that couldn't be processed
logs/                        Run logs
src/EtlTool.Core/            The ETL engine (Extract → Transform → Load)
src/EtlTool.Cli/             Command-line host
src/EtlTool.Api/             Web API host
tests/EtlTool.Tests/         Unit + end-to-end tests
```

## Quick start (CLI)
Run from the repository root so the `storage/` and `config/` paths resolve:

```powershell
# 1. Put CSV files in storage/input/
# 2. Edit config/mapping.json to describe your mapping
# 3. Run:
dotnet run --project src/EtlTool.Cli
```

Results appear in `storage/output/`, invalid rows in `storage/invalid/`, and logs in `logs/`.

### CLI options
All are optional; defaults shown.

| Option | Default | Description |
|---|---|---|
| `-i, --input <dir>` | `storage/input` | Folder scanned for `*.csv` |
| `-o, --output <dir>` | `storage/output` | Where results are written |
| `--invalid <dir>` | `storage/invalid` | Where invalid rows are written |
| `-c, --config <file>` | `config/mapping.json` | The mapping config |
| `--on-error <mode>` | `skip` | `skip` (log & continue) or `fail` (stop on first error) |
| `-h, --help` | | Show help |

```powershell
dotnet run --project src/EtlTool.Cli -- --input data/in --output data/out --on-error fail
```

## Quick start (Web API)
```powershell
dotnet run --project src/EtlTool.Api --launch-profile http
```
Then open **http://localhost:5055** (redirects to Swagger UI).

| Method | Route | Description |
|---|---|---|
| `POST` | `/api/etl/run` | Runs ETL over the input folder. **No request body.** |
| `GET`  | `/api/etl/files` | Lists the `*.csv` files in the input folder |
| `GET`  | `/api/etl/health` | Liveness check |

Folder paths for the API are configured in `src/EtlTool.Api/appsettings.json` under the `Etl` section.

## The mapping file (`config/mapping.json`)
Everything about a run is described here. Example:

```json
{
  "source":  { "delimiter": ",", "hasHeader": true, "encoding": "utf-8", "normalizeWhitespace": true },
  "target":  { "delimiter": ",", "encoding": "utf-8", "writeHeader": true },

  "columns": [
    { "source": "customer_id", "target": "CustomerId", "type": "int" },
    { "sources": ["firstname", "lastname"], "separator": " ", "target": "Fullname", "transform": "normalizeSpaces" },
    { "source": "email", "target": "Email", "transform": "lower" }
  ],

  "outputs": [
    { "suffix": "male",   "filter": { "column": "gender", "op": "equals", "value": "male" } },
    { "suffix": "female", "filter": { "column": "gender", "op": "equals", "value": "female" } }
  ]
}
```

### `source` / `target`
`delimiter`, `encoding`, `hasHeader` (source), `writeHeader` (target). Set
`"normalizeWhitespace": true` to trim and collapse extra spaces in **every** input field.

### `columns` (output columns, in order)
| Field | Purpose |
|---|---|
| `source` | Input column to read from |
| `sources` + `separator` | Combine several input columns into one (e.g. names) |
| `target` | Output column name (required) |
| `type` | `int` \| `decimal` \| `date` \| `bool` \| `string` |
| `transform` | `trim` \| `upper` \| `lower` \| `normalizeSpaces` |
| `default` | Value used when the source is missing/empty, or for a constant column |
| `sourceFormat` / `format` | For `date`: how to parse the input / render the output |

> The output contains exactly the columns listed here, in this order. To remove a column from
> the output, delete it from `columns`.

### `validation` (optional, per column)
Add a `validation` block to a column to enforce rules on its value **after** type conversion.
A value that fails any rule makes the whole row invalid (see *Invalid records* below).

```json
{ "source": "email", "target": "Email", "transform": "lower",
  "validation": { "required": true, "pattern": "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$" } }
```

| Field | Purpose |
|---|---|
| `required` | Reject the row when the value is missing/empty |
| `pattern` | Regex the value must match (e.g. email, phone) |
| `minLength` / `maxLength` | Bounds on the string length |
| `min` / `max` | Numeric bounds (for `int` / `decimal` columns) |
| `allowedValues` | Whitelist — the value must be one of these |

> Empty optional values pass — only `required` rejects an empty field. Numeric `min`/`max`
> are for numbers; for dates, validate the rendered output with `pattern`.

### `filters` (optional)
Global rules applied before mapping. A row must pass all of them to be processed.
```json
"filters": [ { "column": "status", "op": "equals", "value": "active" } ]
```
Ops: `equals`, `notEquals`, `contains`, `nonEmpty`.

### `outputs` (optional — split into multiple files)
Each entry writes `<inputname>_<suffix>.csv` containing only the rows that match its filter.
If `outputs` is omitted, a single `<inputname>.out.csv` is written with every row.

> Splits filter on the **input** row, so the column used to split (e.g. `gender`) does not
> need to appear in the output `columns`.

## Invalid records
A row is "invalid" if it fails type conversion, fails a column's `validation` rule, or if it
matches none of the output splits.
Invalid rows are written to `storage/invalid/<inputname>_invalid.csv` with the **original
input columns plus an `InvalidReason`** column, so they can be corrected and re-run. The
invalid file is only created when there is at least one invalid row.

(This is different from a row being *skipped*, which means it was intentionally excluded by a
global `filter`.)

## Tests
```powershell
dotnet test EtlTool.slnx

# run a single test by name:
dotnet test --filter "FullyQualifiedName~Run_with_normalizeWhitespace"
```

## Notes
- Output CSVs are written as UTF-8 **without a byte-order mark**.
- A running CLI/API process locks `EtlTool.Core.dll`; stop it before rebuilding if a build
  reports the file is in use.
