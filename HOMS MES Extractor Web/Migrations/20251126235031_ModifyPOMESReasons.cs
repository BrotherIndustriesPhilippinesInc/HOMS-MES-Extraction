using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMS_MES_Extractor_Web.Migrations
{
    /// <inheritdoc />
    public partial class ModifyPOMESReasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ForDateTime",
                table: "POMESReasons",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForDateTime",
                table: "POMESReasons");
        }
    }
}
