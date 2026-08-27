using GlowBook.Web.Filters;
using GlowBook.Web.Models;
using GlowBook.Web.Models.Clients;
using GlowBook.Web.Models.Enums;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GlowBook.Web.Controllers;

[Authorize]
[RequireClientAccount]
[Route("my")]
public class MyController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ClientAccountService _clients;

    public MyController(UserManager<ApplicationUser> users, ClientAccountService clients)
    {
        _users = users;
        _clients = clients;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        var appointments = await _clients.GetAppointmentsAsync(user);
        ViewBag.DisplayName = user.DisplayName ?? user.Email;
        return View(appointments);
    }

    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        return View(new ClientProfileEditViewModel
        {
            DisplayName = user.DisplayName ?? "",
            Email = user.Email ?? "",
            Phone = user.PhoneNumber
        });
    }

    [HttpPost("profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ClientProfileEditViewModel model)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        if (!ModelState.IsValid)
            return View(model);

        user.DisplayName = model.DisplayName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();

        if (!string.Equals(user.Email, model.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var setEmail = await _users.SetEmailAsync(user, model.Email.Trim());
            if (!setEmail.Succeeded)
            {
                foreach (var err in setEmail.Errors)
                    ModelState.AddModelError(nameof(model.Email), err.Description);
                return View(model);
            }

            user.UserName = model.Email.Trim();
            await _users.SetUserNameAsync(user, model.Email.Trim());
        }

        await _users.UpdateAsync(user);
        await _clients.LinkClientsToUserAsync(user);

        TempData["Success"] = "Профиль сохранён";
        return RedirectToAction(nameof(Profile));
    }
}
