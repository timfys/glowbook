using GlowBook.Web.Data;
using GlowBook.Web.Models.Enums;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GlowBook.Web.Models;

namespace GlowBook.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly MasterProfileService _profiles;

    public DashboardController(ApplicationDbContext db, UserManager<ApplicationUser> users, MasterProfileService profiles)
    {
        _db = db;
        _users = users;
        _profiles = profiles;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        var profile = await _profiles.EnsureForUserAsync(user);
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var todayAppointments = await _db.Appointments
            .Include(a => a.Client)
            .Include(a => a.Service)
            .Where(a => a.MasterProfileId == profile.Id && a.StartsAt >= today && a.StartsAt < tomorrow)
            .OrderBy(a => a.StartsAt)
            .ToListAsync();

        ViewBag.Profile = profile;
        ViewBag.ClientsCount = await _db.Clients.CountAsync(c => c.MasterProfileId == profile.Id);
        ViewBag.ServicesCount = await _db.Services.CountAsync(s => s.MasterProfileId == profile.Id && s.IsActive);
        ViewBag.TodayCount = todayAppointments.Count;
        ViewBag.IsPremium = profile.Subscription?.IsPremiumActive == true;
        ViewBag.BookingUrl = Url.Action("Index", "Book", new { slug = profile.BookingSlug }, Request.Scheme);

        return View(todayAppointments);
    }
}

