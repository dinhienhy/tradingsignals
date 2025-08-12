using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        
        public DateTime Timestamp { get; set; }
        
        // The Path/Type from the webhook URL
        public string Type { get; set; }
        
        // Concatenated key for uniqueness check (Symbol_Type)
        public string UniqueKey { get; set; }
    }
}
