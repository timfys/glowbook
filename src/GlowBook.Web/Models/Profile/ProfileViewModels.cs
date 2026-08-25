using System.ComponentModel.DataAnnotations;

namespace GlowBook.Web.Models;

public class ProfileCardViewModel
{
    public int ProfileId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string? Specialization { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    public string BookingSlug { get; set; } = string.Empty;
    public bool HasAvatar { get; set; }
    public long? AvatarVersion { get; set; }
    public bool IsPremium { get; set; }
    public DateTime? PremiumUntil { get; set; }

    public string PremiumLabel
    {
        get
        {
            if (!IsPremium)
                return "Free";
            if (PremiumUntil == null)
                return "Premium";
            return "Premium до " + PremiumUntil.Value.ToLocalTime().ToString("dd.MM.yyyy");
        }
    }

    public string? Location
    {
        get
        {
            if (string.IsNullOrWhiteSpace(City) && string.IsNullOrWhiteSpace(Address))
                return null;
            if (string.IsNullOrWhiteSpace(City))
                return Address;
            if (string.IsNullOrWhiteSpace(Address))
                return City;
            return City + ", " + Address;
        }
    }
}

public class ProfileEditViewModel
{
    [Required(ErrorMessage = "Укажите имя")]
    [MaxLength(100)]
    [Display(Name = "Имя")]
    public string DisplayName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Некорректный телефон")]
    [MaxLength(30)]
    [Display(Name = "Телефон")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Укажите название кабинета")]
    [MaxLength(200)]
    [Display(Name = "Название кабинета")]
    public string BusinessName { get; set; } = string.Empty;

    [MaxLength(200)]
    [Display(Name = "Специализация")]
    public string? Specialization { get; set; }

    [MaxLength(100)]
    [Display(Name = "Город")]
    public string? City { get; set; }

    [MaxLength(300)]
    [Display(Name = "Адрес")]
    public string? Address { get; set; }

    [MaxLength(1000)]
    [Display(Name = "О себе")]
    public string? Description { get; set; }

    public IFormFile? Avatar { get; set; }

    [Display(Name = "Удалить фото")]
    public bool RemoveAvatar { get; set; }

    public bool HasAvatar { get; set; }

    public int ProfileId { get; set; }

    public long? AvatarVersion { get; set; }
}
