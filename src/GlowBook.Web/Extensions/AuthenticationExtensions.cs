using System.Security.Claims;
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

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/auth/login";
            options.LogoutPath = "/auth/logout";
            options.AccessDeniedPath = "/auth/login";
        });

        return services;
    }
}
