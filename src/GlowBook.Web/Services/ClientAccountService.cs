using GlowBook.Web.Data;
using GlowBook.Web.Helpers;
using GlowBook.Web.Models;
using GlowBook.Web.Models.Entities;
using GlowBook.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace GlowBook.Web.Services;

public class ClientAccountService
{
    private readonly ApplicationDbContext _db;

    public ClientAccountService(ApplicationDbContext db) => _db = db;

    public static bool IsClient(ApplicationUser user) => user.AccountType == UserAccountType.Client;

    public static bool IsMaster(ApplicationUser user) => user.AccountType == UserAccountType.Master;

    public async Task LinkClientsToUserAsync(ApplicationUser user, CancellationToken ct = default)
    {
        if (!IsClient(user))
            return;

        var email = user.Email?.Trim();
        var phone = PhoneHelper.Normalize(user.PhoneNumber);

        var candidates = await _db.Clients
            .Where(c => !c.IsArchived && (c.LinkedUserId == null || c.LinkedUserId == user.Id))
            .ToListAsync(ct);

        var changed = false;
        foreach (var client in candidates)
        {
            if (client.LinkedUserId == user.Id)
                continue;

            var emailMatch = !string.IsNullOrWhiteSpace(email)
                && !string.IsNullOrWhiteSpace(client.Email)
                && string.Equals(client.Email.Trim(), email, StringComparison.OrdinalIgnoreCase);

            var phoneMatch = !string.IsNullOrEmpty(phone)
                && PhoneHelper.Match(client.Phone, user.PhoneNumber);

            if (emailMatch || phoneMatch)
            {
                client.LinkedUserId = user.Id;
                changed = true;
            }
        }

        if (changed)
            await _db.SaveChangesAsync(ct);
    }

    public async Task<List<ClientAppointmentView>> GetAppointmentsAsync(ApplicationUser user, CancellationToken ct = default)
    {
        await LinkClientsToUserAsync(user, ct);

        var email = user.Email?.Trim();

        var ids = await _db.Clients
            .Where(c => !c.IsArchived && (
                c.LinkedUserId == user.Id
                || (email != null && c.Email != null && c.Email.ToLower() == email.ToLower())))
            .Select(c => c.Id)
            .ToListAsync(ct);

        var phone = PhoneHelper.Normalize(user.PhoneNumber);
        if (!string.IsNullOrEmpty(phone))
        {
            var phoneClients = await _db.Clients
                .Where(c => !c.IsArchived && c.LinkedUserId == null && c.Phone != null)
                .Select(c => new { c.Id, c.Phone })
                .ToListAsync(ct);

            foreach (var pc in phoneClients)
            {
                if (PhoneHelper.Match(pc.Phone, user.PhoneNumber))
                    ids.Add(pc.Id);
            }
        }

        ids = ids.Distinct().ToList();
        if (ids.Count == 0)
            return new List<ClientAppointmentView>();

        return await _db.Appointments
            .Include(a => a.Service)
            .Include(a => a.MasterProfile)
            .Where(a => ids.Contains(a.ClientId))
            .OrderByDescending(a => a.StartsAt)
            .Select(a => new ClientAppointmentView
            {
                Id = a.Id,
                ClientRecordId = a.ClientId,
                StartsAt = a.StartsAt,
                EndsAt = a.EndsAt,
                Status = a.Status,
                ServiceName = a.Service != null ? a.Service.Name : null,
                MasterName = a.MasterProfile != null ? a.MasterProfile.BusinessName : null,
                MasterCity = a.MasterProfile != null ? a.MasterProfile.City : null,
                MasterProfileId = a.MasterProfileId,
                Notes = a.Notes
            })
            .ToListAsync(ct);
    }

    public async Task<ApplicationUser?> GetLinkedAccountForClientAsync(Client client, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(client.LinkedUserId))
            return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == client.LinkedUserId, ct);

        var email = client.Email?.Trim();
        if (!string.IsNullOrWhiteSpace(email))
        {
            var byEmail = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.AccountType == UserAccountType.Client && u.Email != null && u.Email.ToLower() == email.ToLower(), ct);
            if (byEmail != null)
                return byEmail;
        }

        if (!string.IsNullOrWhiteSpace(client.Phone))
        {
            var clientUsers = await _db.Users.AsNoTracking()
                .Where(u => u.AccountType == UserAccountType.Client && u.PhoneNumber != null)
                .ToListAsync(ct);

            return clientUsers.FirstOrDefault(u => PhoneHelper.Match(client.Phone, u.PhoneNumber));
        }

        return null;
    }

    public async Task TryLinkClientRecordAsync(Client client, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(client.LinkedUserId) || client.IsArchived)
            return;

        var user = await GetLinkedAccountForClientAsync(client, ct);
        if (user == null)
            return;

        client.LinkedUserId = user.Id;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<Client>> GetLinkedClientRecordsAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var records = await CollectMatchingClientRecordsAsync(user, ct);
        if (records.Count == 0)
            return records;

        var groups = await GroupClientsByMasterAsync(records, user, ct);
        return groups.Select(g => g.Canonical).ToList();
    }

    public async Task<List<ClientMasterView>> GetMastersAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var records = await CollectMatchingClientRecordsAsync(user, ct);
        if (records.Count == 0)
            return new List<ClientMasterView>();

        var groups = await GroupClientsByMasterAsync(records, user, ct);
        var allIds = records.Select(c => c.Id).ToList();
        var upcomingByClient = await _db.Appointments
            .Where(a => allIds.Contains(a.ClientId) && a.StartsAt >= DateTime.UtcNow && a.Status != AppointmentStatus.Cancelled)
            .GroupBy(a => a.ClientId)
            .Select(g => new { ClientId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClientId, x => x.Count, ct);

        return groups.Select(g =>
        {
            var c = g.Canonical;
            var master = c.MasterProfile!;
            return new ClientMasterView
            {
                ClientRecordId = c.Id,
                MasterProfileId = master.Id,
                MasterName = string.IsNullOrWhiteSpace(master.BusinessName) ? "Мастер" : master.BusinessName,
                Specialization = master.Specialization,
                City = master.City,
                HasAvatar = master.HasAvatar,
                AvatarVersion = master.AvatarUpdatedAt?.Ticks,
                UpcomingAppointments = g.AllClientIds.Sum(id => upcomingByClient.GetValueOrDefault(id))
            };
        }).OrderBy(m => m.MasterName).ToList();
    }

    private async Task<List<Client>> CollectMatchingClientRecordsAsync(ApplicationUser user, CancellationToken ct)
    {
        await LinkClientsToUserAsync(user, ct);

        var email = user.Email?.Trim();
        var phone = PhoneHelper.Normalize(user.PhoneNumber);

        var records = await _db.Clients
            .Include(c => c.MasterProfile)
            .Where(c => !c.IsArchived && c.LinkedUserId == user.Id)
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(email))
        {
            var byEmail = await _db.Clients
                .Include(c => c.MasterProfile)
                .Where(c => !c.IsArchived && c.LinkedUserId == null && c.Email != null && c.Email.ToLower() == email.ToLower())
                .ToListAsync(ct);
            records.AddRange(byEmail);
        }

        if (!string.IsNullOrEmpty(phone))
        {
            var unlinked = await _db.Clients
                .Include(c => c.MasterProfile)
                .Where(c => !c.IsArchived && c.LinkedUserId == null && c.Phone != null)
                .ToListAsync(ct);
            records.AddRange(unlinked.Where(c => PhoneHelper.Match(c.Phone, user.PhoneNumber)));
        }

        return records
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .Where(c => c.MasterProfile != null)
            .ToList();
    }

    private async Task<List<MasterClientGroup>> GroupClientsByMasterAsync(
        List<Client> records,
        ApplicationUser user,
        CancellationToken ct)
    {
        var ids = records.Select(c => c.Id).ToList();

        var messageCounts = await _db.ClientMessages
            .Where(m => ids.Contains(m.ClientId))
            .GroupBy(m => m.ClientId)
            .Select(g => new { ClientId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClientId, x => x.Count, ct);

        var apptCounts = await _db.Appointments
            .Where(a => ids.Contains(a.ClientId))
            .GroupBy(a => a.ClientId)
            .Select(g => new { ClientId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClientId, x => x.Count, ct);

        return records
            .GroupBy(c => c.MasterProfileId)
            .Select(g => new MasterClientGroup(
                PickCanonicalClientRecord(g, user.Id, messageCounts, apptCounts),
                g.Select(c => c.Id).ToList()))
            .ToList();
    }

    private static Client PickCanonicalClientRecord(
        IEnumerable<Client> group,
        string userId,
        IReadOnlyDictionary<int, int> messageCounts,
        IReadOnlyDictionary<int, int> apptCounts)
    {
        return PickCanonicalClientRecord(group, messageCounts, apptCounts, userId);
    }

    private static Client PickCanonicalClientRecord(
        IEnumerable<Client> group,
        IReadOnlyDictionary<int, int> messageCounts,
        IReadOnlyDictionary<int, int> apptCounts,
        string? preferredLinkedUserId = null)
    {
        return group
            .OrderByDescending(c => preferredLinkedUserId != null && c.LinkedUserId == preferredLinkedUserId ? 1 : 0)
            .ThenByDescending(c => messageCounts.GetValueOrDefault(c.Id))
            .ThenByDescending(c => apptCounts.GetValueOrDefault(c.Id))
            .ThenByDescending(c => c.CreatedAt)
            .First();
    }

    public async Task<IReadOnlyList<LinkedUserClientGroup>> GetLinkedUserGroupsForMasterAsync(
        int masterProfileId,
        CancellationToken ct = default)
    {
        var records = await _db.Clients
            .AsNoTracking()
            .Where(c => c.MasterProfileId == masterProfileId && !c.IsArchived && c.LinkedUserId != null)
            .ToListAsync(ct);

        if (records.Count == 0)
            return Array.Empty<LinkedUserClientGroup>();

        var ids = records.Select(c => c.Id).ToList();
        var messageCounts = await _db.ClientMessages
            .Where(m => ids.Contains(m.ClientId))
            .GroupBy(m => m.ClientId)
            .Select(g => new { ClientId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClientId, x => x.Count, ct);

        var apptCounts = await _db.Appointments
            .Where(a => ids.Contains(a.ClientId))
            .GroupBy(a => a.ClientId)
            .Select(g => new { ClientId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClientId, x => x.Count, ct);

        return records
            .GroupBy(c => c.LinkedUserId!)
            .Select(g => new LinkedUserClientGroup(
                PickCanonicalClientRecord(g, messageCounts, apptCounts),
                g.Select(c => c.Id).ToList(),
                g.Key))
            .ToList();
    }

    public async Task<IReadOnlyList<int>> GetRelatedClientRecordIdsAsync(int clientId, CancellationToken ct = default)
    {
        var client = await _db.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clientId && !c.IsArchived, ct);
        if (client == null)
            return Array.Empty<int>();

        if (string.IsNullOrWhiteSpace(client.LinkedUserId))
            return new[] { clientId };

        return await _db.Clients
            .AsNoTracking()
            .Where(c => c.MasterProfileId == client.MasterProfileId
                && c.LinkedUserId == client.LinkedUserId
                && !c.IsArchived)
            .Select(c => c.Id)
            .ToListAsync(ct);
    }

    public async Task<int> GetCanonicalClientRecordIdAsync(int clientId, CancellationToken ct = default)
    {
        var relatedIds = await GetRelatedClientRecordIdsAsync(clientId, ct);
        if (relatedIds.Count <= 1)
            return clientId;

        var records = await _db.Clients
            .AsNoTracking()
            .Where(c => relatedIds.Contains(c.Id))
            .ToListAsync(ct);

        var messageCounts = await _db.ClientMessages
            .Where(m => relatedIds.Contains(m.ClientId))
            .GroupBy(m => m.ClientId)
            .Select(g => new { ClientId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClientId, x => x.Count, ct);

        var apptCounts = await _db.Appointments
            .Where(a => relatedIds.Contains(a.ClientId))
            .GroupBy(a => a.ClientId)
            .Select(g => new { ClientId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClientId, x => x.Count, ct);

        return PickCanonicalClientRecord(records, messageCounts, apptCounts).Id;
    }

    public async Task<IReadOnlyList<MasterClientGroup>> GetMasterClientGroupsAsync(
        ApplicationUser user,
        CancellationToken ct = default)
    {
        var records = await CollectMatchingClientRecordsAsync(user, ct);
        if (records.Count == 0)
            return Array.Empty<MasterClientGroup>();

        return await GroupClientsByMasterAsync(records, user, ct);
    }

    public async Task<LinkedClientAccountView?> GetLinkedAccountViewAsync(Client client, CancellationToken ct = default)
    {
        var user = await GetLinkedAccountForClientAsync(client, ct);
        if (user == null)
            return null;

        var avatar = await _db.ClientAvatars.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == user.Id, ct);

        return new LinkedClientAccountView
        {
            UserId = user.Id,
            DisplayName = user.DisplayName ?? user.Email ?? client.Name,
            Email = user.Email,
            Phone = user.PhoneNumber,
            HasAvatar = avatar != null,
            AvatarVersion = avatar?.UpdatedAt.Ticks,
            ClientRecordId = client.Id,
            RegisteredAt = null
        };
    }
}

public class ClientAppointmentView
{
    public int Id { get; set; }
    public int ClientRecordId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public AppointmentStatus Status { get; set; }
    public string? ServiceName { get; set; }
    public string? MasterName { get; set; }
    public string? MasterCity { get; set; }
    public int? MasterProfileId { get; set; }
    public string? Notes { get; set; }
}

public class ClientMasterView
{
    public int ClientRecordId { get; set; }
    public int MasterProfileId { get; set; }
    public string MasterName { get; set; } = "";
    public string? Specialization { get; set; }
    public string? City { get; set; }
    public bool HasAvatar { get; set; }
    public long? AvatarVersion { get; set; }
    public int UpcomingAppointments { get; set; }
}

public sealed record MasterClientGroup(Client Canonical, IReadOnlyList<int> AllClientIds);

public sealed record LinkedUserClientGroup(Client Canonical, IReadOnlyList<int> AllClientIds, string LinkedUserId);

public class LinkedClientAccountView
{
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool HasAvatar { get; set; }
    public long? AvatarVersion { get; set; }
    public int ClientRecordId { get; set; }
    public DateTime? RegisteredAt { get; set; }
}
