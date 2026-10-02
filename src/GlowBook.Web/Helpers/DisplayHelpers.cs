using System.ComponentModel.DataAnnotations;
using System.Reflection;
using GlowBook.Web.Models.Enums;

namespace GlowBook.Web.Helpers;

public static class DisplayHelpers
{
    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();

        return string.Concat(char.ToUpperInvariant(parts[0][0]), char.ToUpperInvariant(parts[^1][0]));
    }

    public static string DisplayName(this Enum value)
    {
        var attr = value.GetType()
            .GetField(value.ToString())
            ?.GetCustomAttribute<DisplayAttribute>();
        return attr?.Name ?? value.ToString();
    }

    public static string StatusClass(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Pending => "gb-status gb-status-pending",
        AppointmentStatus.Confirmed => "gb-status gb-status-confirmed",
        AppointmentStatus.Completed => "gb-status gb-status-completed",
        AppointmentStatus.Cancelled => "gb-status gb-status-cancelled",
        AppointmentStatus.NoShow => "gb-status gb-status-noshow",
        _ => "gb-status"
    };

    public static string PremiumUntilDate(DateTime? expiresAt)
    {
        if (expiresAt == null)
            return "бессрочно";
        return expiresAt.Value.ToLocalTime().ToString("d MMMM yyyy");
    }

    public static string PremiumBadgeText(DateTime? expiresAt) =>
        expiresAt == null ? "Premium" : "Premium до " + expiresAt.Value.ToLocalTime().ToString("dd.MM.yyyy");

    public static int? PremiumDaysLeft(DateTime? expiresAt)
    {
        if (expiresAt == null)
            return null;
        return Math.Max(0, (int)Math.Ceiling((expiresAt.Value - DateTime.UtcNow).TotalDays));
    }
}
