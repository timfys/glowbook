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
                StartsAt = a.StartsAt,
                EndsAt = a.EndsAt,
                Status = a.Status,
                ServiceName = a.Service != null ? a.Service.Name : null,
                MasterName = a.MasterProfile != null ? a.MasterProfile.BusinessName : null,
                MasterCity = a.MasterProfile != null ? a.MasterProfile.City : null,
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
}

public class ClientAppointmentView
{
    public int Id { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public AppointmentStatus Status { get; set; }
    public string? ServiceName { get; set; }
    public string? MasterName { get; set; }
    public string? MasterCity { get; set; }
    public string? Notes { get; set; }
}
