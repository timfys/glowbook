using System.ComponentModel.DataAnnotations;

namespace GlowBook.Web.Models.Enums;

public enum AppointmentStatus
{
    [Display(Name = "Ожидает")]
    Pending = 0,
    [Display(Name = "Подтверждена")]
    Confirmed = 1,
    [Display(Name = "Завершена")]
    Completed = 2,
    [Display(Name = "Отменена")]
    Cancelled = 3,
    [Display(Name = "Не пришёл")]
    NoShow = 4
}
