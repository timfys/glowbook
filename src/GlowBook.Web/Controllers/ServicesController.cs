using GlowBook.Web.Data;
using GlowBook.Web.Filters;
using GlowBook.Web.Models;
using GlowBook.Web.Models.Entities;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GlowBook.Web.Models;

namespace GlowBook.Web.Controllers;

[Authorize]
[RequireMasterAccount]
public class ServicesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly MasterProfileService _profiles;

    public ServicesController(ApplicationDbContext db, UserManager<ApplicationUser> users, MasterProfileService profiles)
    {
        _db = db;
        _users = users;
        _profiles = profiles;
    }

    public async Task<IActionResult> Index()
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var services = await _db.Services
            .Where(s => s.MasterProfileId == profile.Id)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync();

        return View(services);
    }

    public IActionResult Create() => View(new Service { Name = "", DurationMinutes = 60, Price = 0, IsActive = true });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Service model)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();
        if (!ModelState.IsValid) return View(model);

        model.MasterProfileId = profile.Id;
        _db.Services.Add(model);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task<MasterProfile?> GetProfileAsync()
    {
        var user = await _users.GetUserAsync(User);
        return user == null ? null : await _profiles.EnsureForUserAsync(user);
    }
}
