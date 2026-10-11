using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuergerPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAntragStatusVerlauf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PostkorbHandle",
                table: "Antraege",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AntragStatusEreignisse",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AntragId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ZeitpunktUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntragStatusEreignisse", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AntragStatusEreignisse_Antraege_AntragId",
                        column: x => x.AntragId,
                        principalTable: "Antraege",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AntragStatusEreignisse_AntragId_ZeitpunktUtc",
                table: "AntragStatusEreignisse",
                columns: new[] { "AntragId", "ZeitpunktUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AntragStatusEreignisse");

            migrationBuilder.DropColumn(
                name: "PostkorbHandle",
                table: "Antraege");
        }
    }
}
