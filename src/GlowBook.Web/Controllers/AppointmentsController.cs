using GlowBook.Web.Data;
using GlowBook.Web.Filters;
using GlowBook.Web.Models;
using GlowBook.Web.Models.Entities;
using GlowBook.Web.Models.Enums;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GlowBook.Web.Controllers;

[Authorize]
[RequireMasterAccount]
public class AppointmentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly MasterProfileService _profiles;

    public AppointmentsController(ApplicationDbContext db, UserManager<ApplicationUser> users, MasterProfileService profiles)
    {
        _db = db;
        _users = users;
        _profiles = profiles;
    }

    public async Task<IActionResult> Index()
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var items = await _db.Appointments
            .Include(a => a.Client)
            .Include(a => a.Service)
            .Where(a => a.MasterProfileId == profile.Id)
            .OrderByDescending(a => a.StartsAt)
            .Take(100)
            .ToListAsync();

        return View(items);
    }

    public async Task<IActionResult> Calendar(DateTime? week, DateTime? month, string? view)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var calendarView = string.Equals(view, "month", StringComparison.OrdinalIgnoreCase) ? "month" : "week";

        if (calendarView == "month")
        {
            var anchor = month?.Date ?? DateTime.Today;
            var monthStart = new DateTime(anchor.Year, anchor.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var items = await _db.Appointments
                .Include(a => a.Client)
                .Include(a => a.Service)
                .Where(a => a.MasterProfileId == profile.Id && a.StartsAt >= monthStart && a.StartsAt < monthEnd)
                .OrderBy(a => a.StartsAt)
                .ToListAsync();

            ViewBag.ViewMode = "month";
            ViewBag.MonthStart = monthStart;
            ViewBag.PrevMonth = monthStart.AddMonths(-1);
            ViewBag.NextMonth = monthStart.AddMonths(1);
            ViewBag.CalendarWeeks = BuildMonthWeeks(monthStart);
            return View("Calendar", items);
        }

        var weekAnchor = week?.Date ?? DateTime.Today;
        var start = StartOfWeek(weekAnchor);
        var end = start.AddDays(7);

        var weekItems = await _db.Appointments
            .Include(a => a.Client)
            .Include(a => a.Service)
            .Where(a => a.MasterProfileId == profile.Id && a.StartsAt >= start && a.StartsAt < end)
            .OrderBy(a => a.StartsAt)
            .ToListAsync();

        ViewBag.ViewMode = "week";
        ViewBag.WeekStart = start;
        ViewBag.PrevWeek = start.AddDays(-7);
        ViewBag.NextWeek = start.AddDays(7);
        ViewBag.Days = Enumerable.Range(0, 7).Select(i => start.AddDays(i)).ToList();
        return View("Calendar", weekItems);
    }

    public async Task<IActionResult> Create()
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();
        await LoadLookupsAsync(profile.Id);
        return View(new Appointment
        {
            StartsAt = DateTime.Today.AddHours(12),
            EndsAt = DateTime.Today.AddHours(13),
            Status = AppointmentStatus.Pending
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Appointment model)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        if (model.EndsAt <= model.StartsAt)
            ModelState.AddModelError(nameof(model.EndsAt), "Конец должен быть позже начала");

        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync(profile.Id);
            return View(model);
        }

        model.MasterProfileId = profile.Id;
        model.CreatedAt = DateTime.UtcNow;
        _db.Appointments.Add(model);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Calendar));
    }

    public async Task<IActionResult> Edit(int id, string? returnUrl = null)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(a => a.Id == id && a.MasterProfileId == profile.Id);
        if (appointment == null) return NotFound();

        await LoadLookupsAsync(profile.Id);
        ViewBag.ReturnUrl = returnUrl;
        return View(appointment);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Appointment model, string? returnUrl = null)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(a => a.Id == id && a.MasterProfileId == profile.Id);
        if (appointment == null) return NotFound();

        if (model.EndsAt <= model.StartsAt)
            ModelState.AddModelError(nameof(model.EndsAt), "Конец должен быть позже начала");

        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync(profile.Id);
            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        appointment.ClientId = model.ClientId;
        appointment.ServiceId = model.ServiceId;
        appointment.StartsAt = model.StartsAt;
        appointment.EndsAt = model.EndsAt;
        appointment.Status = model.Status;
        appointment.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Calendar));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTime(int id, string startsAtTime, string endsAtTime, string? returnUrl = null)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(a => a.Id == id && a.MasterProfileId == profile.Id);
        if (appointment == null) return NotFound();

        string? error = null;
        if (!TimeSpan.TryParse(startsAtTime, out var startTime) || !TimeSpan.TryParse(endsAtTime, out var endTime))
        {
            error = "Неверный формат времени";
        }
        else
        {
            var newStart = appointment.StartsAt.Date.Add(startTime);
            var newEnd = appointment.StartsAt.Date.Add(endTime);
            if (newEnd <= newStart)
                error = "Конец должен быть позже начала";
            else
            {
                appointment.StartsAt = newStart;
                appointment.EndsAt = newEnd;
                await _db.SaveChangesAsync();
            }
        }

        if (WantsJson())
        {
            if (error != null)
                return BadRequest(new { error });

            return Json(new
            {
                ok = true,
                startsAt = appointment.StartsAt.ToString("HH:mm"),
                endsAt = appointment.EndsAt.ToString("HH:mm")
            });
        }

        if (error != null)
            TempData["CalendarError"] = error;

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Calendar));
    }

    private bool WantsJson() =>
        string.Equals(Request.Headers.XRequestedWith, "fetch", StringComparison.OrdinalIgnoreCase)
        || (Request.Headers.Accept.ToString()?.Contains("application/json", StringComparison.OrdinalIgnoreCase) ?? false);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int id, AppointmentStatus status, string? returnUrl = null)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(a => a.Id == id && a.MasterProfileId == profile.Id);
        if (appointment == null) return NotFound();

        appointment.Status = status;
        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnUrl = null)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(a => a.Id == id && a.MasterProfileId == profile.Id);
        if (appointment == null) return NotFound();

        _db.Appointments.Remove(appointment);
        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    private async Task LoadLookupsAsync(int profileId)
    {
        ViewBag.Clients = new SelectList(
            await _db.Clients.Where(c => c.MasterProfileId == profileId && !c.IsArchived).OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name");
        ViewBag.Services = new SelectList(await _db.Services.Where(s => s.MasterProfileId == profileId && s.IsActive).OrderBy(s => s.Name).ToListAsync(), "Id", "Name");
    }

    private async Task<MasterProfile?> GetProfileAsync()
    {
        var user = await _users.GetUserAsync(User);
        return user == null ? null : await _profiles.EnsureForUserAsync(user);
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7; // Monday = 0
        return date.Date.AddDays(-diff);
    }

    private static List<List<DateTime>> BuildMonthWeeks(DateTime monthStart)
    {
        var firstCell = StartOfWeek(monthStart);
        var weeks = new List<List<DateTime>>();
        for (var w = 0; w < 6; w++)
        {
            var weekStart = firstCell.AddDays(w * 7);
            weeks.Add(Enumerable.Range(0, 7).Select(i => weekStart.AddDays(i)).ToList());
        }
        return weeks;
    }
}
