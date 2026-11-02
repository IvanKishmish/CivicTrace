using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CivicTrace.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Edrpou = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Procurements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ApiId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PlanExternalId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CpvCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CpvName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Method = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpectedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExpectedAmountVatIncluded = table.Column<bool>(type: "boolean", nullable: true),
                    ExpectedAmountCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    ContractAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ContractAmountVatIncluded = table.Column<bool>(type: "boolean", nullable: true),
                    ContractAmountCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PlaceAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PlaceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PlaceLatitude = table.Column<double>(type: "double precision", nullable: true),
                    PlaceLongitude = table.Column<double>(type: "double precision", nullable: true),
                    PlaceCoordinatesStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: true),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    NonCompetitiveLegalBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NonCompetitiveJustification = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NonCompetitiveSourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BidsListComplete = table.Column<bool>(type: "boolean", nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    VerifiedAt = table.Column<DateOnly>(type: "date", nullable: true),
                    VerifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Procurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Procurements_Organizations_BuyerId",
                        column: x => x.BuyerId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Procurements_Organizations_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Procurements_UserProfiles_VerifiedByUserId",
                        column: x => x.VerifiedByUserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bids",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcurementId = table.Column<Guid>(type: "uuid", nullable: false),
                    BidderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountVatIncluded = table.Column<bool>(type: "boolean", nullable: true),
                    AmountCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bids_Organizations_BidderId",
                        column: x => x.BidderId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bids_Procurements_ProcurementId",
                        column: x => x.ProcurementId,
                        principalTable: "Procurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DataGaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcurementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataGaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataGaps_Procurements_ProcurementId",
                        column: x => x.ProcurementId,
                        principalTable: "Procurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FundingSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcurementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpenditureCode = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Territory = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundingSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundingSources_Procurements_ProcurementId",
                        column: x => x.ProcurementId,
                        principalTable: "Procurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcurementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    Stage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DateNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementEvents_Procurements_ProcurementId",
                        column: x => x.ProcurementId,
                        principalTable: "Procurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcurementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementNotes_Procurements_ProcurementId",
                        column: x => x.ProcurementId,
                        principalTable: "Procurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bids_BidderId",
                table: "Bids",
                column: "BidderId");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_ProcurementId_BidderId",
                table: "Bids",
                columns: new[] { "ProcurementId", "BidderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataGaps_ProcurementId",
                table: "DataGaps",
                column: "ProcurementId");

            migrationBuilder.CreateIndex(
                name: "IX_FundingSources_ProcurementId",
                table: "FundingSources",
                column: "ProcurementId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Edrpou",
                table: "Organizations",
                column: "Edrpou",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementEvents_ProcurementId_SortOrder",
                table: "ProcurementEvents",
                columns: new[] { "ProcurementId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementNotes_ProcurementId",
                table: "ProcurementNotes",
                column: "ProcurementId");

            migrationBuilder.CreateIndex(
                name: "IX_Procurements_BuyerId",
                table: "Procurements",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_Procurements_ContractorId",
                table: "Procurements",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_Procurements_CpvCode",
                table: "Procurements",
                column: "CpvCode");

            migrationBuilder.CreateIndex(
                name: "IX_Procurements_ExternalId",
                table: "Procurements",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Procurements_Kind",
                table: "Procurements",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_Procurements_Method",
                table: "Procurements",
                column: "Method");

            migrationBuilder.CreateIndex(
                name: "IX_Procurements_Status",
                table: "Procurements",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Procurements_VerifiedByUserId",
                table: "Procurements",
                column: "VerifiedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bids");

            migrationBuilder.DropTable(
                name: "DataGaps");

            migrationBuilder.DropTable(
                name: "FundingSources");

            migrationBuilder.DropTable(
                name: "ProcurementEvents");

            migrationBuilder.DropTable(
                name: "ProcurementNotes");

            migrationBuilder.DropTable(
                name: "Procurements");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropTable(
                name: "UserProfiles");
        }
    }
}
