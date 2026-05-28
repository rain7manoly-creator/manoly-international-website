using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ManolyWarehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Supplier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OrderedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedArrivalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shelves",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Label = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Side = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shelves", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AreaZInventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    BundleCount = table.Column<int>(type: "integer", nullable: false),
                    UnitsPerBundle = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDispatched = table.Column<bool>(type: "boolean", nullable: false),
                    DispatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DispatchedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AreaZInventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AreaZInventory_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    BundleCount = table.Column<int>(type: "integer", nullable: false),
                    UnitsPerBundle = table.Column<int>(type: "integer", nullable: false),
                    ReceivedShelfId = table.Column<int>(type: "integer", nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: true),
                    GoesToAreaZ = table.Column<bool>(type: "boolean", nullable: false),
                    ShelvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ShelvedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItems_PurchaseOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItems_Shelves_ReceivedShelfId",
                        column: x => x.ReceivedShelfId,
                        principalTable: "Shelves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ShelfInventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShelfId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    BundleCount = table.Column<int>(type: "integer", nullable: false),
                    UnitsPerBundle = table.Column<int>(type: "integer", nullable: false),
                    OrderItemId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShelfInventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShelfInventory_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShelfInventory_PurchaseOrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "PurchaseOrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShelfInventory_Shelves_ShelfId",
                        column: x => x.ShelfId,
                        principalTable: "Shelves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryAdjustmentLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShelfInventoryId = table.Column<int>(type: "integer", nullable: false),
                    PreviousBundleCount = table.Column<int>(type: "integer", nullable: false),
                    NewBundleCount = table.Column<int>(type: "integer", nullable: false),
                    PreviousUnitsPerBundle = table.Column<int>(type: "integer", nullable: false),
                    NewUnitsPerBundle = table.Column<int>(type: "integer", nullable: false),
                    AdjustedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    AdjustedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryAdjustmentLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryAdjustmentLogs_ShelfInventory_ShelfInventoryId",
                        column: x => x.ShelfInventoryId,
                        principalTable: "ShelfInventory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Shelves",
                columns: new[] { "Id", "Code", "Label", "Number", "Side" },
                values: new object[,]
                {
                    { 1, "A1", "A", 1, "ABC" },
                    { 2, "B1", "B", 1, "ABC" },
                    { 3, "C1", "C", 1, "ABC" },
                    { 4, "A2", "A", 2, "ABC" },
                    { 5, "B2", "B", 2, "ABC" },
                    { 6, "C2", "C", 2, "ABC" },
                    { 7, "A3", "A", 3, "ABC" },
                    { 8, "B3", "B", 3, "ABC" },
                    { 9, "C3", "C", 3, "ABC" },
                    { 10, "A4", "A", 4, "ABC" },
                    { 11, "B4", "B", 4, "ABC" },
                    { 12, "C4", "C", 4, "ABC" },
                    { 13, "A5", "A", 5, "ABC" },
                    { 14, "B5", "B", 5, "ABC" },
                    { 15, "C5", "C", 5, "ABC" },
                    { 16, "A6", "A", 6, "ABC" },
                    { 17, "B6", "B", 6, "ABC" },
                    { 18, "C6", "C", 6, "ABC" },
                    { 19, "A7", "A", 7, "ABC" },
                    { 20, "B7", "B", 7, "ABC" },
                    { 21, "C7", "C", 7, "ABC" },
                    { 22, "A8", "A", 8, "ABC" },
                    { 23, "B8", "B", 8, "ABC" },
                    { 24, "C8", "C", 8, "ABC" },
                    { 25, "A9", "A", 9, "ABC" },
                    { 26, "B9", "B", 9, "ABC" },
                    { 27, "C9", "C", 9, "ABC" },
                    { 28, "A10", "A", 10, "ABC" },
                    { 29, "B10", "B", 10, "ABC" },
                    { 30, "C10", "C", 10, "ABC" },
                    { 31, "A11", "A", 11, "ABC" },
                    { 32, "B11", "B", 11, "ABC" },
                    { 33, "C11", "C", 11, "ABC" },
                    { 34, "A12", "A", 12, "ABC" },
                    { 35, "B12", "B", 12, "ABC" },
                    { 36, "C12", "C", 12, "ABC" },
                    { 37, "A13", "A", 13, "ABC" },
                    { 38, "B13", "B", 13, "ABC" },
                    { 39, "C13", "C", 13, "ABC" },
                    { 40, "A14", "A", 14, "ABC" },
                    { 41, "B14", "B", 14, "ABC" },
                    { 42, "C14", "C", 14, "ABC" },
                    { 43, "A15", "A", 15, "ABC" },
                    { 44, "B15", "B", 15, "ABC" },
                    { 45, "C15", "C", 15, "ABC" },
                    { 46, "A16", "A", 16, "ABC" },
                    { 47, "B16", "B", 16, "ABC" },
                    { 48, "C16", "C", 16, "ABC" },
                    { 49, "A17", "A", 17, "ABC" },
                    { 50, "B17", "B", 17, "ABC" },
                    { 51, "C17", "C", 17, "ABC" },
                    { 52, "A18", "A", 18, "ABC" },
                    { 53, "B18", "B", 18, "ABC" },
                    { 54, "C18", "C", 18, "ABC" },
                    { 55, "A19", "A", 19, "ABC" },
                    { 56, "B19", "B", 19, "ABC" },
                    { 57, "C19", "C", 19, "ABC" },
                    { 58, "A20", "A", 20, "ABC" },
                    { 59, "B20", "B", 20, "ABC" },
                    { 60, "C20", "C", 20, "ABC" },
                    { 61, "A21", "A", 21, "ABC" },
                    { 62, "B21", "B", 21, "ABC" },
                    { 63, "C21", "C", 21, "ABC" },
                    { 64, "A22", "A", 22, "ABC" },
                    { 65, "B22", "B", 22, "ABC" },
                    { 66, "C22", "C", 22, "ABC" },
                    { 67, "A23", "A", 23, "ABC" },
                    { 68, "B23", "B", 23, "ABC" },
                    { 69, "C23", "C", 23, "ABC" },
                    { 70, "A24", "A", 24, "ABC" },
                    { 71, "B24", "B", 24, "ABC" },
                    { 72, "C24", "C", 24, "ABC" },
                    { 73, "A25", "A", 25, "ABC" },
                    { 74, "B25", "B", 25, "ABC" },
                    { 75, "C25", "C", 25, "ABC" },
                    { 76, "A26", "A", 26, "ABC" },
                    { 77, "B26", "B", 26, "ABC" },
                    { 78, "C26", "C", 26, "ABC" },
                    { 79, "A27", "A", 27, "ABC" },
                    { 80, "B27", "B", 27, "ABC" },
                    { 81, "C27", "C", 27, "ABC" },
                    { 82, "A28", "A", 28, "ABC" },
                    { 83, "B28", "B", 28, "ABC" },
                    { 84, "C28", "C", 28, "ABC" },
                    { 85, "A29", "A", 29, "ABC" },
                    { 86, "B29", "B", 29, "ABC" },
                    { 87, "C29", "C", 29, "ABC" },
                    { 88, "A30", "A", 30, "ABC" },
                    { 89, "B30", "B", 30, "ABC" },
                    { 90, "C30", "C", 30, "ABC" },
                    { 91, "A31", "A", 31, "ABC" },
                    { 92, "B31", "B", 31, "ABC" },
                    { 93, "C31", "C", 31, "ABC" },
                    { 94, "A32", "A", 32, "ABC" },
                    { 95, "B32", "B", 32, "ABC" },
                    { 96, "C32", "C", 32, "ABC" },
                    { 97, "A33", "A", 33, "ABC" },
                    { 98, "B33", "B", 33, "ABC" },
                    { 99, "C33", "C", 33, "ABC" },
                    { 100, "A34", "A", 34, "ABC" },
                    { 101, "B34", "B", 34, "ABC" },
                    { 102, "C34", "C", 34, "ABC" },
                    { 103, "A35", "A", 35, "ABC" },
                    { 104, "B35", "B", 35, "ABC" },
                    { 105, "C35", "C", 35, "ABC" },
                    { 106, "A36", "A", 36, "ABC" },
                    { 107, "B36", "B", 36, "ABC" },
                    { 108, "C36", "C", 36, "ABC" },
                    { 109, "A37", "A", 37, "ABC" },
                    { 110, "B37", "B", 37, "ABC" },
                    { 111, "C37", "C", 37, "ABC" },
                    { 112, "A38", "A", 38, "ABC" },
                    { 113, "B38", "B", 38, "ABC" },
                    { 114, "C38", "C", 38, "ABC" },
                    { 115, "A39", "A", 39, "ABC" },
                    { 116, "B39", "B", 39, "ABC" },
                    { 117, "C39", "C", 39, "ABC" },
                    { 118, "A40", "A", 40, "ABC" },
                    { 119, "B40", "B", 40, "ABC" },
                    { 120, "C40", "C", 40, "ABC" },
                    { 121, "A41", "A", 41, "ABC" },
                    { 122, "B41", "B", 41, "ABC" },
                    { 123, "C41", "C", 41, "ABC" },
                    { 124, "A42", "A", 42, "ABC" },
                    { 125, "B42", "B", 42, "ABC" },
                    { 126, "C42", "C", 42, "ABC" },
                    { 127, "A43", "A", 43, "ABC" },
                    { 128, "B43", "B", 43, "ABC" },
                    { 129, "C43", "C", 43, "ABC" },
                    { 130, "A44", "A", 44, "ABC" },
                    { 131, "B44", "B", 44, "ABC" },
                    { 132, "C44", "C", 44, "ABC" },
                    { 133, "A45", "A", 45, "ABC" },
                    { 134, "B45", "B", 45, "ABC" },
                    { 135, "C45", "C", 45, "ABC" },
                    { 136, "A46", "A", 46, "ABC" },
                    { 137, "B46", "B", 46, "ABC" },
                    { 138, "C46", "C", 46, "ABC" },
                    { 139, "A47", "A", 47, "ABC" },
                    { 140, "B47", "B", 47, "ABC" },
                    { 141, "C47", "C", 47, "ABC" },
                    { 142, "A48", "A", 48, "ABC" },
                    { 143, "B48", "B", 48, "ABC" },
                    { 144, "C48", "C", 48, "ABC" },
                    { 145, "A49", "A", 49, "ABC" },
                    { 146, "B49", "B", 49, "ABC" },
                    { 147, "C49", "C", 49, "ABC" },
                    { 148, "A50", "A", 50, "ABC" },
                    { 149, "B50", "B", 50, "ABC" },
                    { 150, "C50", "C", 50, "ABC" },
                    { 151, "A51", "A", 51, "ABC" },
                    { 152, "B51", "B", 51, "ABC" },
                    { 153, "C51", "C", 51, "ABC" },
                    { 154, "A52", "A", 52, "ABC" },
                    { 155, "B52", "B", 52, "ABC" },
                    { 156, "C52", "C", 52, "ABC" },
                    { 157, "A53", "A", 53, "ABC" },
                    { 158, "B53", "B", 53, "ABC" },
                    { 159, "C53", "C", 53, "ABC" },
                    { 160, "A54", "A", 54, "ABC" },
                    { 161, "B54", "B", 54, "ABC" },
                    { 162, "C54", "C", 54, "ABC" },
                    { 163, "A55", "A", 55, "ABC" },
                    { 164, "B55", "B", 55, "ABC" },
                    { 165, "C55", "C", 55, "ABC" },
                    { 166, "A56", "A", 56, "ABC" },
                    { 167, "B56", "B", 56, "ABC" },
                    { 168, "C56", "C", 56, "ABC" },
                    { 169, "A57", "A", 57, "ABC" },
                    { 170, "B57", "B", 57, "ABC" },
                    { 171, "C57", "C", 57, "ABC" },
                    { 172, "A58", "A", 58, "ABC" },
                    { 173, "B58", "B", 58, "ABC" },
                    { 174, "C58", "C", 58, "ABC" },
                    { 175, "A59", "A", 59, "ABC" },
                    { 176, "B59", "B", 59, "ABC" },
                    { 177, "C59", "C", 59, "ABC" },
                    { 178, "A60", "A", 60, "ABC" },
                    { 179, "B60", "B", 60, "ABC" },
                    { 180, "C60", "C", 60, "ABC" },
                    { 181, "A61", "A", 61, "ABC" },
                    { 182, "B61", "B", 61, "ABC" },
                    { 183, "C61", "C", 61, "ABC" },
                    { 184, "A62", "A", 62, "ABC" },
                    { 185, "B62", "B", 62, "ABC" },
                    { 186, "C62", "C", 62, "ABC" },
                    { 187, "A63", "A", 63, "ABC" },
                    { 188, "B63", "B", 63, "ABC" },
                    { 189, "C63", "C", 63, "ABC" },
                    { 190, "A64", "A", 64, "ABC" },
                    { 191, "B64", "B", 64, "ABC" },
                    { 192, "C64", "C", 64, "ABC" },
                    { 193, "A65", "A", 65, "ABC" },
                    { 194, "B65", "B", 65, "ABC" },
                    { 195, "C65", "C", 65, "ABC" },
                    { 196, "A66", "A", 66, "ABC" },
                    { 197, "B66", "B", 66, "ABC" },
                    { 198, "C66", "C", 66, "ABC" },
                    { 199, "A67", "A", 67, "ABC" },
                    { 200, "B67", "B", 67, "ABC" },
                    { 201, "C67", "C", 67, "ABC" },
                    { 202, "A68", "A", 68, "ABC" },
                    { 203, "B68", "B", 68, "ABC" },
                    { 204, "C68", "C", 68, "ABC" },
                    { 205, "A69", "A", 69, "ABC" },
                    { 206, "B69", "B", 69, "ABC" },
                    { 207, "C69", "C", 69, "ABC" },
                    { 208, "D1", "D", 1, "DEF" },
                    { 209, "E1", "E", 1, "DEF" },
                    { 210, "F1", "F", 1, "DEF" },
                    { 211, "D2", "D", 2, "DEF" },
                    { 212, "E2", "E", 2, "DEF" },
                    { 213, "F2", "F", 2, "DEF" },
                    { 214, "D3", "D", 3, "DEF" },
                    { 215, "E3", "E", 3, "DEF" },
                    { 216, "F3", "F", 3, "DEF" },
                    { 217, "D4", "D", 4, "DEF" },
                    { 218, "E4", "E", 4, "DEF" },
                    { 219, "F4", "F", 4, "DEF" },
                    { 220, "D5", "D", 5, "DEF" },
                    { 221, "E5", "E", 5, "DEF" },
                    { 222, "F5", "F", 5, "DEF" },
                    { 223, "D6", "D", 6, "DEF" },
                    { 224, "E6", "E", 6, "DEF" },
                    { 225, "F6", "F", 6, "DEF" },
                    { 226, "D7", "D", 7, "DEF" },
                    { 227, "E7", "E", 7, "DEF" },
                    { 228, "F7", "F", 7, "DEF" },
                    { 229, "D8", "D", 8, "DEF" },
                    { 230, "E8", "E", 8, "DEF" },
                    { 231, "F8", "F", 8, "DEF" },
                    { 232, "D9", "D", 9, "DEF" },
                    { 233, "E9", "E", 9, "DEF" },
                    { 234, "F9", "F", 9, "DEF" },
                    { 235, "D10", "D", 10, "DEF" },
                    { 236, "E10", "E", 10, "DEF" },
                    { 237, "F10", "F", 10, "DEF" },
                    { 238, "D11", "D", 11, "DEF" },
                    { 239, "E11", "E", 11, "DEF" },
                    { 240, "F11", "F", 11, "DEF" },
                    { 241, "D12", "D", 12, "DEF" },
                    { 242, "E12", "E", 12, "DEF" },
                    { 243, "F12", "F", 12, "DEF" },
                    { 244, "D13", "D", 13, "DEF" },
                    { 245, "E13", "E", 13, "DEF" },
                    { 246, "F13", "F", 13, "DEF" },
                    { 247, "D14", "D", 14, "DEF" },
                    { 248, "E14", "E", 14, "DEF" },
                    { 249, "F14", "F", 14, "DEF" },
                    { 250, "D15", "D", 15, "DEF" },
                    { 251, "E15", "E", 15, "DEF" },
                    { 252, "F15", "F", 15, "DEF" },
                    { 253, "D16", "D", 16, "DEF" },
                    { 254, "E16", "E", 16, "DEF" },
                    { 255, "F16", "F", 16, "DEF" },
                    { 256, "D17", "D", 17, "DEF" },
                    { 257, "E17", "E", 17, "DEF" },
                    { 258, "F17", "F", 17, "DEF" },
                    { 259, "D18", "D", 18, "DEF" },
                    { 260, "E18", "E", 18, "DEF" },
                    { 261, "F18", "F", 18, "DEF" },
                    { 262, "D19", "D", 19, "DEF" },
                    { 263, "E19", "E", 19, "DEF" },
                    { 264, "F19", "F", 19, "DEF" },
                    { 265, "D20", "D", 20, "DEF" },
                    { 266, "E20", "E", 20, "DEF" },
                    { 267, "F20", "F", 20, "DEF" },
                    { 268, "D21", "D", 21, "DEF" },
                    { 269, "E21", "E", 21, "DEF" },
                    { 270, "F21", "F", 21, "DEF" },
                    { 271, "D22", "D", 22, "DEF" },
                    { 272, "E22", "E", 22, "DEF" },
                    { 273, "F22", "F", 22, "DEF" },
                    { 274, "D23", "D", 23, "DEF" },
                    { 275, "E23", "E", 23, "DEF" },
                    { 276, "F23", "F", 23, "DEF" },
                    { 277, "D24", "D", 24, "DEF" },
                    { 278, "E24", "E", 24, "DEF" },
                    { 279, "F24", "F", 24, "DEF" },
                    { 280, "D25", "D", 25, "DEF" },
                    { 281, "E25", "E", 25, "DEF" },
                    { 282, "F25", "F", 25, "DEF" },
                    { 283, "D26", "D", 26, "DEF" },
                    { 284, "E26", "E", 26, "DEF" },
                    { 285, "F26", "F", 26, "DEF" },
                    { 286, "D27", "D", 27, "DEF" },
                    { 287, "E27", "E", 27, "DEF" },
                    { 288, "F27", "F", 27, "DEF" },
                    { 289, "D28", "D", 28, "DEF" },
                    { 290, "E28", "E", 28, "DEF" },
                    { 291, "F28", "F", 28, "DEF" },
                    { 292, "D29", "D", 29, "DEF" },
                    { 293, "E29", "E", 29, "DEF" },
                    { 294, "F29", "F", 29, "DEF" },
                    { 295, "D30", "D", 30, "DEF" },
                    { 296, "E30", "E", 30, "DEF" },
                    { 297, "F30", "F", 30, "DEF" },
                    { 298, "D31", "D", 31, "DEF" },
                    { 299, "E31", "E", 31, "DEF" },
                    { 300, "F31", "F", 31, "DEF" },
                    { 301, "D32", "D", 32, "DEF" },
                    { 302, "E32", "E", 32, "DEF" },
                    { 303, "F32", "F", 32, "DEF" },
                    { 304, "D33", "D", 33, "DEF" },
                    { 305, "E33", "E", 33, "DEF" },
                    { 306, "F33", "F", 33, "DEF" },
                    { 307, "D34", "D", 34, "DEF" },
                    { 308, "E34", "E", 34, "DEF" },
                    { 309, "F34", "F", 34, "DEF" },
                    { 310, "D35", "D", 35, "DEF" },
                    { 311, "E35", "E", 35, "DEF" },
                    { 312, "F35", "F", 35, "DEF" },
                    { 313, "D36", "D", 36, "DEF" },
                    { 314, "E36", "E", 36, "DEF" },
                    { 315, "F36", "F", 36, "DEF" },
                    { 316, "D37", "D", 37, "DEF" },
                    { 317, "E37", "E", 37, "DEF" },
                    { 318, "F37", "F", 37, "DEF" },
                    { 319, "D38", "D", 38, "DEF" },
                    { 320, "E38", "E", 38, "DEF" },
                    { 321, "F38", "F", 38, "DEF" },
                    { 322, "D39", "D", 39, "DEF" },
                    { 323, "E39", "E", 39, "DEF" },
                    { 324, "F39", "F", 39, "DEF" },
                    { 325, "D40", "D", 40, "DEF" },
                    { 326, "E40", "E", 40, "DEF" },
                    { 327, "F40", "F", 40, "DEF" },
                    { 328, "D41", "D", 41, "DEF" },
                    { 329, "E41", "E", 41, "DEF" },
                    { 330, "F41", "F", 41, "DEF" },
                    { 331, "D42", "D", 42, "DEF" },
                    { 332, "E42", "E", 42, "DEF" },
                    { 333, "F42", "F", 42, "DEF" },
                    { 334, "D43", "D", 43, "DEF" },
                    { 335, "E43", "E", 43, "DEF" },
                    { 336, "F43", "F", 43, "DEF" },
                    { 337, "D44", "D", 44, "DEF" },
                    { 338, "E44", "E", 44, "DEF" },
                    { 339, "F44", "F", 44, "DEF" },
                    { 340, "D45", "D", 45, "DEF" },
                    { 341, "E45", "E", 45, "DEF" },
                    { 342, "F45", "F", 45, "DEF" },
                    { 343, "D46", "D", 46, "DEF" },
                    { 344, "E46", "E", 46, "DEF" },
                    { 345, "F46", "F", 46, "DEF" },
                    { 346, "D47", "D", 47, "DEF" },
                    { 347, "E47", "E", 47, "DEF" },
                    { 348, "F47", "F", 47, "DEF" },
                    { 349, "D48", "D", 48, "DEF" },
                    { 350, "E48", "E", 48, "DEF" },
                    { 351, "F48", "F", 48, "DEF" },
                    { 352, "D49", "D", 49, "DEF" },
                    { 353, "E49", "E", 49, "DEF" },
                    { 354, "F49", "F", 49, "DEF" },
                    { 355, "D50", "D", 50, "DEF" },
                    { 356, "E50", "E", 50, "DEF" },
                    { 357, "F50", "F", 50, "DEF" },
                    { 358, "D51", "D", 51, "DEF" },
                    { 359, "E51", "E", 51, "DEF" },
                    { 360, "F51", "F", 51, "DEF" },
                    { 361, "D52", "D", 52, "DEF" },
                    { 362, "E52", "E", 52, "DEF" },
                    { 363, "F52", "F", 52, "DEF" },
                    { 364, "D53", "D", 53, "DEF" },
                    { 365, "E53", "E", 53, "DEF" },
                    { 366, "F53", "F", 53, "DEF" },
                    { 367, "D54", "D", 54, "DEF" },
                    { 368, "E54", "E", 54, "DEF" },
                    { 369, "F54", "F", 54, "DEF" },
                    { 370, "D55", "D", 55, "DEF" },
                    { 371, "E55", "E", 55, "DEF" },
                    { 372, "F55", "F", 55, "DEF" },
                    { 373, "D56", "D", 56, "DEF" },
                    { 374, "E56", "E", 56, "DEF" },
                    { 375, "F56", "F", 56, "DEF" },
                    { 376, "D57", "D", 57, "DEF" },
                    { 377, "E57", "E", 57, "DEF" },
                    { 378, "F57", "F", 57, "DEF" },
                    { 379, "D58", "D", 58, "DEF" },
                    { 380, "E58", "E", 58, "DEF" },
                    { 381, "F58", "F", 58, "DEF" },
                    { 382, "D59", "D", 59, "DEF" },
                    { 383, "E59", "E", 59, "DEF" },
                    { 384, "F59", "F", 59, "DEF" },
                    { 385, "D60", "D", 60, "DEF" },
                    { 386, "E60", "E", 60, "DEF" },
                    { 387, "F60", "F", 60, "DEF" },
                    { 388, "D61", "D", 61, "DEF" },
                    { 389, "E61", "E", 61, "DEF" },
                    { 390, "F61", "F", 61, "DEF" },
                    { 391, "D62", "D", 62, "DEF" },
                    { 392, "E62", "E", 62, "DEF" },
                    { 393, "F62", "F", 62, "DEF" },
                    { 394, "D63", "D", 63, "DEF" },
                    { 395, "E63", "E", 63, "DEF" },
                    { 396, "F63", "F", 63, "DEF" },
                    { 397, "D64", "D", 64, "DEF" },
                    { 398, "E64", "E", 64, "DEF" },
                    { 399, "F64", "F", 64, "DEF" },
                    { 400, "D65", "D", 65, "DEF" },
                    { 401, "E65", "E", 65, "DEF" },
                    { 402, "F65", "F", 65, "DEF" },
                    { 403, "D66", "D", 66, "DEF" },
                    { 404, "E66", "E", 66, "DEF" },
                    { 405, "F66", "F", 66, "DEF" },
                    { 406, "D67", "D", 67, "DEF" },
                    { 407, "E67", "E", 67, "DEF" },
                    { 408, "F67", "F", 67, "DEF" },
                    { 409, "D68", "D", 68, "DEF" },
                    { 410, "E68", "E", 68, "DEF" },
                    { 411, "F68", "F", 68, "DEF" },
                    { 412, "D69", "D", 69, "DEF" },
                    { 413, "E69", "E", 69, "DEF" },
                    { 414, "F69", "F", 69, "DEF" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AreaZInventory_ProductId_Active",
                table: "AreaZInventory",
                column: "ProductId",
                unique: true,
                filter: "\"IsDispatched\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentLog_AdjustedAt",
                table: "InventoryAdjustmentLogs",
                column: "AdjustedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentLog_ShelfInventoryId",
                table: "InventoryAdjustmentLogs",
                column: "ShelfInventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId_Name",
                table: "Products",
                columns: new[] { "CategoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_OrderId",
                table: "PurchaseOrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_ProductId",
                table: "PurchaseOrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_ReceivedShelfId",
                table: "PurchaseOrderItems",
                column: "ReceivedShelfId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_OrderedAt",
                table: "PurchaseOrders",
                column: "OrderedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_Status",
                table: "PurchaseOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "Roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShelfInventory_OrderItemId",
                table: "ShelfInventory",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ShelfInventory_ProductId",
                table: "ShelfInventory",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ShelfInventory_ShelfId_Position",
                table: "ShelfInventory",
                columns: new[] { "ShelfId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shelves_Code",
                table: "Shelves",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shelves_Side_Number",
                table: "Shelves",
                columns: new[] { "Side", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsActive",
                table: "Users",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "Users",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AreaZInventory");

            migrationBuilder.DropTable(
                name: "InventoryAdjustmentLogs");

            migrationBuilder.DropTable(
                name: "RoleClaims");

            migrationBuilder.DropTable(
                name: "UserClaims");

            migrationBuilder.DropTable(
                name: "UserLogins");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "UserTokens");

            migrationBuilder.DropTable(
                name: "ShelfInventory");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "PurchaseOrderItems");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.DropTable(
                name: "Shelves");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
