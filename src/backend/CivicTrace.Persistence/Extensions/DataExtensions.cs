using CivicTrace.Persistence.Context;
using CivicTrace.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CivicTrace.Persistence.Extensions;

public static class DataExtensions
{
    public static async Task ApplyMigrationsAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();

        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var identityDb = scope.ServiceProvider.GetRequiredService<ApplicationIdentityDbContext>();

        await appDb.Database.MigrateAsync(ct);
        await identityDb.Database.MigrateAsync(ct);

        // Гарантуємо наявність таблиць Треку 3, навіть якщо БД уже була ініціалізована раніше
        await appDb.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "RentalObjects" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "Ocid" character varying(40) NOT NULL,
                "ApiId" character varying(40),
                "RawTitle" character varying(1000) NOT NULL,
                "NormalizedTitle" character varying(1000) NOT NULL,
                "ObjectType" character varying(150) NOT NULL,
                "CanonicalUrl" character varying(500) NOT NULL,
                "OriginalUrl" character varying(500) NOT NULL,
                "EnrichmentStatus" character varying(30) NOT NULL,
                "CreatedYear" integer,
                "DatePublished" timestamp with time zone,
                "DateModified" timestamp with time zone,
                "Address" character varying(1000),
                "District" character varying(120),
                "Latitude" double precision,
                "Longitude" double precision,
                "CoordinatesStatus" character varying(30),
                "AreaSqm" numeric(18,2),
                "StatusCode" character varying(30) NOT NULL,
                "StatusLabel" character varying(100) NOT NULL,
                "SourceStatus" character varying(100),
                "ListType" character varying(30) NOT NULL,
                "PropertyOwnerName" character varying(500),
                "PropertyOwnerEdrpou" character varying(16),
                "SellingEntityName" character varying(500),
                "SellingEntityEdrpou" character varying(16),
                "MarketValue" numeric(18,2),
                "SecondListAbsoluteValue" numeric(18,2),
                "LeaseDuration" character varying(40),
                "SubleaseAllowed" boolean,
                "CurrentContractNumber" character varying(120),
                "CurrentContractMethod" character varying(80),
                "CurrentContractValue" numeric(18,2),
                "CurrentContractStart" timestamp with time zone,
                "CurrentContractEnd" timestamp with time zone,
                "CurrentTenantLabel" character varying(500),
                "DataDefectsNotes" character varying(2000),
                "LastSyncedAt" timestamp with time zone,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" uuid NOT NULL,
                "UpdatedAt" timestamp with time zone,
                "UpdatedBy" uuid
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RentalObjects_Ocid" ON "RentalObjects" ("Ocid");
            CREATE INDEX IF NOT EXISTS "IX_RentalObjects_StatusCode" ON "RentalObjects" ("StatusCode");
            CREATE INDEX IF NOT EXISTS "IX_RentalObjects_EnrichmentStatus" ON "RentalObjects" ("EnrichmentStatus");
            CREATE INDEX IF NOT EXISTS "IX_RentalObjects_CreatedYear" ON "RentalObjects" ("CreatedYear");

            CREATE TABLE IF NOT EXISTS "RentalAuctions" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "AuctionId" character varying(40) NOT NULL,
                "ApiId" character varying(40) NOT NULL,
                "RentalObjectId" uuid REFERENCES "RentalObjects"("Id") ON DELETE SET NULL,
                "RegistryObjectOcid" character varying(40),
                "Title" character varying(1000) NOT NULL,
                "SellingMethod" character varying(120) NOT NULL,
                "Status" character varying(60) NOT NULL,
                "StatusLabel" character varying(120) NOT NULL,
                "TenderAttempts" integer,
                "StartPrice" numeric(18,2),
                "FinalPrice" numeric(18,2),
                "MinimalStep" numeric(18,2),
                "AreaSqm" numeric(18,2),
                "PricePerSqmStart" numeric(18,2),
                "PricePerSqmFinal" numeric(18,2),
                "BidsCount" integer NOT NULL,
                "HasSingleBidder" boolean NOT NULL,
                "PriceIncreasePct" numeric(18,2),
                "WinnerKeyMasked" character varying(120),
                "DatePublished" timestamp with time zone,
                "AuctionDate" timestamp with time zone,
                "DateCompleted" timestamp with time zone,
                "ContractDateSigned" timestamp with time zone,
                "SourceUrl" character varying(500) NOT NULL,
                "RiskScore" integer NOT NULL,
                "RiskFlagsJson" character varying(4000) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "CreatedBy" uuid NOT NULL,
                "UpdatedAt" timestamp with time zone,
                "UpdatedBy" uuid
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RentalAuctions_AuctionId" ON "RentalAuctions" ("AuctionId");
            CREATE INDEX IF NOT EXISTS "IX_RentalAuctions_RentalObjectId" ON "RentalAuctions" ("RentalObjectId");
            CREATE INDEX IF NOT EXISTS "IX_RentalAuctions_RegistryObjectOcid" ON "RentalAuctions" ("RegistryObjectOcid");
            """, ct);
    }
}