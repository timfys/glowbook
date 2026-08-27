using GlowBook.Web.Models;
using GlowBook.Web.Models.Entities;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GlowBook.Web.ViewComponents;

public class CabinetSidebarViewComponent : ViewComponent
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly MasterProfileService _profiles;

    public CabinetSidebarViewComponent(UserManager<ApplicationUser> users, MasterProfileService profiles)
    {
        _users = users;
        _profiles = profiles;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = await _users.GetUserAsync(HttpContext.User);
        if (user == null)
            return Content(string.Empty);

        if (user.AccountType == Models.Enums.UserAccountType.Client)
            return View("Client", new ClientCabinetSidebarModel
            {
                DisplayName = user.DisplayName ?? user.Email ?? "Клиент",
                ActiveController = ViewContext.RouteData.Values["controller"]?.ToString() ?? "",
                ActiveAction = ViewContext.RouteData.Values["action"]?.ToString() ?? ""
            });

        var profile = await _profiles.EnsureForUserAsync(user);
        var controller = ViewContext.RouteData.Values["controller"]?.ToString() ?? "";

        return View(new CabinetSidebarModel
        {
            Profile = profile,
            DisplayName = user.DisplayName ?? profile.BusinessName,
            ActiveController = controller,
            ActiveAction = ViewContext.RouteData.Values["action"]?.ToString() ?? "",
            IsPremium = profile.Subscription?.IsPremiumActive == true
        });
    }
}

public class CabinetSidebarModel
{
    public MasterProfile Profile { get; set; } = null!;
    public string DisplayName { get; set; } = "";
    public string ActiveController { get; set; } = "";
    public string ActiveAction { get; set; } = "";
    public bool IsPremium { get; set; }
}

public class ClientCabinetSidebarModel
{
    public string DisplayName { get; set; } = "";
    public string ActiveController { get; set; } = "";
    public string ActiveAction { get; set; } = "";
}
