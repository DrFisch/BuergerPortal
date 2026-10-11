using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuergerPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMangelFotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaengelFotos",
                columns: table => new
                {
                    MaengelmeldungId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Daten = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaengelFotos", x => x.MaengelmeldungId);
                    table.ForeignKey(
                        name: "FK_MaengelFotos_Maengelmeldungen_MaengelmeldungId",
                        column: x => x.MaengelmeldungId,
                        principalTable: "Maengelmeldungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Maengelmeldungen_ReporterUserId",
                table: "Maengelmeldungen",
                column: "ReporterUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaengelFotos");

            migrationBuilder.DropIndex(
                name: "IX_Maengelmeldungen_ReporterUserId",
                table: "Maengelmeldungen");
        }
    }
}
