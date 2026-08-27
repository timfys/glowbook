using GlowBook.Web.Models.Enums;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using GlowBook.Web.Models;

namespace GlowBook.Web.Filters;

public class RequireMasterAccountAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.GetUserAsync(context.HttpContext.User);
        if (user != null && user.AccountType == UserAccountType.Client)
        {
            context.Result = new RedirectToActionResult("Index", "My", null);
            return;
        }

        await next();
    }
}

public class RequireClientAccountAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.GetUserAsync(context.HttpContext.User);
        if (user == null || user.AccountType != UserAccountType.Client)
        {
            context.Result = new RedirectToActionResult("Index", "Dashboard", null);
            return;
        }

        await next();
    }
}
