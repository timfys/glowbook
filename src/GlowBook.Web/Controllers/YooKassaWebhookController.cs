using System.Text.Json;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GlowBook.Web.Controllers;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
[ApiController]
[Route("api/yookassa")]
public class YooKassaWebhookController : ControllerBase
{
    private readonly YooKassaService _yooKassa;
    private readonly ILogger<YooKassaWebhookController> _logger;

    public YooKassaWebhookController(YooKassaService yooKassa, ILogger<YooKassaWebhookController> logger)
    {
        _yooKassa = yooKassa;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        JsonDocument doc;
        try
        {
            doc = await JsonDocument.ParseAsync(Request.Body, cancellationToken: ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "YooKassa webhook: invalid JSON");
            return BadRequest();
        }

        using (doc)
        {
            await _yooKassa.HandleWebhookAsync(doc.RootElement.Clone(), ct);
        }

        return Ok();
    }
}
