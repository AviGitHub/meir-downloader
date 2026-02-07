using MeirDownloader.Core.Models;
using MeirDownloader.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace MeirDownloader.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RabbisController : ControllerBase
{
    private readonly IMeirDownloaderService _service;

    public RabbisController(IMeirDownloaderService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<Rabbi>>> GetRabbis(CancellationToken ct)
    {
        try
        {
            var rabbis = await _service.GetRabbisAsync(ct);
            return Ok(rabbis);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
