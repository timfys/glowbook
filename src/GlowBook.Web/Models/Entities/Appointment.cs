using GlowBook.Web.Models.Enums;

namespace GlowBook.Web.Models.Entities;

public class Appointment
{
    public int Id { get; set; }

    public int MasterProfileId { get; set; }

    public MasterProfile? MasterProfile { get; set; }

    public int ClientId { get; set; }

    public Client? Client { get; set; }

    public int ServiceId { get; set; }

    public Service? Service { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
