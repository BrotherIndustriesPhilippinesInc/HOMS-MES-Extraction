using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMS_MES_Extractor_Web.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePOStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActualStart",
                table: "POStatus",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "POStatus",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualStart",
                table: "POStatus");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "POStatus");
        }
    }
}
