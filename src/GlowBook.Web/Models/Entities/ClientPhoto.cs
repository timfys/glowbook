using GlowBook.Web.Models.Enums;

namespace GlowBook.Web.Models.Entities;

public class ClientPhoto
{
    public int Id { get; set; }

    public int ClientId { get; set; }

    public Client? Client { get; set; }

    public int MasterProfileId { get; set; }

    public MasterProfile? MasterProfile { get; set; }

    public PhotoKind Kind { get; set; } = PhotoKind.Before;

    public byte[] Data { get; set; } = [];

    public string ContentType { get; set; } = "image/jpeg";

    public string? Caption { get; set; }

    public DateTime TakenAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
