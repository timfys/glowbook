using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlowBook.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterAvatars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AvatarUpdatedAt",
                table: "MasterProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasAvatar",
                table: "MasterProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MasterAvatars",
                columns: table => new
                {
                    MasterProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterAvatars", x => x.MasterProfileId);
                    table.ForeignKey(
                        name: "FK_MasterAvatars_MasterProfiles_MasterProfileId",
                        column: x => x.MasterProfileId,
                        principalTable: "MasterProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MasterAvatars");

            migrationBuilder.DropColumn(
                name: "AvatarUpdatedAt",
                table: "MasterProfiles");

            migrationBuilder.DropColumn(
                name: "HasAvatar",
                table: "MasterProfiles");
        }
    }
}
