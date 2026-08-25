using System.ComponentModel.DataAnnotations;

namespace GlowBook.Web.Models.Entities;

public class Service
{
    public int Id { get; set; }

    public int MasterProfileId { get; set; }

    public MasterProfile? MasterProfile { get; set; }

    [Display(Name = "Название")]
    public required string Name { get; set; }

    [Display(Name = "Описание")]
    public string? Description { get; set; }

    [Display(Name = "Длительность, мин")]
    public int DurationMinutes { get; set; } = 60;

    [Display(Name = "Цена, ₽")]
    public decimal Price { get; set; }

    [Display(Name = "Активна")]
    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
