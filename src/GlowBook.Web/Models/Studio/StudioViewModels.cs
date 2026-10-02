using System.ComponentModel.DataAnnotations;
using GlowBook.Web.Models.Entities;

namespace GlowBook.Web.Models.Studio;

public class StudioPageViewModel
{
    public string BookingSlug { get; set; } = string.Empty;

    public string? BookingUrl { get; set; }

    public bool IsPremium { get; set; }

    public string AccentColor { get; set; } = "#8b3db8";

    public bool ShowOnMap { get; set; } = true;

    public string? City { get; set; }

    public string? Address { get; set; }

    public string? Location { get; set; }

    public List<MasterPortfolioPhoto> Portfolio { get; set; } = [];

    public List<MasterPromo> Promos { get; set; } = [];

    public List<MasterReview> Reviews { get; set; } = [];

    public PromoForm Promo { get; set; } = new();
}

public class PromoForm
{
    [Required(ErrorMessage = "Укажите заголовок")]
    [MaxLength(120)]
    [Display(Name = "Заголовок")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "Описание")]
    public string? Description { get; set; }

    [MaxLength(40)]
    [Display(Name = "Плашка")]
    public string? Badge { get; set; }

    [Display(Name = "Действует до")]
    [DataType(DataType.Date)]
    public DateTime? ValidUntil { get; set; }
}
