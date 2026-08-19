using System.ComponentModel.DataAnnotations;
using GlowBook.Web.Models.Entities;

namespace GlowBook.Web.Models.Booking;

public class PublicBookingForm
{
    public MasterProfile? Profile { get; set; }

    public List<Service> Services { get; set; } = new();

    public List<string> AvailableTimes { get; set; } = new();

    [Required(ErrorMessage = "Выберите услугу")]
    public int ServiceId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Выберите время")]
    public string Time { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите имя")]
    public string ClientName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите телефон")]
    [Phone]
    public string ClientPhone { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
