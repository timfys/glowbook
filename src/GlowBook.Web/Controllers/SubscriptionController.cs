using GlowBook.Web.Filters;
using GlowBook.Web.Configuration;
using GlowBook.Web.Models;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GlowBook.Web.Controllers;

[Authorize]
[RequireMasterAccount]
public class SubscriptionController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly MasterProfileService _profiles;
    private readonly YooKassaService _yooKassa;
    private readonly GlowBookSettings _settings;
    private readonly YooKassaSettings _yooKassaSettings;

    public SubscriptionController(
        UserManager<ApplicationUser> users,
        MasterProfileService profiles,
        YooKassaService yooKassa,
        IOptions<GlowBookSettings> settings,
        IOptions<YooKassaSettings> yooKassaSettings)
    {
        _users = users;
        _profiles = profiles;
        _yooKassa = yooKassa;
        _settings = settings.Value;
        _yooKassaSettings = yooKassaSettings.Value;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();
        var profile = await _profiles.EnsureForUserAsync(user);

        ViewBag.PremiumPrice = _settings.PremiumPriceRub;
        ViewBag.BookingUrl = Url.Action("Index", "Book", new { slug = profile.BookingSlug }, Request.Scheme);
        ViewBag.YooKassaConfigured = _yooKassaSettings.IsConfigured;
        ViewBag.PayError = TempData["PayError"];
        ViewBag.PaySuccess = TempData["PaySuccess"];

        return View(profile.Subscription);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();
        var profile = await _profiles.EnsureForUserAsync(user);

        var returnUrl = Url.Action(nameof(Complete), null, null, Request.Scheme)!;
        var (ok, redirectUrl, error) = await _yooKassa.CreatePremiumPaymentAsync(profile.Id, returnUrl);

        if (!ok || string.IsNullOrWhiteSpace(redirectUrl))
        {
            TempData["PayError"] = error ?? "Payment failed";
            return RedirectToAction(nameof(Index));
        }

        return Redirect(redirectUrl);
    }

    [HttpGet]
    public async Task<IActionResult> Complete()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();
        var profile = await _profiles.EnsureForUserAsync(user);

        await _yooKassa.TryConfirmLatestPendingAsync(profile.Id);
        profile = await _profiles.EnsureForUserAsync(user);

        if (profile.Subscription?.IsPremiumActive == true)
            TempData["PaySuccess"] = true;
        else
            TempData["PayPending"] = true;

        return RedirectToAction(nameof(Index));
    }
}
