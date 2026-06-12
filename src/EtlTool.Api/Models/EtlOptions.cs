namespace EtlTool.Api.Models;

/// <summary>Default ETL paths/policy, bound from the "Etl" section of appsettings.json.</summary>
public sealed class EtlOptions
{
    public const string SectionName = "Etl";

    public string InputDir { get; set; } = "storage/input";
    public string OutputDir { get; set; } = "storage/output";
    public string InvalidDir { get; set; } = "storage/invalid";
    public string ConfigPath { get; set; } = "config/mapping.json";
    public string OnError { get; set; } = "skip";
}
