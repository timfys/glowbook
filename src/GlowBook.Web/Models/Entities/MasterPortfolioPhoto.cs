namespace GlowBook.Web.Models.Entities;

public class MasterPortfolioPhoto
{
    public int Id { get; set; }

    public int MasterProfileId { get; set; }

    public MasterProfile? MasterProfile { get; set; }

    public byte[] Data { get; set; } = [];

    public string ContentType { get; set; } = "image/jpeg";

    public string? Caption { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
