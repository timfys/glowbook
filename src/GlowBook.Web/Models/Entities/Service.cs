namespace GlowBook.Web.Models.Entities;

public class Service
{
    public int Id { get; set; }

    public int MasterProfileId { get; set; }

    public MasterProfile? MasterProfile { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public int DurationMinutes { get; set; } = 60;

    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
