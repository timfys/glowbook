using GlowBook.Web.Models;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GlowBook.Web.Controllers;

[Authorize]
[Route("api/appointment-reminders")]
public class AppointmentRemindersController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly AppointmentReminderService _reminders;

    public AppointmentRemindersController(
        UserManager<ApplicationUser> users,
        AppointmentReminderService reminders)
    {
        _users = users;
        _reminders = reminders;
    }

    [HttpGet("upcoming")]
    public async Task<IActionResult> Upcoming(CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        var items = await _reminders.GetUpcomingAsync(user, ct);
        return Json(items);
    }
}
