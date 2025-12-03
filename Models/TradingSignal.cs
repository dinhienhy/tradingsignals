using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace TradingSignalsApi.Models
{
    /// <summary>
    /// Represents a trading signal
    /// </summary>
    public class TradingSignal
    {
        /// <summary>
        /// Primary key for the trading signal
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Trading symbol (e.g., EURUSD, BTCUSD)
        /// </summary>
        [Required]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Trading action (e.g., BUY, SELL)
        /// </summary>
        [Required]
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Price level for the trading signal
        /// </summary>
        [Required]
        public decimal Price { get; set; }

        /// <summary>
        /// Timestamp when the signal was created
        /// </summary>
        [Required]
        private DateTime _timestamp = DateTime.UtcNow;
        
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp 
        { 
            get => _timestamp;
            set => _timestamp = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        /// <summary>
        /// Additional message or information about the signal
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Current status of the trading signal
        /// </summary>
        public SignalStatus Status { get; set; } = SignalStatus.Pending;
    }
}
