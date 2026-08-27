using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlowBook.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClientChatRealtime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentContentType",
                table: "ClientMessages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "AttachmentData",
                table: "ClientMessages",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentFileName",
                table: "ClientMessages",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentContentType",
                table: "ClientMessages");

            migrationBuilder.DropColumn(
                name: "AttachmentData",
                table: "ClientMessages");

            migrationBuilder.DropColumn(
                name: "AttachmentFileName",
                table: "ClientMessages");
        }
    }
}
