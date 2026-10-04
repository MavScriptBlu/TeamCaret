using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CCOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductNameSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductNameSnapshot",
                table: "OrderLines",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductNameSnapshot",
                table: "OrderLines");
        }
    }
}
