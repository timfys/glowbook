namespace GlowBook.Web.Configuration;

public class YooKassaSettings
{
    public const string SectionName = "YooKassa";

    public string? ShopId { get; set; }
    public string? SecretKey { get; set; }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ShopId) && !string.IsNullOrWhiteSpace(SecretKey);
}

public class GlowBookSettings
{
    public const string SectionName = "GlowBook";

    public decimal PremiumPriceRub { get; set; } = 500;
    public int PremiumDays { get; set; } = 30;
}
