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
            // PostgreSQL-specific SQL to fix the Price column in ActiveTradingSignals from text to numeric
            migrationBuilder.Sql(@"
                -- First, create a backup of the table (optional but safe)
                CREATE TABLE ""ActiveTradingSignals_backup"" AS SELECT * FROM ""ActiveTradingSignals"";
                
                -- Alter the Price column from text to numeric (decimal)
                ALTER TABLE ""ActiveTradingSignals"" 
                ALTER COLUMN ""Price"" TYPE numeric USING ""Price""::numeric;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
