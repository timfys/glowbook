namespace GlowBook.Web.Models.Entities;

public class TreatmentRecord
{
    public int Id { get; set; }

    public int ClientId { get; set; }

    public Client? Client { get; set; }

    public int MasterProfileId { get; set; }

    public MasterProfile? MasterProfile { get; set; }

    public int? AppointmentId { get; set; }

    public Appointment? Appointment { get; set; }

    public int? ServiceId { get; set; }

    public Service? Service { get; set; }

    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

    public string? ProcedureName { get; set; }

    public string? ProductsUsed { get; set; }

    public string? EquipmentUsed { get; set; }

    public string? Notes { get; set; }

    public decimal? Price { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
