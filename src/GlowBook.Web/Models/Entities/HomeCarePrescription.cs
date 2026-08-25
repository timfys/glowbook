namespace GlowBook.Web.Models.Entities;

public class HomeCarePrescription
{
    public int Id { get; set; }

    public int ClientId { get; set; }

    public Client? Client { get; set; }

    public int MasterProfileId { get; set; }

    public MasterProfile? MasterProfile { get; set; }

    public required string Title { get; set; }

    public string? Instructions { get; set; }

    public string? Products { get; set; }

    public DateTime PrescribedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
