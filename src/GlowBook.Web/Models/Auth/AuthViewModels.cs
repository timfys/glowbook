using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using GlowBook.Web.Configuration;
using GlowBook.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GlowBook.Web.Models.Auth;

public class LoginViewModel
{
    [Required(ErrorMessage = "Укажите email")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите пароль")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Укажите имя")]
    [Display(Name = "Имя")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите email")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите пароль")]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Пароли не совпадают")]
    [Display(Name = "Повтор пароля")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class AuthProvidersViewModel
{
    public bool Google { get; set; }
    public bool MailRu { get; set; }
    public bool Telegram { get; set; }
    public string? TelegramBotUsername { get; set; }
}
