using EtlTool.Api.Interfaces;
using EtlTool.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace EtlTool.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class EtlController : ControllerBase
{
    private readonly IEtlService _etlService;

    public EtlController(IEtlService etlService) => _etlService = etlService;

    /// <summary>Runs ETL over every *.csv in the input folder. No request body required.</summary>
    [HttpPost("run")]
    public ActionResult<BatchEtlResult> RunEtl() => Ok(_etlService.RunEtl());

    /// <summary>Lists the *.csv files currently in the input folder.</summary>
    [HttpGet("files")]
    public ActionResult<IReadOnlyList<string>> ListInputFiles() => Ok(_etlService.ListInputFiles());

    /// <summary>Liveness probe.</summary>
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok" });
}
