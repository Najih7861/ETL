# Execution Plan — File-Based CSV→CSV ETL Tool (.NET Backend)

## 1. Context & Goal

Build a **backend-only file-based ETL application** that:
- Scans the **input folder** and picks up **all CSV files** found there.
- Applies **column mapping** and **value conversion** driven by a **config file**.
- Writes the result back out as **CSV** to the **output folder** (local storage).
- Produces **detailed logs** for every run (console + rolling log file).

**Stack:** .NET 8 console application + a reusable ETL engine library.
Designed to **stream** rows so it works for small files now and scales to large files later.

`D:\ETL` is a greenfield project (empty). Everything below is built from scratch.
**No frontend / UI is part of this plan — backend only.**

---

## 2. High-Level Architecture

```
                       ┌──────────────────────────────────────┐
  storage/input/  ───► │  EtlTool.Cli (console host)           │
  *.csv (ALL files)    │   - scan input folder for *.csv       │
                       │   - load mapping config               │
                       │   - run pipeline per file             │
                       │   - log everything                    │
                       └───────────────────┬──────────────────┘
                                           │ calls
                              ┌────────────▼─────────────┐
                              │  EtlTool.Core (library)   │
                              │  Extract → Transform → Load│
                              │  streamed IEnumerable<Row> │
                              └────────────┬─────────────┘
                                           │ reads / writes
                  ┌────────────────────────▼────────────────────────┐
                  │  Local storage                                   │
                  │  storage/input/*.csv   ──►   storage/output/*.csv │
                  │  logs/etl-YYYYMMDD.log                            │
                  └──────────────────────────────────────────────────┘
```

The ETL logic lives in a **class library (`EtlTool.Core`)**, hosted by a **console app
(`EtlTool.Cli`)**. The pipeline processes rows as a **lazy stream**, keeping memory flat.

---

## 3. Core Logic — Batch the Whole Input Folder

On each run the console host:

1. **Discovers files**: enumerate every `*.csv` in `storage/input/`
   (`Directory.EnumerateFiles(inputDir, "*.csv")`).
2. **Loads the mapping config** once (`config/mapping.json`).
3. **For each input file** (logged individually):
   - `CsvExtractor` opens it and yields rows lazily.
   - `RowTransformer` applies filters → column mapping → type conversion → defaults.
   - `CsvLoader` streams the transformed rows to `storage/output/<sourceName>.out.csv`.
   - Collects a per-file `RunResult` (rows read / written / skipped, errors, elapsed).
4. **Writes a run summary** (totals across all files) to the log.
5. **Error policy** (`--on-error skip|fail`): a bad row is logged and skipped, or the
   run stops — configurable. A failing *file* never aborts the others unless `fail` is set.

Empty input folder ⇒ logs a warning and exits cleanly (exit code 0). Any file-level failure
under `skip` ⇒ logged and run continues; final exit code reflects whether any file failed.

---

## 4. Project / Folder Structure

```
D:\ETL\
  EtlTool.sln
  src/
    EtlTool.Core/                 # ETL engine (no host dependency)
      EtlTool.Core.csproj
      Configuration/
        MappingConfig.cs          # POCOs: source, target, columns, filters
        ConfigLoader.cs           # load + validate mapping (JSON)
      Pipeline/
        CsvExtractor.cs           # CsvHelper -> lazy IEnumerable<Row>
        RowTransformer.cs         # filter -> map -> convert -> default
        CsvLoader.cs              # stream rows out as CSV
        EtlRunner.cs              # run ONE file E->T->L, return RunResult
        BatchRunner.cs            # scan input folder, run EtlRunner per file
      Transforms/
        ITransform.cs
        BuiltInTransforms.cs      # trim, upper, lower, type conversions
        TransformFactory.cs
      Models/
        Row.cs                    # Dictionary<string, object?> wrapper
        RunResult.cs              # per-file + aggregate counts, errors, elapsed
    EtlTool.Cli/                  # console host
      EtlTool.Cli.csproj
      Program.cs                  # parse args, configure logging, run BatchRunner
      appsettings.json            # paths (input/output/logs), default error policy
  config/
    mapping.sample.json           # documented example mapping
  storage/
    input/                        # source CSVs (ALL *.csv processed)
    output/                       # generated CSVs
  logs/                           # rolling run logs
  tests/
    EtlTool.Tests/                # xUnit unit + E2E tests
      EtlTool.Tests.csproj
      fixtures/                   # tiny CSV + config fixtures
```

---

## 5. Libraries to Install (.NET 8)

| Project | Package | Purpose |
|---|---|---|
| Core | `CsvHelper` | Streaming CSV read/write (quoting, delimiters, encodings, lazy records). |
| Core | `System.Text.Json` (built-in) | Parse JSON mapping config (zero extra dependency). |
| Core | `Microsoft.Extensions.Logging.Abstractions` | Logging abstraction inside the engine. |
| Cli | `Serilog`, `Serilog.Sinks.Console`, `Serilog.Sinks.File` | Structured logs to console + rolling daily file. |
| Cli | `Serilog.Extensions.Logging` | Bridge Serilog to `Microsoft.Extensions.Logging`. |
| Cli | `System.CommandLine` | CLI args (`--input`, `--output`, `--config`, `--on-error`). |
| Tests | `xunit`, `xunit.runner.visualstudio` | Test framework. |
| Tests | `FluentAssertions` | Readable assertions. |

**Install commands (run from `D:\ETL`):**
```powershell
# Solution + projects
dotnet new sln -n EtlTool
dotnet new classlib -n EtlTool.Core  -o src/EtlTool.Core
dotnet new console  -n EtlTool.Cli   -o src/EtlTool.Cli
dotnet new xunit    -n EtlTool.Tests -o tests/EtlTool.Tests

dotnet sln add src/EtlTool.Core src/EtlTool.Cli tests/EtlTool.Tests
dotnet add src/EtlTool.Cli    reference src/EtlTool.Core
dotnet add tests/EtlTool.Tests reference src/EtlTool.Core

# Core packages
dotnet add src/EtlTool.Core package CsvHelper
dotnet add src/EtlTool.Core package Microsoft.Extensions.Logging.Abstractions

# Cli packages (logging + CLI args)
dotnet add src/EtlTool.Cli package Serilog
dotnet add src/EtlTool.Cli package Serilog.Sinks.Console
dotnet add src/EtlTool.Cli package Serilog.Sinks.File
dotnet add src/EtlTool.Cli package Serilog.Extensions.Logging
dotnet add src/EtlTool.Cli package System.CommandLine --prerelease

# Test packages
dotnet add tests/EtlTool.Tests package FluentAssertions
```

---

## 6. Logging

Serilog configured in `Program.cs` writing to **both** sinks:
- **Console** — live progress (file started/finished, row counts, warnings, errors).
- **Rolling file** — `logs/etl-YYYYMMDD.log`, daily rollover.

What gets logged:
- Run start: input/output dirs, config path, error policy, count of files discovered.
- Per file: name, rows read, rows written, rows skipped, elapsed ms.
- Per-row failures: file name, row number, column, the parse/convert error message.
- Run end: aggregate totals (files processed/failed, total rows), total elapsed, exit code.

---

## 7. Mapping Config Format (JSON, config-driven)

`config/mapping.sample.json` — applied to every input file in the batch.

```json
{
  "source":  { "delimiter": ",", "hasHeader": true, "encoding": "utf-8" },
  "target":  { "delimiter": ",", "encoding": "utf-8", "writeHeader": true },
  "filters": [
    { "column": "status", "op": "equals", "value": "active" }
  ],
  "columns": [
    { "source": "cust_id",   "target": "CustomerId", "type": "int" },
    { "source": "full_name", "target": "Name",        "transform": "trim" },
    { "source": "dob",       "target": "BirthDate",   "type": "date",
      "sourceFormat": "MM/dd/yyyy", "format": "yyyy-MM-dd" },
    { "target": "Country",   "default": "US" }
  ]
}
```

**Capabilities (extensible):**
- Rename / select / reorder columns (output order = `columns` order).
- Type conversion: `int`, `decimal`, `date` (with `sourceFormat`/`format`), `bool`, `string`.
- Transforms: `trim`, `upper`, `lower` (add more via `ITransform` + `TransformFactory`).
- Defaults / constant columns (no `source`).
- Row filters: `equals | notEquals | contains | nonEmpty`.

---

## 8. CLI Usage

```powershell
# Uses defaults from appsettings.json (storage/input, storage/output, config/mapping.json)
dotnet run --project src/EtlTool.Cli

# Override any path / policy
dotnet run --project src/EtlTool.Cli -- `
  --input storage/input --output storage/output `
  --config config/mapping.json --on-error skip
```

---

## 9. Build Order (Step-by-Step)

1. **Scaffold** solution + projects (commands in §5). Create `storage/input`,
   `storage/output`, `logs/`, `config/`.
2. **Core engine:**
   - `Row`, `RunResult` models.
   - `MappingConfig` POCOs + `ConfigLoader` (System.Text.Json deserialize + validation).
   - `CsvExtractor` (lazy `IEnumerable<Row>` via CsvHelper).
   - `ITransform`, `BuiltInTransforms`, `TransformFactory`.
   - `RowTransformer` (filters → map → convert → defaults).
   - `CsvLoader` (stream rows out).
   - `EtlRunner` (one file E→T→L) + `BatchRunner` (folder scan → loop over files).
3. **Console host:** `Program.cs` — Serilog setup, `System.CommandLine` args,
   `appsettings.json`, invoke `BatchRunner`, set exit code.
4. **Sample data:** add `config/mapping.sample.json` and a couple of tiny CSVs in
   `storage/input/`.
5. **Tests:** xUnit unit tests per transform/type conversion + an end-to-end `BatchRunner`
   test processing a fixtures input folder and comparing outputs to expected CSVs.

---

## 10. Verification

- **Unit/E2E tests:** `dotnet test` — every transform + a full batch run over a fixtures
  input folder, comparing each generated output CSV to an expected fixture.
- **Manual run:** drop 2–3 CSVs into `storage/input/`, run the CLI, then confirm:
  - matching files appear in `storage/output/`,
  - columns are renamed/converted per the mapping,
  - `logs/etl-YYYYMMDD.log` contains per-file and aggregate summaries.
- **Scale check (later):** point at a multi-hundred-MB CSV; confirm memory stays flat
  (streaming), proving the design scales without a rewrite.

---

## 11. Decisions & Notes
- **Backend only** — no UI; operated via CLI and scheduled (e.g. Windows Task Scheduler).
- **Whole-folder batch** — every `*.csv` in the input folder is processed in one run.
- **Config format = JSON** (zero extra .NET dependency). Switch to YAML later via
  `YamlDotNet` if commented hand-editing is desired — schema is identical.
- **ETL logic isolated in `EtlTool.Core`** so the engine can be reused (e.g. by a future
  API) with no rewrite.
- **Streaming everywhere** (`IEnumerable<Row>`) is the key design choice for future scale.
```
