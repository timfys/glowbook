using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlowBook.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalDossier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Allergies",
                table: "Clients",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SkinConcerns",
                table: "Clients",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClientPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    MasterProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Caption = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    TakenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientPhotos_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientPhotos_MasterProfiles_MasterProfileId",
                        column: x => x.MasterProfileId,
                        principalTable: "MasterProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HomeCarePrescriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    MasterProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Instructions = table.Column<string>(type: "TEXT", nullable: true),
                    Products = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    PrescribedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeCarePrescriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeCarePrescriptions_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HomeCarePrescriptions_MasterProfiles_MasterProfileId",
                        column: x => x.MasterProfileId,
                        principalTable: "MasterProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    MasterProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    AppointmentId = table.Column<int>(type: "INTEGER", nullable: true),
                    ServiceId = table.Column<int>(type: "INTEGER", nullable: true),
                    PerformedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProcedureName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ProductsUsed = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    EquipmentUsed = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    Price = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TreatmentRecords_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TreatmentRecords_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TreatmentRecords_MasterProfiles_MasterProfileId",
                        column: x => x.MasterProfileId,
                        principalTable: "MasterProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TreatmentRecords_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPhotos_ClientId_TakenAt",
                table: "ClientPhotos",
                columns: new[] { "ClientId", "TakenAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPhotos_MasterProfileId",
                table: "ClientPhotos",
                column: "MasterProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeCarePrescriptions_ClientId_PrescribedAt",
                table: "HomeCarePrescriptions",
                columns: new[] { "ClientId", "PrescribedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HomeCarePrescriptions_MasterProfileId",
                table: "HomeCarePrescriptions",
                column: "MasterProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRecords_AppointmentId",
                table: "TreatmentRecords",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRecords_ClientId_PerformedAt",
                table: "TreatmentRecords",
                columns: new[] { "ClientId", "PerformedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRecords_MasterProfileId",
                table: "TreatmentRecords",
                column: "MasterProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRecords_ServiceId",
                table: "TreatmentRecords",
                column: "ServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientPhotos");

            migrationBuilder.DropTable(
                name: "HomeCarePrescriptions");

            migrationBuilder.DropTable(
                name: "TreatmentRecords");

            migrationBuilder.DropColumn(
                name: "Allergies",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "SkinConcerns",
                table: "Clients");
        }
    }
}
