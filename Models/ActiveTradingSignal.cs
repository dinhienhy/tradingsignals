using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace TradingSignalsApi.Models
{
    public class ActiveTradingSignal
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        public string Symbol { get; set; }
        
        public string Action { get; set; }
        
        public decimal Price { get; set; }
        
        private DateTime _timestamp;
        
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp 
        { 
            get => _timestamp;
            set => _timestamp = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
        
        // The Path/Type from the webhook URL
        public string Type { get; set; }
        
        // Concatenated key for uniqueness check (Symbol_Type)
        // Nullable to allow server to generate it
        public string? UniqueKey { get; set; }
        
        // Flag to mark if the signal has been used by MT5 bot
        private bool _used = false;
        
        // Trong PostgreSQL, trường này đang là integer, nhưng trong code chúng ta muốn sử dụng như boolean
        [NotMapped]
        public bool Used 
        { 
            get => _used; 
            set => _used = value; 
        }
        
        // Trường này sẽ ánh xạ với cột integer trong database
        [Column("Used")]
        public int UsedAsInt 
        { 
            get => _used ? 1 : 0; 
            set => _used = value != 0; 
        }
        
        // Swing price level from TradingView signal
        public decimal? Swing { get; set; }
        
        // Flag to mark if the signal has been resolved (processed)
        private bool _resolved = false;
        
        [NotMapped]
        public bool Resolved 
        { 
            get => _resolved; 
            set => _resolved = value; 
        }
        
        // Trường này sẽ ánh xạ với cột integer trong database
        [Column("Resolved")]
        public int ResolvedAsInt 
        { 
            get => _resolved ? 1 : 0; 
            set => _resolved = value != 0; 
        }
    }
}
