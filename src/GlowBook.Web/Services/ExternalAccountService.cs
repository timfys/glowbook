using System.Security.Claims;
using GlowBook.Web.Configuration;
using GlowBook.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GlowBook.Web.Services;

public class ExternalAccountService
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly MasterProfileService _profiles;

    public ExternalAccountService(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        MasterProfileService profiles)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _profiles = profiles;
    }

    public async Task<IActionResult> SignInFromExternalLoginAsync(
        Controller controller,
        ExternalLoginInfo loginInfo,
        string? returnUrl)
    {
        var signInResult = await _signInManager.ExternalLoginSignInAsync(
            loginInfo.LoginProvider,
            loginInfo.ProviderKey,
            isPersistent: false);

        if (signInResult.Succeeded)
        {
            var existing = await _userManager.FindByLoginAsync(loginInfo.LoginProvider, loginInfo.ProviderKey);
            if (existing != null)
                await _profiles.EnsureForUserAsync(existing);

            return controller.LocalRedirect(NormalizeReturnUrl(returnUrl));
        }

        var email = loginInfo.Principal.FindFirstValue(ClaimTypes.Email);
        var given = loginInfo.Principal.FindFirstValue(ClaimTypes.GivenName);
        var surname = loginInfo.Principal.FindFirstValue(ClaimTypes.Surname);
        var composed = $"{given} {surname}".Trim();
        var name = loginInfo.Principal.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(composed))
            name = composed;

        ApplicationUser? user = null;
        if (!string.IsNullOrWhiteSpace(email))
            user = await _userManager.FindByEmailAsync(email);

        user ??= new ApplicationUser
        {
            UserName = email ?? $"{loginInfo.LoginProvider.ToLowerInvariant()}_{loginInfo.ProviderKey}",
            Email = email,
            EmailConfirmed = !string.IsNullOrWhiteSpace(email),
            DisplayName = name
        };

        if (string.IsNullOrEmpty(user.Id))
        {
            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                controller.ModelState.AddModelError(string.Empty, string.Join("; ", createResult.Errors.Select(e => e.Description)));
                return controller.RedirectToAction("Login", "Auth");
            }
        }

        var loginResult = await _userManager.AddLoginAsync(user, loginInfo);
        if (!loginResult.Succeeded && !loginResult.Errors.Any(e => e.Code == "LoginAlreadyAssociated"))
        {
            controller.ModelState.AddModelError(string.Empty, string.Join("; ", loginResult.Errors.Select(e => e.Description)));
            return controller.RedirectToAction("Login", "Auth");
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        await _profiles.EnsureForUserAsync(user);
        return controller.LocalRedirect(NormalizeReturnUrl(returnUrl));
    }

    public async Task<IActionResult> SignInFromTelegramAsync(
        Controller controller,
        string providerKey,
        string? displayName,
        string? returnUrl)
    {
        var signInResult = await _signInManager.ExternalLoginSignInAsync(
            AuthProviders.Telegram,
            providerKey,
            isPersistent: false);

        if (signInResult.Succeeded)
        {
            var existing = await _userManager.FindByLoginAsync(AuthProviders.Telegram, providerKey);
            if (existing != null)
                await _profiles.EnsureForUserAsync(existing);

            return controller.LocalRedirect(NormalizeReturnUrl(returnUrl));
        }

        var userName = $"tg_{providerKey}";
        var user = await _userManager.FindByNameAsync(userName) ?? new ApplicationUser
        {
            UserName = userName,
            DisplayName = displayName ?? userName,
            EmailConfirmed = false
        };

        if (string.IsNullOrEmpty(user.Id))
        {
            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
                return controller.RedirectToAction("Login", "Auth", new { error = "telegram_create_failed" });
        }

        var loginInfo = new UserLoginInfo(AuthProviders.Telegram, providerKey, AuthProviders.Telegram);
        var addLogin = await _userManager.AddLoginAsync(user, loginInfo);
        if (!addLogin.Succeeded && !addLogin.Errors.Any(e => e.Code == "LoginAlreadyAssociated"))
            return controller.RedirectToAction("Login", "Auth", new { error = "telegram_link_failed" });

        await _signInManager.SignInAsync(user, isPersistent: false);
        await _profiles.EnsureForUserAsync(user);
        return controller.LocalRedirect(NormalizeReturnUrl(returnUrl));
    }

    private static string NormalizeReturnUrl(string? returnUrl) =>
        string.IsNullOrWhiteSpace(returnUrl) ? "/Dashboard" : returnUrl;
}
