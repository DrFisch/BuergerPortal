using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuergerPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMeldebescheinigung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeldebescheinigungAntraege",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Vorname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Nachname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Geburtsdatum = table.Column<DateOnly>(type: "date", nullable: false),
                    Geburtsort = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Geburtsname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Strasse = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PLZ = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Ort = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Telefon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Art = table.Column<int>(type: "int", nullable: false),
                    Zweck = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeldebescheinigungAntraege", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeldebescheinigungAntraege_Antraege_Id",
                        column: x => x.Id,
                        principalTable: "Antraege",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeldebescheinigungAntraege");
        }
    }
}
