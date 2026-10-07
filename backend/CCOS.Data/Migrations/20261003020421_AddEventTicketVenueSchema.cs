using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CCOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventTicketVenueSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EventId",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductType",
                table: "Products",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Item");

            migrationBuilder.CreateTable(
                name: "Venues",
                columns: table => new
                {
                    VenueId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venues", x => x.VenueId);
                    table.CheckConstraint("CK_Venues_Capacity", "[Capacity] > 0");
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    EventId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VenueId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    TicketCapacity = table.Column<int>(type: "int", nullable: false),
                    TicketsSold = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MembersOnly = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.EventId);
                    table.CheckConstraint("CK_Events_TicketCapacity", "[TicketCapacity] > 0");
                    table.CheckConstraint("CK_Events_TicketsSold", "[TicketsSold] >= 0 AND [TicketsSold] <= [TicketCapacity]");
                    table.CheckConstraint("CK_Events_Times", "[EndsAtUtc] > [StartsAtUtc]");
                    table.ForeignKey(
                        name: "FK_Events_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "VenueId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VenueAddresses",
                columns: table => new
                {
                    VenueAddressId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VenueId = table.Column<int>(type: "int", nullable: false),
                    AddressLine1 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AddressLine2 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    State = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ZipCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VenueAddresses", x => x.VenueAddressId);
                    table.ForeignKey(
                        name: "FK_VenueAddresses_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "VenueId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketRegistrations",
                columns: table => new
                {
                    TicketRegistrationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    TicketTypeId = table.Column<int>(type: "int", nullable: false),
                    OrderLineId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "Purchased"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CanceledAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketRegistrations", x => x.TicketRegistrationId);
                    table.CheckConstraint("CK_TicketRegistrations_Status", "[Status] IN ('Purchased', 'Canceled')");
                    table.ForeignKey(
                        name: "FK_TicketRegistrations_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketRegistrations_OrderLines_OrderLineId",
                        column: x => x.OrderLineId,
                        principalTable: "OrderLines",
                        principalColumn: "OrderLineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketRegistrations_Products_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_EventId",
                table: "Products",
                column: "EventId",
                filter: "[EventId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_TicketEvent",
                table: "Products",
                sql: "(ProductType = 'Ticket' AND EventId IS NOT NULL) OR (ProductType = 'Item' AND EventId IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Events_StartsAtUtc",
                table: "Events",
                column: "StartsAtUtc",
                filter: "[IsActive] = 1")
                .Annotation("SqlServer:Include", new[] { "EndsAtUtc", "Name", "VenueId", "MembersOnly" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_VenueId_StartsAtUtc",
                table: "Events",
                columns: new[] { "VenueId", "StartsAtUtc" })
                .Annotation("SqlServer:Include", new[] { "EndsAtUtc", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketRegistrations_EventId_Status",
                table: "TicketRegistrations",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketRegistrations_OrderLineId",
                table: "TicketRegistrations",
                column: "OrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketRegistrations_TicketTypeId",
                table: "TicketRegistrations",
                column: "TicketTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_VenueAddresses_VenueId",
                table: "VenueAddresses",
                column: "VenueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Venues_Name",
                table: "Venues",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Events_EventId",
                table: "Products",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "EventId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Events_EventId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "TicketRegistrations");

            migrationBuilder.DropTable(
                name: "VenueAddresses");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "Venues");

            migrationBuilder.DropIndex(
                name: "IX_Products_EventId",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_TicketEvent",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "Products");
        }
    }
}
