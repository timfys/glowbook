using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using GlowBook.Web.Models;
using GlowBook.Web.Models.Entities;

namespace GlowBook.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<MasterProfile> MasterProfiles => Set<MasterProfile>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<WorkingHour> WorkingHours => Set<WorkingHour>();
    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();
    public DbSet<MasterAvatar> MasterAvatars => Set<MasterAvatar>();
    public DbSet<TreatmentRecord> TreatmentRecords => Set<TreatmentRecord>();
    public DbSet<ClientPhoto> ClientPhotos => Set<ClientPhoto>();
    public DbSet<HomeCarePrescription> HomeCarePrescriptions => Set<HomeCarePrescription>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<MasterProfile>(e =>
        {
            e.HasIndex(x => x.BookingSlug).IsUnique();
            e.HasIndex(x => x.UserId).IsUnique();
            e.Property(x => x.BusinessName).HasMaxLength(200);
            e.Property(x => x.BookingSlug).HasMaxLength(100);
            e.HasOne(x => x.User).WithOne(x => x.MasterProfile).HasForeignKey<MasterProfile>(x => x.UserId);
        });

        builder.Entity<MasterAvatar>(e =>
        {
            e.HasKey(x => x.MasterProfileId);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.HasOne(x => x.MasterProfile)
                .WithOne(x => x.Avatar)
                .HasForeignKey<MasterAvatar>(x => x.MasterProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Client>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Allergies).HasMaxLength(1000);
            e.Property(x => x.SkinConcerns).HasMaxLength(1000);
            e.HasIndex(x => new { x.MasterProfileId, x.Phone });
            e.HasIndex(x => x.LinkedUserId);
            e.HasOne(x => x.LinkedUser)
                .WithMany()
                .HasForeignKey(x => x.LinkedUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Service>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Price).HasPrecision(10, 2);
        });

        builder.Entity<Appointment>(e =>
        {
            e.HasIndex(x => new { x.MasterProfileId, x.StartsAt });
            e.HasOne(x => x.Client).WithMany(x => x.Appointments).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Service).WithMany(x => x.Appointments).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TreatmentRecord>(e =>
        {
            e.Property(x => x.ProcedureName).HasMaxLength(200);
            e.Property(x => x.ProductsUsed).HasMaxLength(1000);
            e.Property(x => x.EquipmentUsed).HasMaxLength(500);
            e.Property(x => x.Price).HasPrecision(10, 2);
            e.HasIndex(x => new { x.ClientId, x.PerformedAt });
            e.HasOne(x => x.Client).WithMany(x => x.TreatmentRecords).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Service).WithMany().OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Appointment).WithMany().OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ClientPhoto>(e =>
        {
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.Caption).HasMaxLength(300);
            e.HasIndex(x => new { x.ClientId, x.TakenAt });
            e.HasOne(x => x.Client).WithMany(x => x.Photos).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<HomeCarePrescription>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Products).HasMaxLength(1000);
            e.HasIndex(x => new { x.ClientId, x.PrescribedAt });
            e.HasOne(x => x.Client).WithMany(x => x.HomeCarePrescriptions).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Subscription>(e =>
        {
            e.Property(x => x.PriceRub).HasPrecision(10, 2);
            e.HasIndex(x => x.MasterProfileId).IsUnique();
        });

        builder.Entity<WorkingHour>(e =>
        {
            e.HasIndex(x => new { x.MasterProfileId, x.DayOfWeek }).IsUnique();
        });

        builder.Entity<PaymentOrder>(e =>
        {
            e.HasIndex(x => x.YooKassaPaymentId).IsUnique();
            e.Property(x => x.YooKassaPaymentId).HasMaxLength(64);
            e.Property(x => x.Status).HasMaxLength(32);
            e.Property(x => x.AmountRub).HasPrecision(10, 2);
            e.HasOne(x => x.MasterProfile).WithMany().HasForeignKey(x => x.MasterProfileId);
        });
    }
}
