using System.ComponentModel.DataAnnotations;

namespace GlowBook.Web.Models.Booking;

public class PublicReviewForm
{
    [Required(ErrorMessage = "Укажите имя")]
    [MaxLength(80)]
    [Display(Name = "Ваше имя")]
    public string AuthorName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите телефон")]
    [Phone(ErrorMessage = "Некорректный телефон")]
    [Display(Name = "Телефон")]
    public string AuthorPhone { get; set; } = string.Empty;

    [Range(1, 5)]
    [Display(Name = "Оценка")]
    public int Rating { get; set; } = 5;

    [Required(ErrorMessage = "Напишите отзыв")]
    [MaxLength(800)]
    [Display(Name = "Отзыв")]
    public string Text { get; set; } = string.Empty;
}
