using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingSignalsApi.Migrations
{
    /// <inheritdoc />
    public partial class FixIdentityColumnsCorrectly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL-specific commands to set the Id columns as identity columns
            migrationBuilder.Sql(@"
                -- Drop the existing default values and sequences if they exist
                DO $$ 
                BEGIN
                    -- For WebhookConfigs table
                    BEGIN
                        ALTER TABLE ""WebhookConfigs"" ALTER COLUMN ""Id"" DROP DEFAULT;
                        DROP SEQUENCE IF EXISTS ""WebhookConfigs_Id_seq"";
                    EXCEPTION WHEN OTHERS THEN
                        -- Do nothing, just continue
                    END;
                    
                    -- For TradingSignals table
                    BEGIN
                        ALTER TABLE ""TradingSignals"" ALTER COLUMN ""Id"" DROP DEFAULT;
                        DROP SEQUENCE IF EXISTS ""TradingSignals_Id_seq"";
                    EXCEPTION WHEN OTHERS THEN
                        -- Do nothing, just continue
                    END;
                    
                    -- For ActiveTradingSignals table
                    BEGIN
                        ALTER TABLE ""ActiveTradingSignals"" ALTER COLUMN ""Id"" DROP DEFAULT;
                        DROP SEQUENCE IF EXISTS ""ActiveTradingSignals_Id_seq"";
                    EXCEPTION WHEN OTHERS THEN
                        -- Do nothing, just continue
                    END;
                END $$;
                
                -- Create new sequences
                CREATE SEQUENCE IF NOT EXISTS ""WebhookConfigs_Id_seq"" 
                    START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;
                CREATE SEQUENCE IF NOT EXISTS ""TradingSignals_Id_seq"" 
                    START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;
                CREATE SEQUENCE IF NOT EXISTS ""ActiveTradingSignals_Id_seq"" 
                    START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;
                
                -- Set the current sequence values
                SELECT setval('""WebhookConfigs_Id_seq""', COALESCE((SELECT MAX(""Id"") FROM ""WebhookConfigs""), 0) + 1, false);
                SELECT setval('""TradingSignals_Id_seq""', COALESCE((SELECT MAX(""Id"") FROM ""TradingSignals""), 0) + 1, false);
                SELECT setval('""ActiveTradingSignals_Id_seq""', COALESCE((SELECT MAX(""Id"") FROM ""ActiveTradingSignals""), 0) + 1, false);
                
                -- Alter columns to use the sequences
                ALTER TABLE ""WebhookConfigs"" ALTER COLUMN ""Id"" SET DEFAULT nextval('""WebhookConfigs_Id_seq""');
                ALTER TABLE ""TradingSignals"" ALTER COLUMN ""Id"" SET DEFAULT nextval('""TradingSignals_Id_seq""');
                ALTER TABLE ""ActiveTradingSignals"" ALTER COLUMN ""Id"" SET DEFAULT nextval('""ActiveTradingSignals_Id_seq""');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the identity column configuration
            migrationBuilder.Sql(@"
                -- Remove default value sequences
                ALTER TABLE ""WebhookConfigs"" ALTER COLUMN ""Id"" DROP DEFAULT;
                ALTER TABLE ""TradingSignals"" ALTER COLUMN ""Id"" DROP DEFAULT;
                ALTER TABLE ""ActiveTradingSignals"" ALTER COLUMN ""Id"" DROP DEFAULT;
                
                -- Drop sequences
                DROP SEQUENCE IF EXISTS ""WebhookConfigs_Id_seq"";
                DROP SEQUENCE IF EXISTS ""TradingSignals_Id_seq"";
                DROP SEQUENCE IF EXISTS ""ActiveTradingSignals_Id_seq"";
            ");
        }
    }
}
