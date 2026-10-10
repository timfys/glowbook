using GlowBook.Web.Data;
using GlowBook.Web.Models.Blog;
using Microsoft.EntityFrameworkCore;

namespace GlowBook.Web.Services;

public class MasterSearchService
{
    private readonly ApplicationDbContext _db;

    public MasterSearchService(ApplicationDbContext db) => _db = db;

    public async Task<List<MasterSearchResult>> SearchAsync(string? query, int take = 30, CancellationToken ct = default)
    {
        var q = (query ?? string.Empty).Trim().TrimStart('@');
        if (q.Length < 1)
            return [];

        var qLower = q.ToLowerInvariant();

        var profiles = await _db.MasterProfiles
            .AsNoTracking()
            .Include(p => p.Subscription)
            .Where(p =>
                p.BookingSlug.ToLower().Contains(qLower)
                || p.BusinessName.ToLower().Contains(qLower)
                || (p.Specialization != null && p.Specialization.ToLower().Contains(qLower))
                || (p.City != null && p.City.ToLower().Contains(qLower)))
            .OrderByDescending(p => p.BookingSlug.ToLower() == qLower)
            .ThenByDescending(p => p.BookingSlug.ToLower().StartsWith(qLower))
            .ThenBy(p => p.BusinessName)
            .Take(take)
            .ToListAsync(ct);

        var ids = profiles.Select(p => p.Id).ToList();
        var articleCounts = await _db.MasterArticles
            .AsNoTracking()
            .Where(a => ids.Contains(a.MasterProfileId) && a.IsPublished)
            .GroupBy(a => a.MasterProfileId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return profiles.Select(p => new MasterSearchResult
        {
            Username = p.BookingSlug,
            BusinessName = p.BusinessName,
            Specialization = p.Specialization,
            City = p.City,
            HasAvatar = p.HasAvatar,
            ProfileId = p.Id,
            AvatarVersion = p.AvatarUpdatedAt?.Ticks,
            PublishedArticles = articleCounts.GetValueOrDefault(p.Id),
            IsPremiumMaster = p.Subscription?.IsPremiumActive == true
        }).ToList();
    }
}
