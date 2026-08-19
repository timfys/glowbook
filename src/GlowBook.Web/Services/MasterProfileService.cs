using GlowBook.Web.Data;
using GlowBook.Web.Models;
using GlowBook.Web.Models.Entities;
using GlowBook.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace GlowBook.Web.Services;

public class MasterProfileService
{
    private readonly ApplicationDbContext _db;

    public MasterProfileService(ApplicationDbContext db) => _db = db;

    public async Task<MasterProfile?> GetForUserAsync(string userId, CancellationToken ct = default) =>
        await _db.MasterProfiles
            .Include(x => x.Subscription)
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

    public async Task<MasterProfile> EnsureForUserAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var profile = await GetForUserAsync(user.Id, ct);
        if (profile != null)
            return profile;

        var slugBase = Slugify(user.DisplayName ?? user.Email ?? user.Id);
        var slug = await EnsureUniqueSlugAsync(slugBase, ct);

        profile = new MasterProfile
        {
            UserId = user.Id,
            BusinessName = user.DisplayName ?? "Мой кабинет",
            BookingSlug = slug
        };

        _db.MasterProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);

        _db.Subscriptions.Add(new Subscription
        {
            MasterProfileId = profile.Id,
            Plan = SubscriptionPlan.Free,
            PriceRub = 0,
            IsActive = true
        });

        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            _db.WorkingHours.Add(new WorkingHour
            {
                MasterProfileId = profile.Id,
                DayOfWeek = day,
                StartTime = new TimeOnly(10, 0),
                EndTime = new TimeOnly(19, 0),
                IsWorkingDay = day is not DayOfWeek.Sunday
            });
        }

        await _db.SaveChangesAsync(ct);
        return profile;
    }

    private async Task<string> EnsureUniqueSlugAsync(string baseSlug, CancellationToken ct)
    {
        var slug = baseSlug;
        var i = 1;
        while (await _db.MasterProfiles.AnyAsync(x => x.BookingSlug == slug, ct))
            slug = $"{baseSlug}-{++i}";
        return slug;
    }

    public static string Slugify(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')
            .ToArray();
        var slug = new string(chars);
        return string.IsNullOrWhiteSpace(slug) ? "master" : slug[..Math.Min(slug.Length, 50)];
    }
}
