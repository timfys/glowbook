using GlowBook.Web.Configuration;
using GlowBook.Web.Models;
using GlowBook.Web.Models.Auth;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GlowBook.Web.Controllers;

[Route("auth")]
public class AuthController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ExternalAccountService _externalAccounts;
    private readonly TelegramAuthService _telegramAuth;
    private readonly MasterProfileService _profiles;
    private readonly ExternalAuthSettings _authSettings;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ExternalAccountService externalAccounts,
        TelegramAuthService telegramAuth,
        MasterProfileService profiles,
        IOptions<ExternalAuthSettings> authSettings)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _externalAccounts = externalAccounts;
        _telegramAuth = telegramAuth;
        _profiles = profiles;
        _authSettings = authSettings.Value;
    }

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null, string? error = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        ViewData["ReturnUrl"] = returnUrl;
        ViewData["Error"] = error;
        ViewBag.Providers = BuildProvidersModel();
        return View(new LoginViewModel());
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewBag.Providers = BuildProvidersModel();
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
                await _profiles.EnsureForUserAsync(user);
            return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/Dashboard" : returnUrl);
        }

        ModelState.AddModelError(string.Empty, "Неверный email или пароль");
        return View(model);
    }

    [HttpGet("register")]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        ViewData["ReturnUrl"] = returnUrl;
        ViewBag.Providers = BuildProvidersModel();
        return View(new RegisterViewModel());
    }

    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        ViewBag.Providers = BuildProvidersModel();
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.DisplayName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
                ModelState.AddModelError(string.Empty, err.Description);
            return View(model);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        await _profiles.EnsureForUserAsync(user);
        return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/Dashboard" : returnUrl);
    }

    [HttpPost("external")]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), values: new { returnUrl })!;
        var props = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(props, provider);
    }

    [HttpGet("external-callback")]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (!string.IsNullOrWhiteSpace(remoteError))
            return RedirectToAction(nameof(Login), new { error = remoteError, returnUrl });

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
            return RedirectToAction(nameof(Login), new { error = "external_info_missing", returnUrl });

        return await _externalAccounts.SignInFromExternalLoginAsync(this, info, returnUrl);
    }

    [HttpGet("telegram/callback")]
    public async Task<IActionResult> TelegramCallback(string? returnUrl = null)
    {
        var fields = Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.Ordinal);
        if (!_telegramAuth.TryValidate(fields, out var error))
            return RedirectToAction(nameof(Login), new { error, returnUrl });

        if (!fields.TryGetValue("id", out var id) || string.IsNullOrWhiteSpace(id))
            return RedirectToAction(nameof(Login), new { error = "telegram_id_missing", returnUrl });

        var firstName = fields.GetValueOrDefault("first_name");
        var lastName = fields.GetValueOrDefault("last_name");
        var displayName = $"{firstName} {lastName}".Trim();
        if (string.IsNullOrWhiteSpace(displayName) && fields.TryGetValue("username", out var username))
            displayName = "@" + username;

        return await _externalAccounts.SignInFromTelegramAsync(this, id, displayName, returnUrl);
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    private AuthProvidersViewModel BuildProvidersModel() => new()
    {
        Google = _authSettings.Google.IsConfigured,
        MailRu = _authSettings.MailRu.IsConfigured,
        Telegram = _authSettings.Telegram.IsConfigured,
        TelegramBotUsername = _authSettings.Telegram.BotUsername
    };
}
