using EtlTool.Core.Models;

namespace EtlTool.Api.Interfaces;

/// <summary>Drives the ETL engine. All input comes from the configured input folder.</summary>
public interface IEtlService
{
    /// <summary>Runs the CSV→CSV batch over every *.csv in the input folder.</summary>
    BatchEtlResult RunEtl();

    /// <summary>Lists the *.csv file names currently in the input folder.</summary>
    IReadOnlyList<string> ListInputFiles();
}
