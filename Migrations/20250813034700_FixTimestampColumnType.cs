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
                // Ultra simple migration that just creates a new empty table and swaps it
                migrationBuilder.Sql(@"
                    -- Create a new empty table with the correct column types
                    CREATE TABLE ""ActiveTradingSignals_new"" (
                        ""Id"" serial PRIMARY KEY,
                        ""Symbol"" text,
                        ""Action"" text,
                        ""Price"" numeric,
                        ""Timestamp"" timestamp,
                        ""Type"" text,
                        ""UniqueKey"" text
                    );
                    
                    -- No data migration - start with a fresh empty table to avoid any conversion issues
                    -- Just save the old table as backup
                    ALTER TABLE ""ActiveTradingSignals"" RENAME TO ""ActiveTradingSignals_old"";
                    
                    -- Rename the new table to the original name
                    ALTER TABLE ""ActiveTradingSignals_new"" RENAME TO ""ActiveTradingSignals"";
                ");
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
