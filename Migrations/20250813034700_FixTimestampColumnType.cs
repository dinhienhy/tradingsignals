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
                // Create a completely new table with the correct schema
                migrationBuilder.Sql(@"
                    -- Create a new table with the correct column types
                    CREATE TABLE ""ActiveTradingSignals_new"" (
                        ""Id"" serial PRIMARY KEY,
                        ""Symbol"" text,
                        ""Action"" text,
                        ""Price"" numeric,
                        ""Timestamp"" timestamp,
                        ""Type"" text
                    );
                    
                    -- Insert only valid records, avoiding conversion errors
                    INSERT INTO ""ActiveTradingSignals_new"" (""Symbol"", ""Action"", ""Price"", ""Timestamp"", ""Type"")
                    SELECT 
                        ""Symbol"", 
                        ""Action"", 
                        CASE WHEN ""Price"" ~ '^[0-9]+(\\.[0-9]+)?$' THEN ""Price""::numeric ELSE NULL END,
                        CASE 
                            WHEN ""Timestamp"" ~ '^[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}:[0-9]{2}' THEN ""Timestamp""::timestamp 
                            ELSE NULL 
                        END,
                        ""Type""
                    FROM ""ActiveTradingSignals"";
                    
                    -- Backup the old table
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
