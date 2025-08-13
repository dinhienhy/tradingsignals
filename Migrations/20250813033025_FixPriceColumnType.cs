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
                // PostgreSQL-specific SQL to fix the Price column in ActiveTradingSignals from text to numeric
                migrationBuilder.Sql(@"
                    -- First, create a backup of the table (optional but safe)
                    CREATE TABLE IF NOT EXISTS ""ActiveTradingSignals_backup"" AS SELECT * FROM ""ActiveTradingSignals"";
                    
                    -- First update empty strings to NULL
                    UPDATE ""ActiveTradingSignals"" 
                    SET ""Price"" = NULL 
                    WHERE ""Price"" = '' OR ""Price"" IS NULL;
                    
                    -- Then alter the Price column from text to numeric (decimal)
                    ALTER TABLE ""ActiveTradingSignals"" 
                    ALTER COLUMN ""Price"" TYPE numeric 
                    USING 
                      CASE 
                        WHEN ""Price"" IS NULL THEN NULL 
                        ELSE ""Price""::numeric 
                      END;
                ");
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
