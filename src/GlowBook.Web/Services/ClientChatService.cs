using GlowBook.Web.Data;
using GlowBook.Web.Hubs;
using GlowBook.Web.Models;
using GlowBook.Web.Models.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GlowBook.Web.Services;

public class ClientChatService
{
    private static readonly HashSet<string> AllowedAttachmentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif", "application/pdf"
    };

    private const long MaxAttachmentBytes = 5 * 1024 * 1024;

    private readonly ApplicationDbContext _db;
    private readonly ClientAccountService _accounts;
    private readonly IHubContext<ClientChatHub> _hub;
    private readonly ChatRealtimeNotifier _realtime;

    public ClientChatService(
        ApplicationDbContext db,
        ClientAccountService accounts,
        IHubContext<ClientChatHub> hub,
        ChatRealtimeNotifier realtime)
    {
        _db = db;
        _accounts = accounts;
        _hub = hub;
        _realtime = realtime;
    }

    public async Task<List<ClientMessage>> GetMessagesAsync(int clientId, CancellationToken ct = default)
    {
        var clientIds = await _accounts.GetRelatedClientRecordIdsAsync(clientId, ct);
        return await LoadMessagesAsync(clientIds, afterId: null, ct);
    }

    public async Task<List<ClientMessage>> GetMessagesAfterAsync(int clientId, int afterId, CancellationToken ct = default)
    {
        var clientIds = await _accounts.GetRelatedClientRecordIdsAsync(clientId, ct);
        return await LoadMessagesAsync(clientIds, afterId, ct);
    }

    private async Task<List<ClientMessage>> LoadMessagesAsync(
        IReadOnlyList<int> clientIds,
        int? afterId,
        CancellationToken ct)
    {
        if (clientIds.Count == 0)
            return new List<ClientMessage>();

        var query = _db.ClientMessages
            .AsNoTracking()
            .Include(m => m.SenderUser)
            .Where(m => clientIds.Contains(m.ClientId));

        if (afterId != null)
            query = query.Where(m => m.Id > afterId);

        return await query
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ClientMessage
            {
                Id = m.Id,
                ClientId = m.ClientId,
                SenderUserId = m.SenderUserId,
                SenderUser = m.SenderUser,
                Body = m.Body,
                CreatedAt = m.CreatedAt,
                AttachmentFileName = m.AttachmentFileName,
                AttachmentContentType = m.AttachmentContentType
            })
            .ToListAsync(ct);
    }

    public async Task<ClientMessage?> SendAsync(
        int clientId,
        string senderUserId,
        string? body,
        Stream? attachmentStream = null,
        string? attachmentFileName = null,
        string? attachmentContentType = null,
        CancellationToken ct = default)
    {
        var text = (body ?? "").Trim();
        if (text.Length > 2000)
            text = text[..2000];

        byte[]? attachmentData = null;
        string? fileName = null;
        string? contentType = null;

        if (attachmentStream != null)
        {
            await using var ms = new MemoryStream();
            await attachmentStream.CopyToAsync(ms, ct);
            attachmentData = ms.ToArray();

            if (attachmentData.Length == 0)
                return null;

            if (attachmentData.Length > MaxAttachmentBytes)
                return null;

            contentType = DetectAttachmentContentType(attachmentData, attachmentContentType);
            if (contentType == null)
                return null;

            fileName = SanitizeFileName(attachmentFileName);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = contentType == "application/pdf" ? "document.pdf" : "image.jpg";
        }

        if (string.IsNullOrWhiteSpace(text) && attachmentData == null)
            return null;

        clientId = await _accounts.GetCanonicalClientRecordIdAsync(clientId, ct);

        var client = await _db.Clients
            .Include(c => c.MasterProfile)
            .FirstOrDefaultAsync(c => c.Id == clientId && !c.IsArchived, ct);
        if (client?.MasterProfile == null)
            return null;

        if (!await CanAccessChatAsync(clientId, senderUserId, ct))
            return null;

        var message = new ClientMessage
        {
            ClientId = clientId,
            SenderUserId = senderUserId,
            Body = text,
            CreatedAt = DateTime.UtcNow,
            AttachmentFileName = fileName,
            AttachmentContentType = contentType,
            AttachmentData = attachmentData
        };

        _db.ClientMessages.Add(message);
        await _db.SaveChangesAsync(ct);

        message = await _db.ClientMessages
            .Include(m => m.SenderUser)
            .FirstAsync(m => m.Id == message.Id, ct);

        var dto = ToDto(message, senderUserId);
        var threadId = await _accounts.GetCanonicalClientRecordIdAsync(clientId, ct);
        await _hub.Clients
            .Group(ClientChatHub.ThreadGroup(threadId))
            .SendAsync("ReceiveMessage", dto, ct);
        _realtime.Publish(threadId, dto);

        return message;
    }

    public async Task<(byte[] Data, string ContentType, string FileName)?> GetAttachmentAsync(
        int messageId,
        string userId,
        CancellationToken ct = default)
    {
        var message = await _db.ClientMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (message?.AttachmentData == null || message.AttachmentData.Length == 0)
            return null;

        if (!await CanAccessChatAsync(message.ClientId, userId, ct))
            return null;

        return (
            message.AttachmentData,
            message.AttachmentContentType ?? "application/octet-stream",
            message.AttachmentFileName ?? "attachment");
    }

    public async Task<bool> CanAccessChatAsync(int clientId, string userId, CancellationToken ct = default)
    {
        var relatedIds = await _accounts.GetRelatedClientRecordIdsAsync(clientId, ct);
        if (relatedIds.Count == 0)
            return false;

        var clients = await _db.Clients
            .Include(c => c.MasterProfile)
            .AsNoTracking()
            .Where(c => relatedIds.Contains(c.Id) && !c.IsArchived)
            .ToListAsync(ct);

        if (clients.Count == 0 || clients.All(c => c.MasterProfile == null))
            return false;

        if (clients.Any(c => c.MasterProfile!.UserId == userId))
            return true;

        if (clients.Any(c => c.LinkedUserId == userId))
            return true;

        foreach (var client in clients)
        {
            var linked = await _accounts.GetLinkedAccountForClientAsync(client, ct);
            if (linked?.Id == userId)
                return true;
        }

        return false;
    }

    public async Task<List<ChatConversationView>> GetConversationsForMasterAsync(
        int masterProfileId,
        CancellationToken ct = default)
    {
        var groups = await _accounts.GetLinkedUserGroupsForMasterAsync(masterProfileId, ct);
        if (groups.Count == 0)
            return new List<ChatConversationView>();

        var allIds = groups.SelectMany(g => g.AllClientIds).Distinct().ToList();
        var lastMessages = await LoadLastMessagesAsync(allIds, ct);
        var userIds = groups.Select(g => g.LinkedUserId).Distinct().ToList();

        var users = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        var avatars = await _db.ClientAvatars.AsNoTracking()
            .Where(a => userIds.Contains(a.UserId))
            .ToDictionaryAsync(a => a.UserId, a => a.UpdatedAt.Ticks, ct);

        return groups
            .Select(g =>
            {
                users.TryGetValue(g.LinkedUserId, out var linkedUser);
                var last = g.AllClientIds
                    .Select(id => lastMessages.GetValueOrDefault(id))
                    .Where(m => m != null)
                    .OrderByDescending(m => m!.CreatedAt)
                    .FirstOrDefault();
                avatars.TryGetValue(g.LinkedUserId, out var avatarVersion);

                return new ChatConversationView
                {
                    ClientRecordId = g.Canonical.Id,
                    Title = linkedUser?.DisplayName ?? linkedUser?.Email ?? g.Canonical.Name,
                    Preview = BuildPreview(last),
                    LastMessageAt = last?.CreatedAt,
                    HasAvatar = avatars.ContainsKey(g.LinkedUserId),
                    AvatarVersion = avatarVersion
                };
            })
            .OrderByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
            .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<ChatConversationView>> GetConversationsForClientAsync(
        ApplicationUser user,
        CancellationToken ct = default)
    {
        var groups = await _accounts.GetMasterClientGroupsAsync(user, ct);
        if (groups.Count == 0)
            return new List<ChatConversationView>();

        var allIds = groups.SelectMany(g => g.AllClientIds).Distinct().ToList();
        var lastMessages = await LoadLastMessagesAsync(allIds, ct);

        return groups
            .Select(g =>
            {
                var master = g.Canonical.MasterProfile!;
                var last = g.AllClientIds
                    .Select(id => lastMessages.GetValueOrDefault(id))
                    .Where(m => m != null)
                    .OrderByDescending(m => m!.CreatedAt)
                    .FirstOrDefault();

                return new ChatConversationView
                {
                    ClientRecordId = g.Canonical.Id,
                    MasterProfileId = master.Id,
                    Title = string.IsNullOrWhiteSpace(master.BusinessName) ? "Мастер" : master.BusinessName,
                    Preview = BuildPreview(last),
                    LastMessageAt = last?.CreatedAt,
                    HasAvatar = master.HasAvatar,
                    AvatarVersion = master.AvatarUpdatedAt?.Ticks
                };
            })
            .OrderByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
            .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<Dictionary<int, ClientMessage>> LoadLastMessagesAsync(
        IReadOnlyList<int> clientIds,
        CancellationToken ct)
    {
        if (clientIds.Count == 0)
            return new Dictionary<int, ClientMessage>();

        var rows = await _db.ClientMessages
            .AsNoTracking()
            .Where(m => clientIds.Contains(m.ClientId))
            .GroupBy(m => m.ClientId)
            .Select(g => g.OrderByDescending(m => m.Id).First())
            .ToListAsync(ct);

        return rows.ToDictionary(m => m.ClientId);
    }

    private static string BuildPreview(ClientMessage? message)
    {
        if (message == null)
            return "Нет сообщений";

        if (!string.IsNullOrWhiteSpace(message.Body))
        {
            var text = message.Body.Trim();
            return text.Length <= 80 ? text : text[..77] + "…";
        }

        if (HasAttachment(message))
            return IsImageAttachment(message) ? "Фото" : (message.AttachmentFileName ?? "Файл");

        return "Нет сообщений";
    }

    public Task<int> GetThreadClientIdAsync(int clientId, CancellationToken ct = default) =>
        _accounts.GetCanonicalClientRecordIdAsync(clientId, ct);

    public static ClientMessageDto ToDto(ClientMessage m, string currentUserId) => new()
    {
        Id = m.Id,
        Body = m.Body,
        CreatedAt = m.CreatedAt.ToString("o"),
        IsMine = m.SenderUserId == currentUserId,
        SenderUserId = m.SenderUserId,
        SenderName = m.SenderUser?.DisplayName ?? m.SenderUser?.Email ?? "Пользователь",
        HasAttachment = HasAttachment(m),
        AttachmentFileName = m.AttachmentFileName,
        AttachmentContentType = m.AttachmentContentType,
        IsImageAttachment = IsImageAttachment(m),
        AttachmentUrl = HasAttachment(m) ? $"/chat/api/attachment/{m.Id}" : null
    };

    public static bool HasAttachment(ClientMessage m) =>
        !string.IsNullOrWhiteSpace(m.AttachmentFileName)
        && !string.IsNullOrWhiteSpace(m.AttachmentContentType);

    public static bool IsImageAttachment(ClientMessage m) =>
        HasAttachment(m)
        && (m.AttachmentContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false);

    private static string? DetectAttachmentContentType(byte[] data, string? reported)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "image/jpeg";
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return "image/png";
        if (data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
            return "image/gif";
        if (data.Length >= 12
            && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46
            && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
            return "image/webp";
        if (data.Length >= 4 && data[0] == 0x25 && data[1] == 0x50 && data[2] == 0x44 && data[3] == 0x46)
            return "application/pdf";

        if (!string.IsNullOrWhiteSpace(reported))
        {
            var normalized = reported.Equals("image/jpg", StringComparison.OrdinalIgnoreCase)
                ? "image/jpeg"
                : reported;
            if (AllowedAttachmentTypes.Contains(normalized))
                return normalized;
        }

        return null;
    }

    private static string SanitizeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        var fileName = Path.GetFileName(name.Trim());
        if (fileName.Length > 255)
            fileName = fileName[..255];
        return fileName;
    }
}

public class ClientMessageDto
{
    public int Id { get; set; }
    public string Body { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public bool IsMine { get; set; }
    public string SenderUserId { get; set; } = "";
    public string SenderName { get; set; } = "";
    public bool HasAttachment { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentContentType { get; set; }
    public bool IsImageAttachment { get; set; }
    public string? AttachmentUrl { get; set; }
}

public class ChatConversationView
{
    public int ClientRecordId { get; set; }
    public int? MasterProfileId { get; set; }
    public string Title { get; set; } = "";
    public string Preview { get; set; } = "";
    public DateTime? LastMessageAt { get; set; }
    public bool HasAvatar { get; set; }
    public long? AvatarVersion { get; set; }
}
