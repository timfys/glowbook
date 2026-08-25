using System.ComponentModel.DataAnnotations;
using GlowBook.Web.Models.Entities;

namespace GlowBook.Web.Models.Booking;

public class PublicBookingForm
{
    public MasterProfile? Profile { get; set; }

    public List<Service> Services { get; set; } = new();

    public List<string> AvailableTimes { get; set; } = new();

    [Required(ErrorMessage = "Выберите услугу")]
    [Display(Name = "Услуга")]
    public int ServiceId { get; set; }

    [Required(ErrorMessage = "Укажите дату")]
    [DataType(DataType.Date)]
    [Display(Name = "Дата")]
    public DateTime Date { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Выберите время")]
    [Display(Name = "Время")]
    public string Time { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите имя")]
    [Display(Name = "Ваше имя")]
    public string ClientName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите телефон")]
    [Phone(ErrorMessage = "Некорректный телефон")]
    [Display(Name = "Телефон")]
    public string ClientPhone { get; set; } = string.Empty;

    [Display(Name = "Комментарий")]
    public string? Notes { get; set; }
}
