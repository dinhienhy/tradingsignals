using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingSignalsApi.Services;

/// <summary>
/// Service to interact with MetaApi.cloud for getting real-time prices
/// </summary>
public class MetaApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MetaApiService> _logger;
    
    private string? _apiToken;
    private string? _accountId;
    
    public MetaApiService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<MetaApiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        
        _apiToken = _configuration["MetaApi:Token"] ?? Environment.GetEnvironmentVariable("METAAPI_TOKEN");
        _accountId = _configuration["MetaApi:AccountId"] ?? Environment.GetEnvironmentVariable("METAAPI_ACCOUNT_ID");
    }
    
    /// <summary>
    /// Get current price for a symbol
    /// </summary>
    public async Task<SymbolPrice?> GetCurrentPriceAsync(string symbol)
    {
        if (string.IsNullOrEmpty(_apiToken) || string.IsNullOrEmpty(_accountId))
        {
            _logger.LogWarning("MetaApi credentials not configured");
            return null;
        }
        
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("auth-token", _apiToken);
            
            // Use London region for MetaAPI (correct URL format)
            var url = $"https://mt-client-api-v1.london.agiliumtrade.ai/users/current/accounts/{_accountId}/symbols/{symbol}/current-price";
            
            _logger.LogDebug("Fetching price for {Symbol} from MetaApi", symbol);
            
            var response = await client.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get price for {Symbol}: {StatusCode}", symbol, response.StatusCode);
                return null;
            }
            
            var content = await response.Content.ReadAsStringAsync();
            var priceData = JsonSerializer.Deserialize<MetaApiPriceResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (priceData == null)
            {
                _logger.LogWarning("Failed to parse price data for {Symbol}", symbol);
                return null;
            }
            
            var price = new SymbolPrice
            {
                Symbol = priceData.Symbol ?? symbol,
                Bid = priceData.Bid,
                Ask = priceData.Ask,
                Time = priceData.Time ?? DateTime.UtcNow
            };
            
            _logger.LogDebug("Price for {Symbol}: Bid={Bid}, Ask={Ask}", symbol, price.Bid, price.Ask);
            
            return price;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching price for {Symbol}", symbol);
            return null;
        }
    }
    
    /// <summary>
    /// Get current prices for multiple symbols in batch
    /// </summary>
    public async Task<Dictionary<string, SymbolPrice>> GetCurrentPricesAsync(IEnumerable<string> symbols)
    {
        var result = new Dictionary<string, SymbolPrice>();
        
        // Fetch prices in parallel
        var tasks = symbols.Distinct().Select(async symbol =>
        {
            var price = await GetCurrentPriceAsync(symbol);
            return (symbol, price);
        });
        
        var prices = await Task.WhenAll(tasks);
        
        foreach (var (symbol, price) in prices)
        {
            if (price != null)
            {
                result[symbol] = price;
            }
        }
        
        return result;
    }
}

/// <summary>
/// Response from MetaApi current price endpoint
/// </summary>
public class MetaApiPriceResponse
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }
    
    [JsonPropertyName("bid")]
    public decimal Bid { get; set; }
    
    [JsonPropertyName("ask")]
    public decimal Ask { get; set; }
    
    [JsonPropertyName("time")]
    public DateTime? Time { get; set; }
    
    [JsonPropertyName("brokerTime")]
    public string? BrokerTime { get; set; }
}

/// <summary>
/// Simplified price data
/// </summary>
public class SymbolPrice
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Bid { get; set; }
    public decimal Ask { get; set; }
    public DateTime Time { get; set; }
    
    /// <summary>
    /// Get mid price (average of bid and ask)
    /// </summary>
    public decimal MidPrice => (Bid + Ask) / 2;
}
