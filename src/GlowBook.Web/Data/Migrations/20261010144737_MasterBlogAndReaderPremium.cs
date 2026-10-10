using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GlowBook.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class MasterBlogAndReaderPremium : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PremiumExpiresAt",
                table: "AspNetUsers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MasterArticles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MasterProfileId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Excerpt = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    CoverData = table.Column<byte[]>(type: "bytea", nullable: true),
                    CoverContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    IsPremiumOnly = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterArticles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MasterArticles_MasterProfiles_MasterProfileId",
                        column: x => x.MasterProfileId,
                        principalTable: "MasterProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPaymentOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    YooKassaPaymentId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AmountRub = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPaymentOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPaymentOrders_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MasterArticles_MasterProfileId_IsPublished_PublishedAt",
                table: "MasterArticles",
                columns: new[] { "MasterProfileId", "IsPublished", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MasterArticles_MasterProfileId_Slug",
                table: "MasterArticles",
                columns: new[] { "MasterProfileId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPaymentOrders_UserId",
                table: "UserPaymentOrders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPaymentOrders_YooKassaPaymentId",
                table: "UserPaymentOrders",
                column: "YooKassaPaymentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MasterArticles");

            migrationBuilder.DropTable(
                name: "UserPaymentOrders");

            migrationBuilder.DropColumn(
                name: "PremiumExpiresAt",
                table: "AspNetUsers");
        }
    }
}
