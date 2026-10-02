namespace GlowBook.Web.Helpers;

public static class ImageContentHelper
{
    public static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
    };

    public const long MaxBytes = 5 * 1024 * 1024;

    public static string? DetectContentType(byte[] data, string? reported)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "image/jpeg";
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return "image/png";
        if (data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
            return "image/gif";
        if (data.Length >= 12
            && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46
            && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
            return "image/webp";

        if (!string.IsNullOrWhiteSpace(reported) && AllowedTypes.Contains(reported))
            return reported.Equals("image/jpg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : reported;

        return null;
    }

    public static async Task<(byte[]? Data, string? ContentType, string? Error)> ReadAsync(IFormFile file, long maxBytes = MaxBytes)
    {
        if (file.Length <= 0)
            return (null, null, "Файл пустой");
        if (file.Length > maxBytes)
            return (null, null, "Фото не должно быть больше 5 МБ");

        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        var data = ms.ToArray();
        var contentType = DetectContentType(data, file.ContentType);
        if (contentType == null)
            return (null, null, "Поддерживаются JPEG, PNG, WebP и GIF.");

        return (data, contentType, null);
    }
}
