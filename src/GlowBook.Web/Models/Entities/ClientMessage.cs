using GlowBook.Web.Models;

namespace GlowBook.Web.Models.Entities;

public class ClientMessage
{
    public int Id { get; set; }

    public int ClientId { get; set; }

    public Client Client { get; set; } = null!;

    public required string SenderUserId { get; set; }

    public ApplicationUser SenderUser { get; set; } = null!;

    public string Body { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? AttachmentFileName { get; set; }

    public string? AttachmentContentType { get; set; }

    public byte[]? AttachmentData { get; set; }
}
