using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using GlowBook.Web.Models;

namespace GlowBook.Web.Controllers;

[Authorize]
[Route("chat/api")]
public class ChatApiController : Controller
{
    private readonly ClientChatService _chat;
    private readonly UserManager<ApplicationUser> _users;

    public ChatApiController(ClientChatService chat, UserManager<ApplicationUser> users)
    {
        _chat = chat;
        _users = users;
    }

    [HttpPost("{clientRecordId:int}/send")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Send(int clientRecordId, string? message, IFormFile? file, CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        var hasMessage = !string.IsNullOrWhiteSpace(message);
        var hasFile = file is { Length: > 0 };
        if (!hasMessage && !hasFile)
            return BadRequest(new { error = "Укажите текст или файл" });

        Stream? stream = null;
        if (hasFile)
            stream = file!.OpenReadStream();

        var saved = await _chat.SendAsync(
            clientRecordId,
            user.Id,
            message,
            stream,
            file?.FileName,
            file?.ContentType,
            ct);

        if (saved == null)
            return BadRequest(new { error = "Не удалось отправить сообщение" });

        return Json(ClientChatService.ToDto(saved, user.Id));
    }

    [HttpGet("attachment/{messageId:int}")]
    public async Task<IActionResult> Attachment(int messageId, CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        var result = await _chat.GetAttachmentAsync(messageId, user.Id, ct);
        if (result == null)
            return NotFound();

        return File(result.Value.Data, result.Value.ContentType, result.Value.FileName);
    }
}
