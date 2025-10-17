using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingSignalsApi.Migrations
{
    /// <inheritdoc />
    public partial class FixSwingColumnType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Alter Swing column from text to numeric(18,5) for PostgreSQL
            migrationBuilder.Sql(@"
                ALTER TABLE ""ActiveTradingSignals"" 
                ALTER COLUMN ""Swing"" TYPE numeric(18,5) 
                USING CASE 
                    WHEN ""Swing"" ~ '^[0-9]+\.?[0-9]*$' THEN ""Swing""::numeric(18,5)
                    ELSE NULL 
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
