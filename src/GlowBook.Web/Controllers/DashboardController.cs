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
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var nextMonth = monthStart.AddMonths(1);

        var todayAppointments = await _db.Appointments
            .Include(a => a.Client)
            .Include(a => a.Service)
            .Where(a => a.MasterProfileId == profile.Id && a.StartsAt >= today && a.StartsAt < tomorrow)
            .OrderBy(a => a.StartsAt)
            .ToListAsync();

        var completedToday = await _db.Appointments
            .Include(a => a.Service)
            .Where(a => a.MasterProfileId == profile.Id
                && a.Status == AppointmentStatus.Completed
                && a.StartsAt >= today && a.StartsAt < tomorrow)
            .ToListAsync();

        var completedMonth = await _db.Appointments
            .Include(a => a.Service)
            .Where(a => a.MasterProfileId == profile.Id
                && a.Status == AppointmentStatus.Completed
                && a.StartsAt >= monthStart && a.StartsAt < nextMonth)
            .ToListAsync();

        var treatmentRevenueToday = await _db.TreatmentRecords
            .Where(t => t.MasterProfileId == profile.Id && t.PerformedAt >= today && t.PerformedAt < tomorrow && t.Price != null)
            .SumAsync(t => t.Price ?? 0);

        var treatmentRevenueMonth = await _db.TreatmentRecords
            .Where(t => t.MasterProfileId == profile.Id && t.PerformedAt >= monthStart && t.PerformedAt < nextMonth && t.Price != null)
            .SumAsync(t => t.Price ?? 0);

        var appointmentRevenueToday = completedToday.Sum(a => a.Service?.Price ?? 0);
        var appointmentRevenueMonth = completedMonth.Sum(a => a.Service?.Price ?? 0);

        ViewBag.Profile = profile;
        ViewBag.ClientsCount = await _db.Clients.CountAsync(c => c.MasterProfileId == profile.Id);
        ViewBag.ServicesCount = await _db.Services.CountAsync(s => s.MasterProfileId == profile.Id && s.IsActive);
        ViewBag.TodayCount = todayAppointments.Count;
        ViewBag.RevenueToday = appointmentRevenueToday + treatmentRevenueToday;
        ViewBag.RevenueMonth = appointmentRevenueMonth + treatmentRevenueMonth;
        ViewBag.IsPremium = profile.Subscription?.IsPremiumActive == true;
        ViewBag.BookingUrl = Url.Action("Index", "Book", new { slug = profile.BookingSlug }, Request.Scheme);

        return View(todayAppointments);
    }
}
