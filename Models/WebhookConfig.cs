using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingSignalsApi.Models
{
    /// <summary>
    /// Represents a webhook configuration
    /// </summary>
    public class WebhookConfig
    {
        /// <summary>
        /// Primary key for the webhook configuration
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// The path for the webhook endpoint (used in /webhook/{path})
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Secret key for authenticating webhook requests
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Secret { get; set; } = string.Empty;

        /// <summary>
        /// Optional description of the webhook configuration
        /// </summary>
        [StringLength(500)]
        public string? Description { get; set; }
    }
}
