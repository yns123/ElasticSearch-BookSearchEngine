using BookSearch.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookSearch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private readonly BookSearchService _service;

    public BooksController(BookSearchService service)
    {
        _service = service;
    }

    [HttpGet("ping")]
    public async Task<IActionResult> Ping(CancellationToken ct)
    {
        var result = await _service.PingAsync(ct);
        return result.Connected ? Ok(result) : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
    }
}
