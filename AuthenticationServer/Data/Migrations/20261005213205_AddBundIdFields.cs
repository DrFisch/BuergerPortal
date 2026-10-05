using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthenticationServer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBundIdFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bpk2",
                table: "AspNetUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedViaBundIdUtc",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginUtc",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostkorbHandle",
                table: "AspNetUsers",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrustLevel",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Bpk2",
                table: "AspNetUsers",
                column: "Bpk2",
                unique: true,
                filter: "[Bpk2] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_Bpk2",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Bpk2",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CreatedViaBundIdUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastLoginUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PostkorbHandle",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TrustLevel",
                table: "AspNetUsers");
        }
    }
}
