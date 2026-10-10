using System.ComponentModel.DataAnnotations;
using GlowBook.Web.Models.Entities;

namespace GlowBook.Web.Models.Blog;

public class ArticleEditForm
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Укажите заголовок")]
    [MaxLength(160)]
    [Display(Name = "Заголовок")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(120)]
    [Display(Name = "Адрес в ссылке")]
    [RegularExpression(@"^$|^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Только латиница, цифры и дефис")]
    public string? Slug { get; set; }

    [MaxLength(320)]
    [Display(Name = "Краткое описание")]
    public string? Excerpt { get; set; }

    [Required(ErrorMessage = "Напишите текст")]
    [MaxLength(50000)]
    [Display(Name = "Текст")]
    public string Body { get; set; } = string.Empty;

    [Display(Name = "Только для Premium")]
    public bool IsPremiumOnly { get; set; }

    [Display(Name = "Опубликовать")]
    public bool IsPublished { get; set; } = true;

    public IFormFile? Cover { get; set; }

    [Display(Name = "Удалить обложку")]
    public bool RemoveCover { get; set; }

    public bool HasCover { get; set; }
}

public class StudioBlogPageViewModel
{
    public string BookingSlug { get; set; } = string.Empty;
    public string PublicBlogUrl { get; set; } = string.Empty;
    public List<MasterArticle> Articles { get; set; } = [];
}

public class PublicArticleCard
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Excerpt { get; set; }
    public bool IsPremiumOnly { get; set; }
    public bool HasCover { get; set; }
    public DateTime PublishedAt { get; set; }
}

public class PublicBlogListViewModel
{
    public string Username { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string? Specialization { get; set; }
    public string AccentColor { get; set; } = "#8b3db8";
    public bool HasAvatar { get; set; }
    public int ProfileId { get; set; }
    public long? AvatarVersion { get; set; }
    public string? BookingUrl { get; set; }
    public List<PublicArticleCard> Articles { get; set; } = [];
}

public class PublicArticleViewModel
{
    public string Username { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string? Specialization { get; set; }
    public string AccentColor { get; set; } = "#8b3db8";
    public bool HasAvatar { get; set; }
    public int ProfileId { get; set; }
    public long? AvatarVersion { get; set; }
    public string? BookingUrl { get; set; }

    public int ArticleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Excerpt { get; set; }
    public string? BodyHtml { get; set; }
    public bool IsPremiumOnly { get; set; }
    public bool HasCover { get; set; }
    public bool CanReadFull { get; set; }
    public bool IsAuthenticated { get; set; }
    public bool ViewerHasPremium { get; set; }
    public DateTime PublishedAt { get; set; }
}

public class MasterSearchResult
{
    public string Username { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string? Specialization { get; set; }
    public string? City { get; set; }
    public bool HasAvatar { get; set; }
    public int ProfileId { get; set; }
    public long? AvatarVersion { get; set; }
    public int PublishedArticles { get; set; }
    public bool IsPremiumMaster { get; set; }
}

public class MasterSearchViewModel
{
    public string? Query { get; set; }
    public List<MasterSearchResult> Results { get; set; } = [];
}
