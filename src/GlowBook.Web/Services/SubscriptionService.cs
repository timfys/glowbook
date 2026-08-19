using GlowBook.Web.Configuration;
using GlowBook.Web.Data;
using GlowBook.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GlowBook.Web.Services;

public class SubscriptionService
{
    private readonly ApplicationDbContext _db;
    private readonly GlowBookSettings _settings;

    public SubscriptionService(ApplicationDbContext db, IOptions<GlowBookSettings> settings)
    {
        _db = db;
        _settings = settings.Value;
    }

    public async Task ActivatePremiumAsync(int masterProfileId, string externalPaymentId, CancellationToken ct = default)
    {
        var sub = await _db.Subscriptions.FirstOrDefaultAsync(s => s.MasterProfileId == masterProfileId, ct);
        if (sub == null)
        {
            sub = new Models.Entities.Subscription { MasterProfileId = masterProfileId };
            _db.Subscriptions.Add(sub);
        }

        var now = DateTime.UtcNow;
        var baseDate = sub.IsPremiumActive && sub.ExpiresAt.HasValue && sub.ExpiresAt > now
            ? sub.ExpiresAt.Value
            : now;

        sub.Plan = SubscriptionPlan.Premium;
        sub.PriceRub = _settings.PremiumPriceRub;
        sub.IsActive = true;
        sub.StartedAt = now;
        sub.ExpiresAt = baseDate.AddDays(_settings.PremiumDays);
        sub.PaymentProvider = "YooKassa";
        sub.ExternalPaymentId = externalPaymentId;

        await _db.SaveChangesAsync(ct);
    }
}
