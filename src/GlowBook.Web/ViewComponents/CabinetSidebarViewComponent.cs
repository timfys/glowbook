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

        var profile = await _profiles.EnsureForUserAsync(user);
        var controller = ViewContext.RouteData.Values["controller"]?.ToString() ?? "";

        return View(new CabinetSidebarModel
        {
            Profile = profile,
            ActiveController = controller,
            IsPremium = profile.Subscription?.IsPremiumActive == true
        });
    }
}

public class CabinetSidebarModel
{
    public MasterProfile Profile { get; set; } = null!;
    public string ActiveController { get; set; } = "";
    public bool IsPremium { get; set; }
}
