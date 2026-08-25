namespace GlowBook.Web.Configuration;

public class ExternalAuthSettings
{
    public const string SectionName = "Authentication";

    public OAuthProviderOptions Google { get; set; } = new();
    public OAuthProviderOptions MailRu { get; set; } = new();
    public OAuthProviderOptions VkId { get; set; } = new();
    public TelegramOptions Telegram { get; set; } = new();
}

public class OAuthProviderOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

public class TelegramOptions
{
    public string? BotToken { get; set; }
    public string? BotUsername { get; set; }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(BotUsername);
}
