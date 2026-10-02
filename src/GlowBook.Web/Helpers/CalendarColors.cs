using GlowBook.Web.Models.Entities;
using GlowBook.Web.Models.Enums;

namespace GlowBook.Web.Helpers;

public static class CalendarColors
{
    public static readonly string[] Palette =
    [
        "#8b3db8",
        "#e45d9a",
        "#2f80ed",
        "#27ae60",
        "#f2994a",
        "#eb5757",
        "#9b51e0",
        "#56ccf2",
        "#6f6680",
        "#219653"
    ];

    public const string DefaultAccent = "#8b3db8";

    public static string ForService(Service? service)
    {
        if (IsHex(service?.Color))
            return service!.Color!;

        var id = service?.Id ?? 0;
        return Palette[Math.Abs(id) % Palette.Length];
    }

    public static string ForStatus(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Pending => "#d4a017",
        AppointmentStatus.Confirmed => "#0f7a43",
        AppointmentStatus.Completed => "#5b3aa8",
        AppointmentStatus.Cancelled => "#6f6680",
        AppointmentStatus.NoShow => "#b42318",
        _ => DefaultAccent
    };

    public static string StatusModifier(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Cancelled => "is-cancelled",
        AppointmentStatus.NoShow => "is-noshow",
        AppointmentStatus.Completed => "is-completed",
        AppointmentStatus.Confirmed => "is-confirmed",
        _ => "is-pending"
    };

    public static string NextUnused(IEnumerable<string?> used)
    {
        var taken = new HashSet<string>(
            used.Where(IsHex).Select(c => c!.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);

        return Palette.FirstOrDefault(c => !taken.Contains(c)) ?? Palette[taken.Count % Palette.Length];
    }

    public static string Normalize(string? color, string fallback = DefaultAccent) =>
        IsHex(color) ? color!.ToLowerInvariant() : fallback;

    public static bool IsHex(string? color) =>
        !string.IsNullOrWhiteSpace(color)
        && color.Length == 7
        && color[0] == '#'
        && color.Skip(1).All(Uri.IsHexDigit);
}
