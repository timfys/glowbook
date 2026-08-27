using System.ComponentModel.DataAnnotations;

namespace GlowBook.Web.Models.Clients;

public class ClientProfileEditViewModel
{
    [Required(ErrorMessage = "Укажите имя")]
    [Display(Name = "Имя")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите email")]
    [EmailAddress(ErrorMessage = "Некорректный email")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Телефон")]
    [Phone(ErrorMessage = "Некорректный телефон")]
    public string? Phone { get; set; }
}
