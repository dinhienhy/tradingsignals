# Business Rules Engine - Documentation

## 📚 Tổng quan

Business Rules Engine cho phép bạn xử lý Active Trading Signals thông qua các quy tắc nghiệp vụ (business rules) có thể tùy chỉnh và mở rộng.

## 🏗️ Kiến trúc

### Components

1. **ISignalRule Interface** - Định nghĩa contract cho mỗi rule
2. **SignalRuleEngine** - Engine chạy tất cả rules theo thứ tự priority
3. **Concrete Rules** - Các rule cụ thể (DuplicateSignalRule, PriceValidationRule, etc.)
4. **ActiveSignalProcessor Service** - Service orchestrate toàn bộ quá trình

### Luồng xử lý

```
Signal Input
    ↓
ActiveSignalProcessor
    ↓
SignalRuleEngine
    ↓
Rule 1 (Priority 5) → Rule 2 (Priority 10) → Rule 3 (Priority 20) → ...
    ↓
Validation Result
```

## 📝 Built-in Rules

### 1. PriceValidationRule (Priority: 5)
**Mục đích:** Validate giá trị price hợp lệ
- Kiểm tra price > 0
- Phát hiện price spike bất thường (>5% so với trung bình gần đây)

### 2. DuplicateSignalRule (Priority: 10)
**Mục đích:** Ngăn chặn duplicate signals
- Kiểm tra signal trùng lặp trong vòng 5 phút
- So sánh Symbol + Action

### 3. SwingDetectionRule (Priority: 20)
**Mục đích:** Tự động detect và set Swing level
- Tìm swing level gần nhất từ signals trước
- Tự động set swing level nếu chưa có

### 4. SignalExpirationRule (Priority: 50)
**Mục đích:** Auto-resolve signals cũ
- Tự động mark Resolved = 1 cho signals > 24h

## 🔧 Cách sử dụng

### 1. Validate signal trước khi lưu

```csharp
// Inject service
private readonly IActiveSignalProcessor _signalProcessor;

// Validate
var signal = new ActiveTradingSignal { ... };
var result = await _signalProcessor.ProcessSignalAsync(signal);

if (result.Success)
{
    // Signal passed all rules
    await _context.SaveChangesAsync();
}
else
{
    // Handle validation failure
    _logger.LogWarning("Validation failed: {Message}", result.Message);
}
```

### 2. Test via API

**Validate Signal:**
```bash
curl -X POST https://your-app.herokuapp.com/api/activesignals/validate \
  -H "Content-Type: application/json" \
  -H "X-API-Key: your-api-key" \
  -d '{
    "symbol": "EURUSD",
    "action": "BUY",
    "price": 1.05432,
    "type": "scalping",
    "timestamp": "2025-10-16T10:00:00Z"
  }'
```

**Run Maintenance:**
```bash
curl -X POST https://your-app.herokuapp.com/api/activesignals/maintenance \
  -H "X-API-Key: your-api-key"
```

## ➕ Thêm Rule mới

### Bước 1: Tạo Rule class

```csharp
public class MyCustomRule : ISignalRule
{
    public string RuleName => "MyCustomRule";
    public int Priority => 30; // Set priority
    
    private readonly ILogger<MyCustomRule> _logger;
    
    public MyCustomRule(ILogger<MyCustomRule> logger)
    {
        _logger = logger;
    }
    
    public Task<RuleResult> ExecuteAsync(ActiveTradingSignal signal, SignalContext context)
    {
        // Your business logic here
        if (/* validation failed */)
        {
            return Task.FromResult(RuleResult.Failure("Reason", shouldContinue: true));
        }
        
        // Modify signal if needed
        signal.Swing = "1.05000";
        
        return Task.FromResult(RuleResult.Success("Rule passed"));
    }
}
```

### Bước 2: Register trong Program.cs

```csharp
builder.Services.AddTransient<ISignalRule, MyCustomRule>();
```

## 🎯 Best Practices

1. **Priority Numbers:**
   - 1-10: Critical validation (price, data integrity)
   - 11-30: Business logic (duplicates, calculations)
   - 31-50: Enhancement (auto-fill, detection)
   - 51+: Maintenance (cleanup, expiration)

2. **Rule Result:**
   - `RuleResult.Success()` - Rule passed, continue
   - `RuleResult.Failure(msg, shouldContinue: true)` - Failed but continue other rules
   - `RuleResult.Failure(msg, shouldContinue: false)` - Failed, stop execution
   - `RuleResult.Skip(msg)` - Pass but skip remaining rules

3. **Context Data:**
   - Sử dụng `context.ExistingSignals` để access signals khác
   - Sử dụng `context.AdditionalData` để share data giữa rules
   - Sử dụng `context.ProcessingTime` thay vì DateTime.UtcNow

4. **Logging:**
   - Log warnings khi rule fails
   - Log info khi có hành động quan trọng
   - Tránh log quá nhiều trong production

## 🔄 Scheduled Maintenance

Để chạy maintenance tự động, có thể:

1. **Sử dụng Heroku Scheduler:**
```bash
heroku addons:create scheduler:standard
heroku addons:open scheduler
```

Thêm job:
```bash
curl -X POST https://your-app.herokuapp.com/api/activesignals/maintenance \
  -H "X-API-Key: your-api-key"
```

2. **Background Service trong .NET:**
Tạo `IHostedService` để chạy maintenance định kỳ.

## 📊 Monitoring

Check logs để monitor rule execution:
```bash
heroku logs --tail -a your-app-name | grep "Rule"
```

## 💡 Examples

### Example 1: Tạo rule kiểm tra volume
```csharp
public class VolumeValidationRule : ISignalRule
{
    public string RuleName => "VolumeValidationRule";
    public int Priority => 15;
    
    public Task<RuleResult> ExecuteAsync(ActiveTradingSignal signal, SignalContext context)
    {
        // Get volume from context (if provided by webhook)
        if (context.AdditionalData.TryGetValue("volume", out var volumeObj))
        {
            var volume = Convert.ToDouble(volumeObj);
            if (volume < 1000)
            {
                return Task.FromResult(RuleResult.Failure("Volume too low"));
            }
        }
        
        return Task.FromResult(RuleResult.Success());
    }
}
```

### Example 2: Rule gửi notification
```csharp
public class NotificationRule : ISignalRule
{
    private readonly INotificationService _notificationService;
    
    public string RuleName => "NotificationRule";
    public int Priority => 100; // Run last
    
    public async Task<RuleResult> ExecuteAsync(ActiveTradingSignal signal, SignalContext context)
    {
        // Send to Discord/Telegram
        await _notificationService.SendAsync($"New signal: {signal.Symbol} {signal.Action}");
        
        return RuleResult.Success();
    }
}
```

## 🚀 Tips

- Rule nên stateless - không lưu state giữa các lần execute
- Sử dụng dependency injection cho services
- Test từng rule độc lập trước khi integrate
- Monitor performance - rule không nên chạy quá lâu
