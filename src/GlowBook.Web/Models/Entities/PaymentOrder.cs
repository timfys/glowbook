namespace GlowBook.Web.Models.Entities;

public class PaymentOrder
{
    public int Id { get; set; }

    public int MasterProfileId { get; set; }

    public MasterProfile? MasterProfile { get; set; }

    public required string YooKassaPaymentId { get; set; }

    public decimal AmountRub { get; set; }

    public required string Status { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }
}
