using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMS_MES_Extractor_Web.Migrations
{
    /// <inheritdoc />
    public partial class AddComplianceRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ComplianceRate",
                table: "POStatus",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComplianceRate",
                table: "POStatus");
        }
    }
}
