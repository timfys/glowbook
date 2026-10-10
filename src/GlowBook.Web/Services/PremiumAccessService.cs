using GlowBook.Web.Data;
using GlowBook.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GlowBook.Web.Services;

public class PremiumAccessService
{
    private readonly ApplicationDbContext _db;

    public PremiumAccessService(ApplicationDbContext db) => _db = db;

    public async Task<bool> HasPremiumAsync(ApplicationUser? user, CancellationToken ct = default)
    {
        if (user == null) return false;
        if (user.HasReaderPremium) return true;

        var profile = await _db.MasterProfiles
            .AsNoTracking()
            .Include(p => p.Subscription)
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

        return profile?.Subscription?.IsPremiumActive == true;
    }

    public async Task ActivateUserPremiumAsync(string userId, string externalPaymentId, int days, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return;

        var now = DateTime.UtcNow;
        var baseDate = user.HasReaderPremium && user.PremiumExpiresAt > now
            ? user.PremiumExpiresAt!.Value
            : now;

        user.PremiumExpiresAt = baseDate.AddDays(days);
        await _db.SaveChangesAsync(ct);
    }
}
