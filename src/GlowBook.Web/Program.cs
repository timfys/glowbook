using System.Globalization;
using GlowBook.Web.Configuration;
using GlowBook.Web.Data;
using GlowBook.Web.Extensions;
using GlowBook.Web.Models;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var ru = new CultureInfo("ru-RU");
CultureInfo.DefaultThreadCurrentCulture = ru;
CultureInfo.DefaultThreadCurrentUICulture = ru;

// SQLite-era DateTimes are Unspecified; allow them on timestamptz without forcing UTC rewrite.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

var dataDir = ResolveDataDirectory(builder);
Directory.CreateDirectory(dataDir);

var db = DatabaseConnectionResolver.Resolve(builder.Configuration, dataDir);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (db.Provider == DatabaseProviderKind.Postgres)
        options.UseNpgsql(db.ConnectionString);
    else
        options.UseSqlite(db.ConnectionString);
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 3 * 1024 * 1024;
});

builder.Services.Configure<GlowBookSettings>(builder.Configuration.GetSection(GlowBookSettings.SectionName));
builder.Services.Configure<YooKassaSettings>(builder.Configuration.GetSection(YooKassaSettings.SectionName));

builder.Services.AddGlowBookAuthentication(builder.Configuration);

builder.Services.AddScoped<MasterProfileService>();
builder.Services.AddScoped<ExternalAccountService>();
builder.Services.AddScoped<TelegramAuthService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddHttpClient<YooKassaService>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Railway / reverse proxies; trust edge headers so OAuth redirect_uri stays https://
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();
app.Logger.LogInformation("Database provider: {Provider}; connection: {ConnectionString}",
    db.Provider,
    DatabaseConnectionResolver.Redact(db.ConnectionString));

await DbInitializer.InitializeAsync(app.Services, dataDir);

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();

static string ResolveDataDirectory(WebApplicationBuilder builder)
{
    var configured = builder.Configuration["DATA_DIR"];
    if (!string.IsNullOrWhiteSpace(configured))
        return configured;

    var railwayVolume = Environment.GetEnvironmentVariable("RAILWAY_VOLUME_MOUNT_PATH");
    if (!string.IsNullOrWhiteSpace(railwayVolume))
        return railwayVolume;

    if (Directory.Exists("/data"))
        return "/data";

    return Path.Combine(builder.Environment.ContentRootPath, "Data");
}
