using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CCOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderManagementLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders");

            migrationBuilder.Sql("UPDATE \"Orders\" SET \"Status\" = 'Picked up' WHERE \"Status\" = 'Fulfilled'");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Orders",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders",
                sql: "\"Status\" IN ('Pending', 'Paid', 'Picked up', 'Canceled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The old schema cannot represent Paid without losing its meaning, so refuse a lossy rollback.
            migrationBuilder.Sql(
                "DO $$ BEGIN "
                + "IF EXISTS (SELECT 1 FROM \"Orders\" WHERE \"Status\" = 'Paid') THEN "
                + "RAISE EXCEPTION 'Cannot roll back order lifecycle while Paid orders exist.'; "
                + "END IF; END $$;");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Orders");

            migrationBuilder.Sql("UPDATE \"Orders\" SET \"Status\" = 'Fulfilled' WHERE \"Status\" = 'Picked up'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders",
                sql: "\"Status\" IN ('Pending', 'Fulfilled', 'Canceled')");
        }
    }
}
