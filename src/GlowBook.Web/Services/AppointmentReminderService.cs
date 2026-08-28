using GlowBook.Web.Data;
using GlowBook.Web.Models;
using GlowBook.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace GlowBook.Web.Services;

public class AppointmentReminderService
{
    private static readonly AppointmentStatus[] ActiveStatuses =
    [
        AppointmentStatus.Pending,
        AppointmentStatus.Confirmed
    ];

    private readonly ApplicationDbContext _db;
    private readonly MasterProfileService _profiles;
    private readonly ClientAccountService _clients;

    public AppointmentReminderService(
        ApplicationDbContext db,
        MasterProfileService profiles,
        ClientAccountService clients)
    {
        _db = db;
        _profiles = profiles;
        _clients = clients;
    }

    public async Task<IReadOnlyList<AppointmentReminderDto>> GetUpcomingAsync(
        ApplicationUser user,
        CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var horizon = now.AddHours(25);

        if (ClientAccountService.IsClient(user))
            return await GetForClientAsync(user, now, horizon, ct);

        return await GetForMasterAsync(user, now, horizon, ct);
    }

    private async Task<IReadOnlyList<AppointmentReminderDto>> GetForMasterAsync(
        ApplicationUser user,
        DateTime now,
        DateTime horizon,
        CancellationToken ct)
    {
        var profile = await _profiles.GetForUserAsync(user.Id, ct);
        if (profile == null)
            return [];

        return await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Client)
            .Include(a => a.Service)
            .Where(a => a.MasterProfileId == profile.Id
                && a.StartsAt > now
                && a.StartsAt <= horizon
                && ActiveStatuses.Contains(a.Status))
            .OrderBy(a => a.StartsAt)
            .Select(a => new AppointmentReminderDto
            {
                Id = a.Id,
                StartsAt = a.StartsAt,
                Title = a.Service != null ? a.Service.Name : "Запись",
                Subtitle = a.Client != null ? a.Client.Name : null,
                EditUrl = $"/Appointments/Edit/{a.Id}"
            })
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyList<AppointmentReminderDto>> GetForClientAsync(
        ApplicationUser user,
        DateTime now,
        DateTime horizon,
        CancellationToken ct)
    {
        var appointments = await _clients.GetAppointmentsAsync(user, ct);

        return appointments
            .Where(a => a.StartsAt > now
                && a.StartsAt <= horizon
                && ActiveStatuses.Contains(a.Status))
            .OrderBy(a => a.StartsAt)
            .Select(a => new AppointmentReminderDto
            {
                Id = a.Id,
                StartsAt = a.StartsAt,
                Title = a.ServiceName ?? "Запись",
                Subtitle = a.MasterName,
                EditUrl = "/my"
            })
            .ToList();
    }
}

public class AppointmentReminderDto
{
    public int Id { get; set; }
    public DateTime StartsAt { get; set; }
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public string? EditUrl { get; set; }
}
