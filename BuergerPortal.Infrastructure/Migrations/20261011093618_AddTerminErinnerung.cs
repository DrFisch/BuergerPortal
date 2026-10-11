using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuergerPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTerminErinnerung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ErinnerungGesendetUtc",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GebuchtUtc",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostkorbHandle",
                table: "Appointments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ErinnerungGesendetUtc_StartUtc",
                table: "Appointments",
                columns: new[] { "ErinnerungGesendetUtc", "StartUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_ErinnerungGesendetUtc_StartUtc",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ErinnerungGesendetUtc",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "GebuchtUtc",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PostkorbHandle",
                table: "Appointments");
        }
    }
}
