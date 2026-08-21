namespace GlowBook.Web.Models.Entities;

public class MasterAvatar
{
    public int MasterProfileId { get; set; }

    public MasterProfile MasterProfile { get; set; } = null!;

    public byte[] Data { get; set; } = [];

    public string ContentType { get; set; } = "image/jpeg";
}
