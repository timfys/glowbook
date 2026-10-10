using GlowBook.Web.Configuration;
using GlowBook.Web.Models;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GlowBook.Web.Controllers;

/// <summary>Premium для чтения материалов (клиенты и мастера без мастер-подписки).</summary>
[Authorize]
[Route("premium")]
public class PremiumController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly YooKassaService _yooKassa;
    private readonly PremiumAccessService _premium;
    private readonly GlowBookSettings _settings;
    private readonly YooKassaSettings _yooKassaSettings;

    public PremiumController(
        UserManager<ApplicationUser> users,
        YooKassaService yooKassa,
        PremiumAccessService premium,
        IOptions<GlowBookSettings> settings,
        IOptions<YooKassaSettings> yooKassaSettings)
    {
        _users = users;
        _yooKassa = yooKassa;
        _premium = premium;
        _settings = settings.Value;
        _yooKassaSettings = yooKassaSettings.Value;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? returnUrl)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        ViewBag.PremiumPrice = _settings.PremiumPriceRub;
        ViewBag.HasPremium = await _premium.HasPremiumAsync(user);
        ViewBag.PremiumUntil = user.PremiumExpiresAt;
        ViewBag.YooKassaConfigured = _yooKassaSettings.IsConfigured;
        ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? null : returnUrl;
        ViewBag.PayError = TempData["PayError"];
        ViewBag.PaySuccess = TempData["PaySuccess"];
        return View();
    }

    [HttpPost("pay")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(string? returnUrl)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        if (await _premium.HasPremiumAsync(user))
        {
            TempData["PaySuccess"] = true;
            return RedirectToAction(nameof(Index), new { returnUrl });
        }

        var completeUrl = Url.Action(nameof(Complete), null,
            new { returnUrl }, Request.Scheme)!;
        var (ok, redirectUrl, error) = await _yooKassa.CreateUserPremiumPaymentAsync(user.Id, completeUrl);
        if (!ok || string.IsNullOrWhiteSpace(redirectUrl))
        {
            TempData["PayError"] = error ?? "Не удалось создать оплату";
            return RedirectToAction(nameof(Index), new { returnUrl });
        }

        return Redirect(redirectUrl);
    }

    [HttpGet("complete")]
    public async Task<IActionResult> Complete(string? returnUrl)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        await _yooKassa.TryConfirmLatestUserPendingAsync(user.Id);
        user = await _users.GetUserAsync(User);

        if (user != null && await _premium.HasPremiumAsync(user))
            TempData["PaySuccess"] = true;
        else
            TempData["PayPending"] = true;

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }
}
