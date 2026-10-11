using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthenticationServer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBundIdAnmeldungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BundIdAnmeldungen",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ZeitpunktUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Niveau = table.Column<int>(type: "int", nullable: false),
                    Mittel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BundIdAnmeldungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BundIdAnmeldungen_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BundIdAnmeldungen_UserId_ZeitpunktUtc",
                table: "BundIdAnmeldungen",
                columns: new[] { "UserId", "ZeitpunktUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BundIdAnmeldungen");
        }
    }
}
