using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingSignalsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSwingAndResolvedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Resolved",
                table: "ActiveTradingSignals",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Swing",
                table: "ActiveTradingSignals",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Resolved",
                table: "ActiveTradingSignals");

            migrationBuilder.DropColumn(
                name: "Swing",
                table: "ActiveTradingSignals");
        }
    }
}
