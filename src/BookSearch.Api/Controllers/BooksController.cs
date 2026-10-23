using BookSearch.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookSearch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private readonly BookSearchService _service;
    private readonly BookIndexService _indexService;

    public BooksController(BookSearchService service, BookIndexService indexService)
    {
        _service = service;
        _indexService = indexService;
    }

    [HttpGet("ping")]
    public async Task<IActionResult> Ping(CancellationToken ct)
    {
        var result = await _service.PingAsync(ct);
        return result.Connected ? Ok(result) : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
    }

    // Aşama 2: books-v1 index'ini mapping + Türkçe analyzer + alias ile oluşturur.
    [HttpPost("index")]
    public async Task<IActionResult> CreateIndex(CancellationToken ct)
    {
        var result = await _indexService.CreateIndexAsync(ct);
        return result.Success ? Ok(result) : Conflict(result);
    }

    // Aşama 2: data/books.json dosyasını bulk index eder.
    [HttpPost("seed")]
    public async Task<IActionResult> Seed(CancellationToken ct)
    {
        var result = await _indexService.SeedAsync(ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // Aşama 2: analyzer'ın metni hangi token'lara böldüğünü gösterir.
    [HttpGet("analyze")]
    public async Task<IActionResult> Analyze([FromQuery] string text, [FromQuery] string analyzer = "turkish_custom", CancellationToken ct = default)
    {
        var tokens = await _indexService.AnalyzeAsync(analyzer, text, ct);
        return Ok(new { analyzer, text, tokens });
    }
}
