using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FieldModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Not: rowversion kolonlarına varsayılan değer verilemez; EF'in ürettiği defaultValue elle kaldırıldı.
            // SQL Server mevcut satırlara sürümü kendisi atar. vw_StockBalance (SCHEMABINDING) yeni kolonu
            // kullanmadığından ALTER TABLE ADD view'u bozmaz.
            migrationBuilder.CreateSequence(
                name: "FieldInvoiceNumbers");

            migrationBuilder.CreateSequence(
                name: "FieldOrderNumbers");

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "StockMovements",
                type: "rowversion",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "Products",
                type: "varchar(13)",
                unicode: false,
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Products",
                type: "rowversion",
                nullable: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Payments",
                type: "rowversion",
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "Orders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Orders",
                type: "rowversion",
                nullable: false);

            migrationBuilder.AddColumn<int>(
                name: "ShortQuantity",
                table: "OrderLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Invoices",
                type: "rowversion",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Customers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Customers",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Customers",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Customers",
                type: "rowversion",
                nullable: false);

            migrationBuilder.AddColumn<int>(
                name: "DriverId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    InstallationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AppVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PushToken = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    City = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FieldTombstones",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Entity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    SalesRepresentativeId = table.Column<int>(type: "int", nullable: true),
                    DriverId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldTombstones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "LocationPings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    Accuracy = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationPings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Visits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesRepresentativeId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    PlannedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CheckInAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckOutAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckInLatitude = table.Column<double>(type: "float", nullable: true),
                    CheckInLongitude = table.Column<double>(type: "float", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Visits_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Visits_SalesRepresentatives_SalesRepresentativeId",
                        column: x => x.SalesRepresentativeId,
                        principalTable: "SalesRepresentatives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Deliveries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    SalesRepresentativeId = table.Column<int>(type: "int", nullable: false),
                    PlannedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<byte>(type: "tinyint", nullable: true),
                    RecipientName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    ProofPhotoPath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    SignaturePath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deliveries_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Deliveries_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_Version",
                table: "StockMovements",
                column: "Version");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode",
                unique: true,
                filter: "[Barcode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Version",
                table: "Products",
                column: "Version");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Version",
                table: "Payments",
                column: "Version");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ClientRequestId",
                table: "Orders",
                column: "ClientRequestId",
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Version",
                table: "Orders",
                column: "Version");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Version",
                table: "Invoices",
                column: "Version");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Version",
                table: "Customers",
                column: "Version");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DriverId",
                table: "AspNetUsers",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_DriverId_PlannedDate_Sequence",
                table: "Deliveries",
                columns: new[] { "DriverId", "PlannedDate", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_OrderId",
                table: "Deliveries",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_SalesRepresentativeId",
                table: "Deliveries",
                column: "SalesRepresentativeId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_Version",
                table: "Deliveries",
                column: "Version");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_UserId_InstallationId",
                table: "Devices",
                columns: new[] { "UserId", "InstallationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_Email",
                table: "Drivers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FieldTombstones_DriverId",
                table: "FieldTombstones",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldTombstones_SalesRepresentativeId",
                table: "FieldTombstones",
                column: "SalesRepresentativeId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldTombstones_Version",
                table: "FieldTombstones",
                column: "Version");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_CreatedAt",
                table: "IdempotencyRecords",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LocationPings_ClientId",
                table: "LocationPings",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocationPings_UserId_RecordedAt",
                table: "LocationPings",
                columns: new[] { "UserId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Visits_CustomerId",
                table: "Visits",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_SalesRepresentativeId_PlannedDate_Sequence",
                table: "Visits",
                columns: new[] { "SalesRepresentativeId", "PlannedDate", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Visits_Version",
                table: "Visits",
                column: "Version");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Deliveries");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "FieldTombstones");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "LocationPings");

            migrationBuilder.DropTable(
                name: "Visits");

            migrationBuilder.DropTable(
                name: "Drivers");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_Version",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_Products_Barcode",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Version",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Version",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ClientRequestId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Version",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_Version",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Version",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_DriverId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShortQuantity",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DriverId",
                table: "AspNetUsers");

            migrationBuilder.DropSequence(
                name: "FieldInvoiceNumbers");

            migrationBuilder.DropSequence(
                name: "FieldOrderNumbers");
        }
    }
}
