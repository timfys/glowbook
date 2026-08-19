using GlowBook.Web.Models;
namespace GlowBook.Web.Models.Entities;

public class MasterProfile
{
    public int Id { get; set; }

    public required string UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public string? Specialization { get; set; }

    public string? City { get; set; }

    public string? Address { get; set; }

    public string? Description { get; set; }

    public string? AvatarUrl { get; set; }

    public required string BookingSlug { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Client> Clients { get; set; } = new List<Client>();

    public ICollection<Service> Services { get; set; } = new List<Service>();

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public ICollection<WorkingHour> WorkingHours { get; set; } = new List<WorkingHour>();

    public Subscription? Subscription { get; set; }
}
