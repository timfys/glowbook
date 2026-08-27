using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GlowBook.Web.Hubs;

[Authorize]
public class ClientChatHub : Hub
{
    private readonly ClientChatService _chat;

    public ClientChatHub(ClientChatService chat)
    {
        _chat = chat;
    }

    public async Task JoinThread(int clientId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !await _chat.CanAccessChatAsync(clientId, userId))
            throw new HubException("Доступ запрещён");

        var threadId = await _chat.GetThreadClientIdAsync(clientId);
        await Groups.AddToGroupAsync(Context.ConnectionId, ThreadGroup(threadId));
    }

    public static string ThreadGroup(int clientId) => $"chat-{clientId}";
}
