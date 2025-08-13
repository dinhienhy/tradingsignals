using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingSignalsApi.Migrations
{
    /// <inheritdoc />
    public partial class FixUsedColumnType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Đầu tiên chuyển đổi các giá trị null thành 0
            migrationBuilder.Sql("UPDATE \"ActiveTradingSignals\" SET \"Used\" = 0 WHERE \"Used\" IS NULL");
            
            // Thay đổi kiểu dữ liệu từ integer thành boolean
            migrationBuilder.Sql("ALTER TABLE \"ActiveTradingSignals\" ALTER COLUMN \"Used\" TYPE boolean USING CASE WHEN \"Used\"=0 THEN false ELSE true END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
