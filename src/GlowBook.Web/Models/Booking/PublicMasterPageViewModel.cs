using GlowBook.Web.Models.Blog;
using GlowBook.Web.Models.Entities;

namespace GlowBook.Web.Models.Booking;

public class PublicMasterPageViewModel
{
    public PublicBookingForm Booking { get; set; } = new();

    public PublicReviewForm Review { get; set; } = new();

    public List<PortfolioCard> Portfolio { get; set; } = [];

    public List<MasterPromo> Promos { get; set; } = [];

    public List<MasterReview> Reviews { get; set; } = [];

    public List<PublicArticleCard> Articles { get; set; } = [];

    public double? AverageRating { get; set; }

    public int ReviewCount { get; set; }

    public string? Location { get; set; }

    public string? MapEmbedUrl { get; set; }

    public string? MapOpenUrl { get; set; }

    public string AccentColor { get; set; } = "#8b3db8";

    public string? ReviewFlash { get; set; }

    public string Slug { get; set; } = string.Empty;

    public class PortfolioCard
    {
        public int Id { get; set; }
        public string? Caption { get; set; }
    }
}
