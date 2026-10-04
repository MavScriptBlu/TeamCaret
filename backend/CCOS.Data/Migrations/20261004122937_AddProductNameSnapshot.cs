using System;
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
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [OrderLines] SET [ProductNameSnapshot] = [Products].[Name] " +
                "FROM [OrderLines] INNER JOIN [Products] ON [OrderLines].[ProductId] = [Products].[ProductId]");

            migrationBuilder.AlterColumn<string>(
                name: "ProductNameSnapshot",
                table: "OrderLines",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);
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
