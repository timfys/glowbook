using GlowBook.Web.Configuration;
using Microsoft.Extensions.Options;

namespace GlowBook.Web.Services;

public class TelegramAuthService
{
    private readonly TelegramOptions _options;

    public TelegramAuthService(IOptions<ExternalAuthSettings> options) =>
        _options = options.Value.Telegram;

    public bool TryValidate(IReadOnlyDictionary<string, string> fields, out string? error)
    {
        error = null;
        if (!_options.IsConfigured)
        {
            error = "Telegram login is not configured";
            return false;
        }

        if (!fields.TryGetValue("hash", out var hashHex) || string.IsNullOrWhiteSpace(hashHex))
        {
            error = "Missing hash";
            return false;
        }

        if (!fields.TryGetValue("auth_date", out var authDateRaw) ||
            !long.TryParse(authDateRaw, out var authDateUnix))
        {
            error = "Invalid auth_date";
            return false;
        }

        var authDate = DateTimeOffset.FromUnixTimeSeconds(authDateUnix);
        if (authDate < DateTimeOffset.UtcNow.AddDays(-1))
        {
            error = "Telegram auth expired";
            return false;
        }

        var dataCheckString = string.Join('\n',
            fields.Where(x => x.Key != "hash")
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{x.Key}={x.Value}"));

        var secretKey = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(_options.BotToken!));
        using var hmac = new System.Security.Cryptography.HMACSHA256(secretKey);
        var computed = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(dataCheckString));
        var computedHex = Convert.ToHexString(computed).ToLowerInvariant();

        if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(computedHex),
                System.Text.Encoding.UTF8.GetBytes(hashHex.ToLowerInvariant())))
        {
            error = "Invalid Telegram signature";
            return false;
        }

        return true;
    }
}
