using GlowBook.Web.Data;
using GlowBook.Web.Models.Entities;
using GlowBook.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace GlowBook.Web.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }
}
