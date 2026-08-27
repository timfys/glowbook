using GlowBook.Web.Models;

namespace GlowBook.Web.Models.Entities;

public class ClientAvatar
{
    public required string UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public byte[] Data { get; set; } = [];

    public string ContentType { get; set; } = "image/jpeg";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
