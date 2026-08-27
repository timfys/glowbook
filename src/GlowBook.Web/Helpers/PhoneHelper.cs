namespace GlowBook.Web.Helpers;

public static class PhoneHelper
{
    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits[0] == '8')
            return "7" + digits[1..];
        if (digits.Length == 10)
            return "7" + digits;
        return digits;
    }

    public static bool Match(string? a, string? b) =>
        !string.IsNullOrEmpty(Normalize(a)) && Normalize(a) == Normalize(b);
}
