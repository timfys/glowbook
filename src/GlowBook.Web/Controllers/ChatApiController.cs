using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using GlowBook.Web.Models;
using System.Text.Json;

namespace GlowBook.Web.Controllers;

[Authorize]
[Route("chat/api")]
public class ChatApiController : Controller
{
    private readonly ClientChatService _chat;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ChatRealtimeNotifier _realtime;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ChatApiController(
        ClientChatService chat,
        UserManager<ApplicationUser> users,
        ChatRealtimeNotifier realtime)
    {
        _chat = chat;
        _users = users;
        _realtime = realtime;
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

    [HttpGet("{clientRecordId:int}/messages")]
    public async Task<IActionResult> Messages(int clientRecordId, int after = 0, CancellationToken ct = default)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        if (!await _chat.CanAccessChatAsync(clientRecordId, user.Id, ct))
            return Forbid();

        var messages = await _chat.GetMessagesAfterAsync(clientRecordId, after, ct);
        return Json(messages.Select(m => ClientChatService.ToDto(m, user.Id)));
    }

    [HttpGet("{clientRecordId:int}/stream")]
    public async Task Stream(int clientRecordId, int after = 0, CancellationToken ct = default)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!await _chat.CanAccessChatAsync(clientRecordId, user.Id, ct))
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var threadId = await _chat.GetThreadClientIdAsync(clientRecordId, ct);
        Response.Headers.CacheControl = "no-cache, no-store";
        Response.Headers.Connection = "keep-alive";
        Response.ContentType = "text/event-stream";

        var lastId = after;
        var (reader, unsubscribe) = _realtime.Subscribe(threadId);
        try
        {
            lastId = await WriteCatchUpAsync(clientRecordId, user.Id, lastId, ct);

            while (!ct.IsCancellationRequested)
            {
                using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                waitCts.CancelAfter(TimeSpan.FromSeconds(12));

                try
                {
                    while (await reader.WaitToReadAsync(waitCts.Token))
                    {
                        while (reader.TryRead(out var dto))
                        {
                            if (dto.Id <= lastId)
                                continue;

                            await WriteEventAsync(dto, ct);
                            lastId = dto.Id;
                        }
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // Periodic DB catch-up for multi-instance or missed pushes.
                }

                lastId = await WriteCatchUpAsync(clientRecordId, user.Id, lastId, ct);
            }
        }
        finally
        {
            unsubscribe();
        }
    }

    private async Task<int> WriteCatchUpAsync(int clientRecordId, string userId, int lastId, CancellationToken ct)
    {
        var messages = await _chat.GetMessagesAfterAsync(clientRecordId, lastId, ct);
        foreach (var message in messages)
        {
            var dto = ClientChatService.ToDto(message, userId);
            await WriteEventAsync(dto, ct);
            lastId = message.Id;
        }

        return lastId;
    }

    private async Task WriteEventAsync(ClientMessageDto dto, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        await Response.WriteAsync($"event: message\ndata: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
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
