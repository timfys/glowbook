using System.ComponentModel.DataAnnotations;
using GlowBook.Web.Models;

namespace GlowBook.Web.Models.Entities;

public class Client
{
    public int Id { get; set; }

    public int MasterProfileId { get; set; }

    public MasterProfile? MasterProfile { get; set; }

    [Display(Name = "Имя")]
    public required string Name { get; set; }

    [Display(Name = "Телефон")]
    public required string Phone { get; set; }

    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Display(Name = "Заметки")]
    public string? Notes { get; set; }

    [Display(Name = "Аллергии")]
    public string? Allergies { get; set; }

    [Display(Name = "Проблемы кожи")]
    public string? SkinConcerns { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Linked client account (AspNetUsers), matched by email or phone.</summary>
    public string? LinkedUserId { get; set; }

    public ApplicationUser? LinkedUser { get; set; }

    public bool IsArchived { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public ICollection<TreatmentRecord> TreatmentRecords { get; set; } = new List<TreatmentRecord>();

    public ICollection<ClientPhoto> Photos { get; set; } = new List<ClientPhoto>();

    public ICollection<HomeCarePrescription> HomeCarePrescriptions { get; set; } = new List<HomeCarePrescription>();

    public ICollection<ClientMessage> Messages { get; set; } = new List<ClientMessage>();
}
