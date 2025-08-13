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
                // PostgreSQL: convert Timestamp column from text to timestamp (without time zone)
                migrationBuilder.Sql(@"
                    -- Optional safety backup
                    CREATE TABLE IF NOT EXISTS ""ActiveTradingSignals_ts_backup"" AS SELECT * FROM ""ActiveTradingSignals"";

                    -- First update empty strings to NULL
                    UPDATE ""ActiveTradingSignals"" 
                    SET ""Timestamp"" = NULL 
                    WHERE ""Timestamp"" = '' OR ""Timestamp"" IS NULL;

                    -- Then convert text to timestamp (without time zone)
                    ALTER TABLE ""ActiveTradingSignals""
                    ALTER COLUMN ""Timestamp"" TYPE timestamp WITHOUT time zone 
                    USING 
                      CASE 
                        WHEN ""Timestamp"" IS NULL THEN NULL 
                        ELSE ""Timestamp""::timestamp 
                      END;
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
