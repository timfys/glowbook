namespace GlowBook.Web.Models.Entities;

public class MasterReview
{
    public int Id { get; set; }

    public int MasterProfileId { get; set; }

    public MasterProfile? MasterProfile { get; set; }

    public int? ClientId { get; set; }

    public Client? Client { get; set; }

    public string AuthorName { get; set; } = string.Empty;

    public string AuthorPhone { get; set; } = string.Empty;

    public int Rating { get; set; } = 5;

    public string Text { get; set; } = string.Empty;

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
