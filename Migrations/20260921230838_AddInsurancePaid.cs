using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EaglesResort.Migrations
{
    /// <inheritdoc />
    public partial class AddInsurancePaid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Chalets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<string>(type: "TEXT", nullable: false),
                    BuildingArea = table.Column<string>(type: "TEXT", nullable: false),
                    ChaletType = table.Column<string>(type: "TEXT", nullable: false),
                    RoomsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxCapacity = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chalets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChaletType = table.Column<string>(type: "TEXT", nullable: false),
                    Month = table.Column<int>(type: "INTEGER", nullable: false),
                    DailyRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    AdultMealRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    ChildMealRate = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpecialRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChaletType = table.Column<string>(type: "TEXT", nullable: false),
                    GuestCategory = table.Column<string>(type: "TEXT", nullable: false),
                    DailyRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    AdultMealRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    ChildMealRate = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChaletId = table.Column<int>(type: "INTEGER", nullable: false),
                    GuestName = table.Column<string>(type: "TEXT", nullable: false),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: false),
                    GuestRank = table.Column<string>(type: "TEXT", nullable: false),
                    GuestCategory = table.Column<string>(type: "TEXT", nullable: false),
                    PaymentSystem = table.Column<string>(type: "TEXT", nullable: false),
                    MealPlan = table.Column<string>(type: "TEXT", nullable: false),
                    AdultCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ChildCount = table.Column<int>(type: "INTEGER", nullable: false),
                    HasResortMembership = table.Column<bool>(type: "INTEGER", nullable: false),
                    MembershipNumber = table.Column<string>(type: "TEXT", nullable: false),
                    CheckInDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CheckOutDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    MealStatus = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    InsurancePaid = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookings_Chalets_ChaletId",
                        column: x => x.ChaletId,
                        principalTable: "Chalets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BookingId = table.Column<int>(type: "INTEGER", nullable: false),
                    DailyRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    ChaletTotal = table.Column<decimal>(type: "TEXT", nullable: false),
                    AdultMealsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AdultMealRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    ChildMealsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ChildMealRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    MealsTotal = table.Column<decimal>(type: "TEXT", nullable: false),
                    ExtraServices = table.Column<decimal>(type: "TEXT", nullable: false),
                    ExtraServicesNote = table.Column<string>(type: "TEXT", nullable: false),
                    Insurance = table.Column<decimal>(type: "TEXT", nullable: false),
                    Deposit = table.Column<decimal>(type: "TEXT", nullable: false),
                    DepositDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HousingPaymentMethod = table.Column<string>(type: "TEXT", nullable: false),
                    MealsPaymentMethod = table.Column<string>(type: "TEXT", nullable: false),
                    NetHousing = table.Column<decimal>(type: "TEXT", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalAfterDeposit = table.Column<decimal>(type: "TEXT", nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReceptionistName = table.Column<string>(type: "TEXT", nullable: false),
                    AccountantName = table.Column<string>(type: "TEXT", nullable: false),
                    ResortManagerName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSettings_Key",
                table: "AppSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ChaletId",
                table: "Bookings",
                column: "ChaletId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_BookingId",
                table: "Invoices",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rates_ChaletType_Month",
                table: "Rates",
                columns: new[] { "ChaletType", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "Rates");

            migrationBuilder.DropTable(
                name: "SpecialRates");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "Chalets");
        }
    }
}
