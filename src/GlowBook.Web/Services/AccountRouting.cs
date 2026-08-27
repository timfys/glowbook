using GlowBook.Web.Models;
using GlowBook.Web.Models.Enums;

namespace GlowBook.Web.Services;

public static class AccountRouting
{
    public static string HomeFor(ApplicationUser user) =>
        user.AccountType == UserAccountType.Client ? "/my" : "/Dashboard";

    public static bool IsClientArea(string? controller) =>
        string.Equals(controller, "My", StringComparison.OrdinalIgnoreCase);
}
