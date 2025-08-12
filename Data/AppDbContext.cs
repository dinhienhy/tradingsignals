using Microsoft.EntityFrameworkCore;
using TradingSignalsApi.Models;

namespace TradingSignalsApi.Data
{
    /// <summary>
    /// Database context for the application
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Collection of trading signals
        /// </summary>
        public DbSet<TradingSignal> TradingSignals { get; set; } = null!;

        /// <summary>
        /// Collection of webhook configurations
        /// </summary>
        public DbSet<WebhookConfig> WebhookConfigs { get; set; } = null!;
        
        /// <summary>
        /// Collection of active trading signals for MT5 bot (one per Symbol+Type)
        /// </summary>
        public DbSet<ActiveTradingSignal> ActiveTradingSignals { get; set; } = null!;

        /// <summary>
        /// Configure the model that was discovered by convention from the entity types
        /// </summary>
        /// <param name="modelBuilder">The builder being used to construct the model for this context</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Configure ID properties to use Identity columns in PostgreSQL
            modelBuilder.Entity<WebhookConfig>()
                .Property(w => w.Id)
                .UseIdentityColumn();
                
            modelBuilder.Entity<TradingSignal>()
                .Property(t => t.Id)
                .UseIdentityColumn();
                
            modelBuilder.Entity<ActiveTradingSignal>()
                .Property(a => a.Id)
                .UseIdentityColumn();

            // Configure WebhookConfig path to be unique
            modelBuilder.Entity<WebhookConfig>()
                .HasIndex(w => w.Path)
                .IsUnique();

            // Configure default status for TradingSignal
            modelBuilder.Entity<TradingSignal>()
                .Property(t => t.Status)
                .HasDefaultValue(SignalStatus.Pending);
                
            // Configure UniqueKey for ActiveTradingSignal to be unique
            modelBuilder.Entity<ActiveTradingSignal>()
                .HasIndex(a => a.UniqueKey)
                .IsUnique();
        }
    }
}
