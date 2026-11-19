using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CivicTrace.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class SyncPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RentalObjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Ocid = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ApiId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    RawTitle = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    NormalizedTitle = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ObjectType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CanonicalUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OriginalUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EnrichmentStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedYear = table.Column<int>(type: "integer", nullable: true),
                    DatePublished = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DateModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    District = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    CoordinatesStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AreaSqm = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    StatusCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StatusLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ListType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PropertyOwnerName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PropertyOwnerEdrpou = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    SellingEntityName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SellingEntityEdrpou = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    MarketValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SecondListAbsoluteValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LeaseDuration = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SubleaseAllowed = table.Column<bool>(type: "boolean", nullable: true),
                    CurrentContractNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CurrentContractMethod = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CurrentContractValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrentContractStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CurrentContractEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CurrentTenantLabel = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DataDefectsNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalObjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RentalAuctions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuctionId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ApiId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RentalObjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    RegistryObjectOcid = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SellingMethod = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    StatusLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TenderAttempts = table.Column<int>(type: "integer", nullable: true),
                    StartPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    FinalPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MinimalStep = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AreaSqm = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PricePerSqmStart = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PricePerSqmFinal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    BidsCount = table.Column<int>(type: "integer", nullable: false),
                    HasSingleBidder = table.Column<bool>(type: "boolean", nullable: false),
                    PriceIncreasePct = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    WinnerKeyMasked = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DatePublished = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AuctionDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DateCompleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ContractDateSigned = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RiskScore = table.Column<int>(type: "integer", nullable: false),
                    RiskFlagsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalAuctions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RentalAuctions_RentalObjects_RentalObjectId",
                        column: x => x.RentalObjectId,
                        principalTable: "RentalObjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RentalAuctions_AuctionId",
                table: "RentalAuctions",
                column: "AuctionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentalAuctions_RegistryObjectOcid",
                table: "RentalAuctions",
                column: "RegistryObjectOcid");

            migrationBuilder.CreateIndex(
                name: "IX_RentalAuctions_RentalObjectId",
                table: "RentalAuctions",
                column: "RentalObjectId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalObjects_CreatedYear",
                table: "RentalObjects",
                column: "CreatedYear");

            migrationBuilder.CreateIndex(
                name: "IX_RentalObjects_EnrichmentStatus",
                table: "RentalObjects",
                column: "EnrichmentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_RentalObjects_Ocid",
                table: "RentalObjects",
                column: "Ocid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentalObjects_StatusCode",
                table: "RentalObjects",
                column: "StatusCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RentalAuctions");

            migrationBuilder.DropTable(
                name: "RentalObjects");
        }
    }
}
