using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingSignalsApi.Migrations
{
    /// <inheritdoc />
    public partial class FixTimestampColumnType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                // PostgreSQL: First create backup table
                migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS ""ActiveTradingSignals_ts_backup"" AS SELECT * FROM ""ActiveTradingSignals""");
                
                // Step 1: Clean up the data first - set all empty or problematic values to NULL
                migrationBuilder.Sql(@"UPDATE ""ActiveTradingSignals"" SET ""Timestamp"" = NULL WHERE ""Timestamp"" = '' OR ""Timestamp"" IS NULL OR ""Timestamp"" NOT SIMILAR TO '[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}:[0-9]{2}(.*)'");

                // Step 2: Add a temporary column with the correct type
                migrationBuilder.Sql(@"ALTER TABLE ""ActiveTradingSignals"" ADD COLUMN ""TimestampNew"" timestamp");

                // Step 3: Copy valid data to the new column
                migrationBuilder.Sql(@"UPDATE ""ActiveTradingSignals"" SET ""TimestampNew"" = ""Timestamp""::timestamp WHERE ""Timestamp"" IS NOT NULL");

                // Step 4: Drop the old column and rename the new one
                migrationBuilder.Sql(@"ALTER TABLE ""ActiveTradingSignals"" DROP COLUMN ""Timestamp""");
                migrationBuilder.Sql(@"ALTER TABLE ""ActiveTradingSignals"" RENAME COLUMN ""TimestampNew"" TO ""Timestamp""");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                // Revert back to text if needed
                migrationBuilder.Sql(@"
                    ALTER TABLE ""ActiveTradingSignals""
                    ALTER COLUMN ""Timestamp"" TYPE text USING ""Timestamp""::text;

                    DROP TABLE IF EXISTS ""ActiveTradingSignals_ts_backup"";
                ");
            }
        }
    }
}
