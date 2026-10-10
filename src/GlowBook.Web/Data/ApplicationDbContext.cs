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
    public DbSet<ClientAvatar> ClientAvatars => Set<ClientAvatar>();
    public DbSet<ClientMessage> ClientMessages => Set<ClientMessage>();
    public DbSet<MasterPortfolioPhoto> MasterPortfolioPhotos => Set<MasterPortfolioPhoto>();
    public DbSet<MasterPromo> MasterPromos => Set<MasterPromo>();
    public DbSet<MasterReview> MasterReviews => Set<MasterReview>();
    public DbSet<MasterArticle> MasterArticles => Set<MasterArticle>();
    public DbSet<UserPaymentOrder> UserPaymentOrders => Set<UserPaymentOrder>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<MasterProfile>(e =>
        {
            e.HasIndex(x => x.BookingSlug).IsUnique();
            e.HasIndex(x => x.UserId).IsUnique();
            e.Property(x => x.BusinessName).HasMaxLength(200);
            e.Property(x => x.BookingSlug).HasMaxLength(100);
            e.Property(x => x.PageAccentColor).HasMaxLength(7);
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
            e.Property(x => x.Color).HasMaxLength(7);
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

        builder.Entity<UserPaymentOrder>(e =>
        {
            e.HasIndex(x => x.YooKassaPaymentId).IsUnique();
            e.Property(x => x.YooKassaPaymentId).HasMaxLength(64);
            e.Property(x => x.Status).HasMaxLength(32);
            e.Property(x => x.UserId).HasMaxLength(450);
            e.Property(x => x.AmountRub).HasPrecision(10, 2);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ClientAvatar>(e =>
        {
            e.HasKey(x => x.UserId);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.HasOne(x => x.User)
                .WithOne()
                .HasForeignKey<ClientAvatar>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ClientMessage>(e =>
        {
            e.Property(x => x.Body).HasMaxLength(2000);
            e.Property(x => x.AttachmentFileName).HasMaxLength(255);
            e.Property(x => x.AttachmentContentType).HasMaxLength(100);
            e.HasIndex(x => new { x.ClientId, x.CreatedAt });
            e.HasOne(x => x.Client)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.SenderUser)
                .WithMany()
                .HasForeignKey(x => x.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MasterPortfolioPhoto>(e =>
        {
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.Caption).HasMaxLength(200);
            e.HasIndex(x => new { x.MasterProfileId, x.SortOrder });
            e.HasOne(x => x.MasterProfile)
                .WithMany(x => x.PortfolioPhotos)
                .HasForeignKey(x => x.MasterProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MasterPromo>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(120);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Badge).HasMaxLength(40);
            e.HasIndex(x => new { x.MasterProfileId, x.IsActive });
            e.HasOne(x => x.MasterProfile)
                .WithMany(x => x.Promos)
                .HasForeignKey(x => x.MasterProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MasterReview>(e =>
        {
            e.Property(x => x.AuthorName).HasMaxLength(80);
            e.Property(x => x.AuthorPhone).HasMaxLength(30);
            e.Property(x => x.Text).HasMaxLength(800);
            e.HasIndex(x => new { x.MasterProfileId, x.IsPublished, x.CreatedAt });
            e.HasOne(x => x.MasterProfile)
                .WithMany(x => x.Reviews)
                .HasForeignKey(x => x.MasterProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<MasterArticle>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(160);
            e.Property(x => x.Slug).HasMaxLength(120);
            e.Property(x => x.Excerpt).HasMaxLength(320);
            e.Property(x => x.CoverContentType).HasMaxLength(100);
            e.HasIndex(x => new { x.MasterProfileId, x.Slug }).IsUnique();
            e.HasIndex(x => new { x.MasterProfileId, x.IsPublished, x.PublishedAt });
            e.HasOne(x => x.MasterProfile)
                .WithMany(x => x.Articles)
                .HasForeignKey(x => x.MasterProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
