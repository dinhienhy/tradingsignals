using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingSignalsApi.Migrations
{
    /// <inheritdoc />
    public partial class FixPriceColumnType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                // PostgreSQL: First create backup table
                migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS ""ActiveTradingSignals_backup"" AS SELECT * FROM ""ActiveTradingSignals""");
                
                // Step 1: Clean up the data first - set all empty or problematic values to NULL
                migrationBuilder.Sql(@"UPDATE ""ActiveTradingSignals"" SET ""Price"" = NULL WHERE ""Price"" = '' OR ""Price"" IS NULL OR ""Price"" !~ '^[0-9]+(\\.[0-9]+)?$'");

                // Step 2: Add a temporary column with the correct type
                migrationBuilder.Sql(@"ALTER TABLE ""ActiveTradingSignals"" ADD COLUMN ""PriceNew"" numeric");

                // Step 3: Copy valid data to the new column
                migrationBuilder.Sql(@"UPDATE ""ActiveTradingSignals"" SET ""PriceNew"" = ""Price""::numeric WHERE ""Price"" IS NOT NULL");

                // Step 4: Drop the old column and rename the new one
                migrationBuilder.Sql(@"ALTER TABLE ""ActiveTradingSignals"" DROP COLUMN ""Price""");
                migrationBuilder.Sql(@"ALTER TABLE ""ActiveTradingSignals"" RENAME COLUMN ""PriceNew"" TO ""Price""");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                // Revert to text type if needed (not typically needed but included for completeness)
                migrationBuilder.Sql(@"
                    ALTER TABLE ""ActiveTradingSignals"" 
                    ALTER COLUMN ""Price"" TYPE text USING ""Price""::text;
                    
                    -- Drop the backup table if it exists
                    DROP TABLE IF EXISTS ""ActiveTradingSignals_backup"";
                ");
            }
        }
    }
}
