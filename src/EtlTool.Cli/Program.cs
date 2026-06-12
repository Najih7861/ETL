using EtlTool.Core.Models;
using EtlTool.Core.Pipeline;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

// ---- Defaults (overridable via CLI flags) -------------------------------------------
var inputDir = "storage/input";
var outputDir = "storage/output";
var invalidDir = "storage/invalid";
var configPath = "config/mapping.json";
var logsDir = "logs";
var errorPolicy = ErrorPolicy.Skip;

// ---- Parse arguments ----------------------------------------------------------------
for (var i = 0; i < args.Length; i++)
{
    switch (args[i].ToLowerInvariant())
    {
        case "--input" or "-i": inputDir = RequireValue(args, ref i); break;
        case "--output" or "-o": outputDir = RequireValue(args, ref i); break;
        case "--invalid": invalidDir = RequireValue(args, ref i); break;
        case "--config" or "-c": configPath = RequireValue(args, ref i); break;
        case "--logs" or "-l": logsDir = RequireValue(args, ref i); break;
        case "--on-error":
            var policy = RequireValue(args, ref i);
            errorPolicy = policy.Equals("fail", StringComparison.OrdinalIgnoreCase) ? ErrorPolicy.Fail : ErrorPolicy.Skip;
            break;
        case "--help" or "-h" or "-?":
            PrintHelp();
            return 0;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            PrintHelp();
            return 64; // EX_USAGE
    }
}

// ---- Configure logging (console + rolling daily file) -------------------------------
Directory.CreateDirectory(logsDir);
const string template = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: template)
    .WriteTo.File(Path.Combine(logsDir, "etl-.log"), rollingInterval: RollingInterval.Day, outputTemplate: template)
    .CreateLogger();

using var loggerFactory = new SerilogLoggerFactory(Log.Logger);
var logger = loggerFactory.CreateLogger("Etl");

// ---- Run ----------------------------------------------------------------------------
var exitCode = 0;
try
{
    logger.LogInformation("ETL run starting. input={Input} output={Output} invalid={Invalid} config={Config} onError={OnError}",
        inputDir, outputDir, invalidDir, configPath, errorPolicy);

    var folderProcessor = new FolderEtlProcessor(logger);
    var result = folderProcessor.ProcessFolder(inputDir, outputDir, invalidDir, configPath, errorPolicy);

    logger.LogInformation(
        "Run summary: files={Files} failed={Failed} rowsRead={Read} rowsWritten={Written} rowsSkipped={Skipped} rowsInvalid={Invalid} elapsed={Ms}ms",
        result.FilesProcessed, result.FilesFailed, result.TotalRowsRead,
        result.TotalRowsWritten, result.TotalRowsSkipped, result.TotalRowsInvalid, result.TotalElapsedMs);

    exitCode = result.FilesFailed > 0 ? 1 : 0;
}
catch (Exception ex)
{
    logger.LogCritical(ex, "ETL run failed: {Message}", ex.Message);
    exitCode = 2;
}
finally
{
    Log.CloseAndFlush();
}

return exitCode;

// ---- Helpers ------------------------------------------------------------------------
static string RequireValue(string[] args, ref int i)
{
    if (i + 1 >= args.Length)
        throw new ArgumentException($"Missing value for option '{args[i]}'.");
    return args[++i];
}

static void PrintHelp()
{
    Console.WriteLine(
        """
        EtlTool — file-based CSV→CSV ETL (batch over an input folder).

        Usage:
          EtlTool [options]

        Options:
          -i, --input <dir>      Input folder scanned for *.csv   (default: storage/input)
          -o, --output <dir>     Output folder for results         (default: storage/output)
              --invalid <dir>    Folder for invalid records        (default: storage/invalid)
          -c, --config <file>    Mapping config JSON               (default: config/mapping.json)
          -l, --logs <dir>       Log folder                        (default: logs)
              --on-error <mode>  skip | fail                       (default: skip)
          -h, --help             Show this help

        Exit codes: 0 = success, 1 = one or more files failed, 2 = run error, 64 = bad usage.
        """);
}
