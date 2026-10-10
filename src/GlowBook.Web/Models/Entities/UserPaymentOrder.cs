using GlowBook.Web.Models;

namespace GlowBook.Web.Models.Entities;

public class UserPaymentOrder
{
    public int Id { get; set; }

    public required string UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public required string YooKassaPaymentId { get; set; }

    public decimal AmountRub { get; set; }

    public required string Status { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }
}
