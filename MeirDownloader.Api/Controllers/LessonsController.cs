using MeirDownloader.Core.Models;
using MeirDownloader.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace MeirDownloader.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LessonsController : ControllerBase
{
    private readonly IMeirDownloaderService _service;

    public LessonsController(IMeirDownloaderService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<Lesson>>> GetLessons(
        [FromQuery] string? rabbiId,
        [FromQuery] string? seriesId,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        try
        {
            var lessons = await _service.GetLessonsAsync(rabbiId, seriesId, page, ct);
            return Ok(lessons);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
