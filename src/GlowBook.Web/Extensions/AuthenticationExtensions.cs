using System.Security.Claims;
using AspNet.Security.OAuth.VkId;
using GlowBook.Web.Configuration;

namespace GlowBook.Web.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddGlowBookAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ExternalAuthSettings>(configuration.GetSection(ExternalAuthSettings.SectionName));

        var authOptions = configuration.GetSection(ExternalAuthSettings.SectionName).Get<ExternalAuthSettings>()
            ?? new ExternalAuthSettings();

        var authBuilder = services.AddAuthentication();

        if (authOptions.Google.IsConfigured)
        {
            authBuilder.AddGoogle(options =>
            {
                options.ClientId = authOptions.Google.ClientId!;
                options.ClientSecret = authOptions.Google.ClientSecret!;
                options.SaveTokens = true;
            });
        }

        if (authOptions.MailRu.IsConfigured)
        {
            authBuilder.AddOAuth(AuthProviders.MailRu, options =>
            {
                options.ClientId = authOptions.MailRu.ClientId!;
                options.ClientSecret = authOptions.MailRu.ClientSecret!;
                options.AuthorizationEndpoint = "https://oauth.mail.ru/login";
                options.TokenEndpoint = "https://oauth.mail.ru/token";
                options.UserInformationEndpoint = "https://oauth.mail.ru/userinfo";
                options.CallbackPath = "/signin-mailru";
                options.SaveTokens = true;
                options.Scope.Add("userinfo");
                options.Events.OnCreatingTicket = context =>
                {
                    if (context.User.TryGetProperty("id", out var id))
                        context.Identity?.AddClaim(new Claim(ClaimTypes.NameIdentifier, id.GetString() ?? string.Empty));
                    if (context.User.TryGetProperty("email", out var email))
                        context.Identity?.AddClaim(new Claim(ClaimTypes.Email, email.GetString() ?? string.Empty));
                    if (context.User.TryGetProperty("name", out var name))
                        context.Identity?.AddClaim(new Claim(ClaimTypes.Name, name.GetString() ?? string.Empty));
                    return Task.CompletedTask;
                };
            });
        }

        if (authOptions.VkId.IsConfigured)
        {
            authBuilder.AddVkId(AuthProviders.VkId, "VK ID", options =>
            {
                options.ClientId = authOptions.VkId.ClientId!;
                options.ClientSecret = authOptions.VkId.ClientSecret!;
                options.SaveTokens = true;
                options.Events.OnCreatingTicket = context =>
                {
                    var given = context.Identity?.FindFirst(ClaimTypes.GivenName)?.Value;
                    var surname = context.Identity?.FindFirst(ClaimTypes.Surname)?.Value;
                    var fullName = $"{given} {surname}".Trim();
                    if (!string.IsNullOrWhiteSpace(fullName)
                        && context.Identity?.FindFirst(ClaimTypes.Name) == null)
                    {
                        context.Identity?.AddClaim(new Claim(ClaimTypes.Name, fullName));
                    }

                    return Task.CompletedTask;
                };
            });
        }

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/auth/login";
            options.LogoutPath = "/auth/logout";
            options.AccessDeniedPath = "/auth/login";
        });

        return services;
    }
}
