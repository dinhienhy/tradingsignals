using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingSignalsApi.Migrations
{
    /// <inheritdoc />
    public partial class FixUsedColumnTypeWithTempTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tạo bảng tạm thời với cấu trúc đúng
            migrationBuilder.Sql(@"
                CREATE TABLE ""ActiveTradingSignals_Temp"" (
                    ""Id"" serial NOT NULL,
                    ""Symbol"" text NOT NULL,
                    ""Action"" text NOT NULL,
                    ""Price"" numeric NOT NULL,
                    ""Timestamp"" timestamp with time zone NOT NULL,
                    ""Type"" text NOT NULL,
                    ""UniqueKey"" text NULL,
                    ""Used"" boolean NOT NULL DEFAULT FALSE,
                    CONSTRAINT ""PK_ActiveTradingSignals_Temp"" PRIMARY KEY (""Id"")
                );
            ");

            // Copy dữ liệu từ bảng cũ sang bảng mới với chuyển đổi kiểu dữ liệu
            migrationBuilder.Sql(@"
                INSERT INTO ""ActiveTradingSignals_Temp"" (""Id"", ""Symbol"", ""Action"", ""Price"", ""Timestamp"", ""Type"", ""UniqueKey"", ""Used"")
                SELECT 
                    ""Id"", 
                    ""Symbol"", 
                    ""Action"", 
                    ""Price"", 
                    ""Timestamp"", 
                    ""Type"", 
                    ""UniqueKey"", 
                    CASE WHEN ""Used"" = 0 OR ""Used"" IS NULL THEN FALSE ELSE TRUE END
                FROM ""ActiveTradingSignals"";
            ");

            // Xóa bảng cũ
            migrationBuilder.Sql(@"DROP TABLE ""ActiveTradingSignals"";");

            // Đổi tên bảng mới thành tên bảng cũ
            migrationBuilder.Sql(@"ALTER TABLE ""ActiveTradingSignals_Temp"" RENAME TO ""ActiveTradingSignals"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
