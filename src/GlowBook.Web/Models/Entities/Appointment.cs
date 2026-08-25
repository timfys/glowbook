using System.ComponentModel.DataAnnotations;
using GlowBook.Web.Models.Enums;

namespace GlowBook.Web.Models.Entities;

public class Appointment
{
    public int Id { get; set; }

    public int MasterProfileId { get; set; }

    public MasterProfile? MasterProfile { get; set; }

    [Display(Name = "Клиент")]
    public int ClientId { get; set; }

    public Client? Client { get; set; }

    [Display(Name = "Услуга")]
    public int ServiceId { get; set; }

    public Service? Service { get; set; }

    [Display(Name = "Начало")]
    public DateTime StartsAt { get; set; }

    [Display(Name = "Конец")]
    public DateTime EndsAt { get; set; }

    [Display(Name = "Статус")]
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

    [Display(Name = "Заметки")]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
