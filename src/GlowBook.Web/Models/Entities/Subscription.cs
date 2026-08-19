using GlowBook.Web.Models.Enums;

namespace GlowBook.Web.Models.Entities;

public class Subscription
{
    public int Id { get; set; }

    public int MasterProfileId { get; set; }

    public MasterProfile? MasterProfile { get; set; }

    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;

    public decimal PriceRub { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;

    public string? PaymentProvider { get; set; }

    public string? ExternalPaymentId { get; set; }

    public bool IsPremiumActive =>
        Plan == SubscriptionPlan.Premium && IsActive && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);
}
