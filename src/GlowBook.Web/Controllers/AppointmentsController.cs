using GlowBook.Web.Data;
using GlowBook.Web.Models.Entities;
using GlowBook.Web.Models.Enums;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using GlowBook.Web.Models;

namespace GlowBook.Web.Controllers;

[Authorize]
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
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadLookupsAsync(int profileId)
    {
        ViewBag.Clients = new SelectList(await _db.Clients.Where(c => c.MasterProfileId == profileId).OrderBy(c => c.Name).ToListAsync(), "Id", "Name");
        ViewBag.Services = new SelectList(await _db.Services.Where(s => s.MasterProfileId == profileId && s.IsActive).OrderBy(s => s.Name).ToListAsync(), "Id", "Name");
    }

    private async Task<MasterProfile?> GetProfileAsync()
    {
        var user = await _users.GetUserAsync(User);
        return user == null ? null : await _profiles.EnsureForUserAsync(user);
    }
}
