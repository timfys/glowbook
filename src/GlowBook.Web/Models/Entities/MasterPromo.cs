using System.ComponentModel.DataAnnotations;

namespace GlowBook.Web.Models.Entities;

public class MasterPromo
{
    public int Id { get; set; }

    public int MasterProfileId { get; set; }

    public MasterProfile? MasterProfile { get; set; }

    [Display(Name = "Заголовок")]
    [MaxLength(120)]
    public required string Title { get; set; }

    [Display(Name = "Описание")]
    [MaxLength(500)]
    public string? Description { get; set; }

    [Display(Name = "Плашка")]
    [MaxLength(40)]
    public string? Badge { get; set; }

    [Display(Name = "Действует до")]
    public DateTime? ValidUntil { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
