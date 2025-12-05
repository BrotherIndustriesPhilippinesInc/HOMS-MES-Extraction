using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMS_MES_Extractor_Web.Migrations
{
    /// <inheritdoc />
    public partial class ModifyPOReasonsForDateTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ForDateTime",
                table: "POMESReasons",
                newName: "ActualDateTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ActualDateTime",
                table: "POMESReasons",
                newName: "ForDateTime");
        }
    }
}
