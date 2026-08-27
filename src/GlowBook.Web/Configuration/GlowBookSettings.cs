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

    public decimal PremiumPriceRub { get; set; } = 350;
    public int PremiumDays { get; set; } = 30;

    /// <summary>Публичные реквизиты для ЮKassa / оферты (/rekvizity).</summary>
    public LegalSettings Legal { get; set; } = new();
}

public class LegalSettings
{
    public string SellerName { get; set; } = "GlowBook";
    public string Inn { get; set; } = "";
    public string Email { get; set; } = "timfy@bk.ru";
    public string? Phone { get; set; }
    public string? Address { get; set; }
}
