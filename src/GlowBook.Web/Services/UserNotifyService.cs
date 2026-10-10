using GlowBook.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace GlowBook.Web.Services;

public class UserNotifyService
{
    private readonly IHubContext<UserNotifyHub> _hub;

    public UserNotifyService(IHubContext<UserNotifyHub> hub)
    {
        _hub = hub;
    }

    public Task NotifyAsync(
        string userId,
        string title,
        string body,
        string url,
        string kind,
        int? threadId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Task.CompletedTask;

        var payload = new
        {
            title,
            body,
            url,
            kind,
            threadId
        };

        return _hub.Clients
            .Group(UserNotifyHub.UserGroup(userId))
            .SendAsync("UserNotify", payload, ct);
    }
}
