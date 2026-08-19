using GlowBook.Web.Data;
using GlowBook.Web.Models.Booking;
using GlowBook.Web.Models.Entities;
using GlowBook.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace GlowBook.Web.Services;

public class BookingService
{
    private readonly ApplicationDbContext _db;

    public BookingService(ApplicationDbContext db) => _db = db;

    public async Task<MasterProfile?> GetBookableProfileAsync(string slug, CancellationToken ct = default) =>
        await _db.MasterProfiles
            .Include(p => p.Subscription)
            .Include(p => p.Services.Where(s => s.IsActive))
            .FirstOrDefaultAsync(p => p.BookingSlug == slug, ct);

    public bool IsOnlineBookingEnabled(MasterProfile profile) =>
        profile.Subscription?.IsPremiumActive == true;

    public async Task<List<string>> GetAvailableTimesAsync(
        int masterProfileId,
        int serviceId,
        DateTime date,
        CancellationToken ct = default)
    {
        var service = await _db.Services.FirstOrDefaultAsync(
            s => s.Id == serviceId && s.MasterProfileId == masterProfileId && s.IsActive, ct);
        if (service == null)
            return new List<string>();

        var day = date.Date;
        var working = await _db.WorkingHours.FirstOrDefaultAsync(
            w => w.MasterProfileId == masterProfileId && w.DayOfWeek == day.DayOfWeek, ct);
        if (working == null || !working.IsWorkingDay)
            return new List<string>();

        var dayStart = day.Add(working.StartTime.ToTimeSpan());
        var dayEnd = day.Add(working.EndTime.ToTimeSpan());
        if (dayEnd <= dayStart)
            return new List<string>();

        var existing = await _db.Appointments
            .Where(a => a.MasterProfileId == masterProfileId
                && a.StartsAt >= dayStart
                && a.StartsAt < day.AddDays(1)
                && a.Status != AppointmentStatus.Cancelled)
            .Select(a => new { a.StartsAt, a.EndsAt })
            .ToListAsync(ct);

        var slots = new List<string>();
        var step = TimeSpan.FromMinutes(30);
        var duration = TimeSpan.FromMinutes(service.DurationMinutes);

        for (var slotStart = dayStart; slotStart + duration <= dayEnd; slotStart += step)
        {
            var slotEnd = slotStart + duration;
            if (slotStart <= DateTime.Now)
                continue;

            var overlaps = existing.Any(a => slotStart < a.EndsAt && slotEnd > a.StartsAt);
            if (!overlaps)
                slots.Add(slotStart.ToString("HH:mm"));
        }

        return slots;
    }

    public async Task<(bool Ok, string? Error)> CreatePublicBookingAsync(
        MasterProfile profile,
        PublicBookingForm form,
        CancellationToken ct = default)
    {
        if (!IsOnlineBookingEnabled(profile))
            return (false, "Online booking requires Premium");

        if (!TimeOnly.TryParse(form.Time, out var timeOnly))
            return (false, "Invalid time");

        var startsAt = form.Date.Date.Add(timeOnly.ToTimeSpan());
        var service = await _db.Services.FirstOrDefaultAsync(
            s => s.Id == form.ServiceId && s.MasterProfileId == profile.Id && s.IsActive, ct);
        if (service == null)
            return (false, "Service not found");

        var endsAt = startsAt.AddMinutes(service.DurationMinutes);
        var times = await GetAvailableTimesAsync(profile.Id, service.Id, form.Date, ct);
        if (!times.Contains(startsAt.ToString("HH:mm")))
            return (false, "This time slot is no longer available");

        var phone = NormalizePhone(form.ClientPhone);
        var client = await _db.Clients.FirstOrDefaultAsync(
            c => c.MasterProfileId == profile.Id && c.Phone == phone, ct);

        if (client == null)
        {
            client = new Client
            {
                MasterProfileId = profile.Id,
                Name = form.ClientName.Trim(),
                Phone = phone,
                Notes = form.Notes
            };
            _db.Clients.Add(client);
            await _db.SaveChangesAsync(ct);
        }
        else if (!string.Equals(client.Name, form.ClientName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            client.Name = form.ClientName.Trim();
        }

        _db.Appointments.Add(new Appointment
        {
            MasterProfileId = profile.Id,
            ClientId = client.Id,
            ServiceId = service.Id,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = AppointmentStatus.Pending,
            Notes = form.Notes,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    private static string NormalizePhone(string phone) =>
        new string(phone.Where(char.IsDigit).ToArray());
}
