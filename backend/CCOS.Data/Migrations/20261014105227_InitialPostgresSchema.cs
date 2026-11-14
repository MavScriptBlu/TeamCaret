using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CCOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgresSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    OrderId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    TotalAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.OrderId);
                    table.CheckConstraint("CK_Orders_Status", "\"Status\" IN ('Pending', 'Fulfilled', 'Canceled')");
                    table.CheckConstraint("CK_Orders_TotalAmount", "\"TotalAmount\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Venues",
                columns: table => new
                {
                    VenueId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venues", x => x.VenueId);
                    table.CheckConstraint("CK_Venues_Capacity", "\"Capacity\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    EventId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VenueId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp(0) with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp(0) with time zone", nullable: false),
                    TicketCapacity = table.Column<int>(type: "integer", nullable: false),
                    TicketsSold = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MembersOnly = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.EventId);
                    table.CheckConstraint("CK_Events_TicketCapacity", "\"TicketCapacity\" > 0");
                    table.CheckConstraint("CK_Events_TicketsSold", "\"TicketsSold\" >= 0 AND \"TicketsSold\" <= \"TicketCapacity\"");
                    table.CheckConstraint("CK_Events_Times", "\"EndsAtUtc\" > \"StartsAtUtc\"");
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
                    VenueAddressId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VenueId = table.Column<int>(type: "integer", nullable: false),
                    AddressLine1 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AddressLine2 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ZipCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
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
                name: "Products",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    MemberPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    StockQuantity = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ProductType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "Item"),
                    EventId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.ProductId);
                    table.CheckConstraint("CK_Products_MemberPrice", "\"MemberPrice\" >= 0");
                    table.CheckConstraint("CK_Products_Price", "\"Price\" >= 0");
                    table.CheckConstraint("CK_Products_StockQuantity", "\"StockQuantity\" >= 0");
                    table.CheckConstraint("CK_Products_TicketEvent", "(\"ProductType\" = 'Ticket' AND \"EventId\" IS NOT NULL) OR (\"ProductType\" = 'Item' AND \"EventId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_Products_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderLines",
                columns: table => new
                {
                    OrderLineId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    ProductNameSnapshot = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLines", x => x.OrderLineId);
                    table.CheckConstraint("CK_OrderLines_Quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_OrderLines_UnitPrice", "\"UnitPrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_OrderLines_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketRegistrations",
                columns: table => new
                {
                    TicketRegistrationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    TicketTypeId = table.Column<int>(type: "integer", nullable: false),
                    OrderLineId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "Purchased"),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp(0) with time zone", nullable: false, defaultValueSql: "now()"),
                    CanceledAtUtc = table.Column<DateTime>(type: "timestamp(0) with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketRegistrations", x => x.TicketRegistrationId);
                    table.CheckConstraint("CK_TicketRegistrations_Status", "\"Status\" IN ('Purchased', 'Canceled')");
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
                name: "IX_Events_StartsAtUtc",
                table: "Events",
                column: "StartsAtUtc",
                filter: "\"IsActive\" = true")
                .Annotation("Npgsql:IndexInclude", new[] { "EndsAtUtc", "Name", "VenueId", "MembersOnly" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_VenueId_StartsAtUtc",
                table: "Events",
                columns: new[] { "VenueId", "StartsAtUtc" })
                .Annotation("Npgsql:IndexInclude", new[] { "EndsAtUtc", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_OrderId",
                table: "OrderLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_ProductId",
                table: "OrderLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_EventId",
                table: "Products",
                column: "EventId",
                filter: "\"EventId\" IS NOT NULL");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketRegistrations");

            migrationBuilder.DropTable(
                name: "VenueAddresses");

            migrationBuilder.DropTable(
                name: "OrderLines");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "Venues");
        }
    }
}
