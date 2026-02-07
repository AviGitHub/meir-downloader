using MeirDownloader.Core.Models;
using MeirDownloader.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace MeirDownloader.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeriesController : ControllerBase
{
    private readonly IMeirDownloaderService _service;

    public SeriesController(IMeirDownloaderService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<Series>>> GetSeries([FromQuery] string? rabbiId, CancellationToken ct)
    {
        try
        {
            var series = await _service.GetSeriesAsync(rabbiId, ct);
            return Ok(series);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
