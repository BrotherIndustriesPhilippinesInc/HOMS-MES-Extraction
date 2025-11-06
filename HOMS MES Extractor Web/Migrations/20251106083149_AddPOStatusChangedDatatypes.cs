using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOMS_MES_Extractor_Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPOStatusChangedDatatypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First, cast text to integer using explicit SQL commands
            migrationBuilder.Sql("ALTER TABLE \"POStatus\" ALTER COLUMN \"ProducedQty\" TYPE integer USING \"ProducedQty\"::integer;");
            migrationBuilder.Sql("ALTER TABLE \"POStatus\" ALTER COLUMN \"PlannedQty\" TYPE integer USING \"PlannedQty\"::integer;");
            migrationBuilder.Sql("ALTER TABLE \"POStatus\" ALTER COLUMN \"FinishedQty\" TYPE integer USING \"FinishedQty\"::integer;");
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"POStatus\" ALTER COLUMN \"ProducedQty\" TYPE text;");
            migrationBuilder.Sql("ALTER TABLE \"POStatus\" ALTER COLUMN \"PlannedQty\" TYPE text;");
            migrationBuilder.Sql("ALTER TABLE \"POStatus\" ALTER COLUMN \"FinishedQty\" TYPE text;");
        }

    }
}
