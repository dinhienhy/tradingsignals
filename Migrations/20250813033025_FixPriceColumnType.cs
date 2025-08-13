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
            // Make this a no-op since the FixTimestampColumnType migration will handle both columns
            // The FixTimestampColumnType migration creates a new table with both Price and Timestamp 
            // columns having the correct types (numeric and timestamp respectively)
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                // No-op migration as FixTimestampColumnType will handle both columns
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
