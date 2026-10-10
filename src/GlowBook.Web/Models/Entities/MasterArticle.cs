using System.ComponentModel.DataAnnotations;

namespace GlowBook.Web.Models.Entities;

public class MasterArticle
{
    public int Id { get; set; }

    public int MasterProfileId { get; set; }

    public MasterProfile? MasterProfile { get; set; }

    [MaxLength(160)]
    public required string Title { get; set; }

    [MaxLength(120)]
    public required string Slug { get; set; }

    [MaxLength(320)]
    public string? Excerpt { get; set; }

    public required string Body { get; set; }

    public byte[]? CoverData { get; set; }

    [MaxLength(100)]
    public string? CoverContentType { get; set; }

    public bool IsPublished { get; set; }

    public bool IsPremiumOnly { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PublishedAt { get; set; }

    public bool HasCover => CoverData is { Length: > 0 };
}
