using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuergerPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Antrag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Antraege",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicantUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Typ = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Antraege", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReisepassAntraege",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Vorname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Nachname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Geburtsdatum = table.Column<DateOnly>(type: "date", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Telefon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Express = table.Column<bool>(type: "bit", nullable: false),
                    AltpassVorhanden = table.Column<bool>(type: "bit", nullable: false),
                    Hinweis = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReisepassAntraege", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReisepassAntraege_Antraege_Id",
                        column: x => x.Id,
                        principalTable: "Antraege",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AntragId",
                table: "Appointments",
                column: "AntragId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_Service_StartUtc_EndUtc",
                table: "Appointments",
                columns: new[] { "Service", "StartUtc", "EndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Antraege_ApplicantUserId_Typ_Status",
                table: "Antraege",
                columns: new[] { "ApplicantUserId", "Typ", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Antraege_AntragId",
                table: "Appointments",
                column: "AntragId",
                principalTable: "Antraege",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Antraege_AntragId",
                table: "Appointments");

            migrationBuilder.DropTable(
                name: "ReisepassAntraege");

            migrationBuilder.DropTable(
                name: "Antraege");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_AntragId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_Service_StartUtc_EndUtc",
                table: "Appointments");
        }
    }
}
