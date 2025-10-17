# Signal Monitoring Background Service

## 📚 Tổng quan

Background service tự động chạy mỗi **1 phút** để:
- ✅ Quan sát tất cả Active Trading Signals
- ✅ Auto-resolve signals hết hạn
- ✅ Phát hiện conflicts và patterns
- ✅ Update trạng thái signals

## 🔄 Luồng hoạt động

```
TradingView → Webhook → Save to DB
                            ↓
                    [Active Signals]
                            ↓
      ┌─────────────────────┴─────────────────────┐
      │                                            │
      │   Background Service (Every 1 minute)     │
      │                                            │
      │   1. Load active signals (Resolved = 0)   │
      │   2. Group by Type & Symbol               │
      │   3. Process each group:                  │
      │      - EntryCHoCH signals                 │
      │      - EntryBOS signals                   │
      │      - Generic signals                    │
      │   4. Auto-resolve expired signals         │
      │   5. Detect conflicts                     │
      │   6. Save changes to DB                   │
      │                                            │
      └─────────────────────┬─────────────────────┘
                            ↓
                    [Updated Signals]
```

## 🎯 Processing Rules

### 1️⃣ EntryCHoCH Signals

**Auto-Resolve:**
- ⏱️ Signals > 4 hours old → Mark as Resolved

**Reversal Detection:**
- 🔄 Nếu có CHoCH ngược chiều trong vòng 1 giờ
- ✅ Mark CHoCH cũ = Resolved
- 📝 Log: "Resolved CHoCH due to reversal"

**Example:**
```
10:00 - CHoCH SELL (EURUSD)
10:30 - CHoCH BUY  (EURUSD) → Resolve SELL signal
```

### 2️⃣ EntryBOS Signals

**Auto-Resolve:**
- ⏱️ Signals > 8 hours old → Mark as Resolved

**Conflict Detection:**
- 🔍 Check recent CHoCH signals (within 30 min)
- ⚠️ Nếu có opposite CHoCH → Resolve BOS
- 📝 Log: "Resolved BOS due to opposite CHoCH"

**Example:**
```
10:00 - BOS BUY (GBPUSD)
10:20 - CHoCH SELL (GBPUSD) → Resolve BOS BUY (trend đảo chiều)
```

### 3️⃣ Generic Signals (Other Types)

**Auto-Resolve:**
- ⏱️ Signals > 24 hours old → Mark as Resolved

## 📊 Monitoring & Logs

### Log Messages

**Service Lifecycle:**
```
[INFO] Signal Monitoring Service started
[INFO] Starting signal processing cycle...
[INFO] Found 15 active unresolved signals
[INFO] Processing 5 signals for type: entrychoch
[INFO] Processing complete. Resolved: 3, Updated: 0
```

**Signal Processing:**
```
[INFO] Auto-resolved old CHoCH: EURUSD BUY, age: 4.2h
[INFO] Resolved CHoCH due to reversal: GBPUSD SELL
[INFO] Resolved BOS due to opposite CHoCH: USDJPY BUY
```

### Heroku Logs

View real-time:
```bash
heroku logs --tail -a tradingsignals | grep "SignalMonitoring"
```

Filter specific type:
```bash
heroku logs --tail -a tradingsignals | grep "CHoCH"
heroku logs --tail -a tradingsignals | grep "BOS"
```

## 🛠️ Configuration

### Change Interval

Edit `SignalMonitoringService.cs`:
```csharp
private readonly TimeSpan _interval = TimeSpan.FromMinutes(1); // Change here
```

Options:
- `TimeSpan.FromSeconds(30)` - Every 30 seconds
- `TimeSpan.FromMinutes(5)` - Every 5 minutes
- `TimeSpan.FromHours(1)` - Every hour

### Change Expiration Times

**EntryCHoCH:**
```csharp
if (age.TotalHours > 4) // Change from 4 hours
```

**EntryBOS:**
```csharp
if (age.TotalHours > 8) // Change from 8 hours
```

**Generic:**
```csharp
if (age.TotalHours > 24) // Change from 24 hours
```

## 🎨 Customization

### Add New Signal Type

Ví dụ: Thêm xử lý cho `EntryOB` (Order Block):

```csharp
switch (type)
{
    case "entrychoch":
        await ProcessCHoCHSignalsAsync(...);
        break;
        
    case "entrybos":
        await ProcessBOSSignalsAsync(...);
        break;
        
    case "entryob":  // NEW
        await ProcessOrderBlockSignalsAsync(...);
        break;
        
    default:
        await ProcessGenericSignalsAsync(...);
        break;
}
```

Implement method:
```csharp
private async Task ProcessOrderBlockSignalsAsync(
    AppDbContext context, 
    List<ActiveTradingSignal> signals, 
    ref int resolvedCount, 
    ref int updatedCount)
{
    var now = DateTime.UtcNow;
    
    // Your custom logic here
    foreach (var signal in signals)
    {
        var age = now - signal.Timestamp;
        if (age.TotalHours > 12) // Custom expiration
        {
            signal.Resolved = 1;
            resolvedCount++;
        }
    }
}
```

### Add Custom Monitoring Logic

Ví dụ: Detect strong trends:

```csharp
// In ProcessBOSSignalsAsync, add:
var bosCount = symbolSignals
    .Where(s => s.Action == "BUY" && s.Resolved == 0)
    .Count();

if (bosCount >= 3)
{
    _logger.LogWarning("Strong BUY trend detected for {Symbol}: {Count} active BOS signals", 
        symbolGroup.Key, bosCount);
    
    // Could trigger notification, update field, etc.
}
```

## 📈 Performance

### Resource Usage
- **Memory:** ~10-50 MB per cycle
- **CPU:** Minimal (runs for 1-2 seconds every minute)
- **Database:** 1-2 queries per minute

### Optimization Tips

1. **Limit query size:**
```csharp
var activeSignals = await context.ActiveTradingSignals
    .Where(s => s.Resolved == 0)
    .Take(1000) // Limit results
    .OrderByDescending(s => s.Timestamp)
    .ToListAsync();
```

2. **Add indexes to database:**
```sql
CREATE INDEX idx_resolved ON "ActiveTradingSignals"("Resolved");
CREATE INDEX idx_type ON "ActiveTradingSignals"("Type");
CREATE INDEX idx_timestamp ON "ActiveTradingSignals"("Timestamp");
```

3. **Batch processing:**
```csharp
// Process in batches of 100
for (int i = 0; i < signals.Count; i += 100)
{
    var batch = signals.Skip(i).Take(100);
    // Process batch
    await context.SaveChangesAsync(); // Save per batch
}
```

## 🧪 Testing

### Test Locally

1. **Run application:**
```bash
dotnet run
```

2. **Send test webhook:**
```bash
curl -X POST http://localhost:5000/webhook/EntryCHoCH \
  -H "Content-Type: application/json" \
  -d '{
    "secret": "your-secret",
    "symbol": "EURUSD",
    "action": "BUY",
    "price": 1.05500
  }'
```

3. **Wait 1 minute** → Check logs
4. **Wait 4+ hours** → Signal should auto-resolve

### Test on Heroku

```bash
# View logs
heroku logs --tail -a tradingsignals

# Send webhook
curl -X POST https://tradingsignals.herokuapp.com/webhook/EntryCHoCH \
  -H "Content-Type: application/json" \
  -d '{"secret":"xxx","symbol":"EURUSD","action":"BUY","price":1.05500}'

# Check processing after 1 minute
```

### Manual Trigger (for testing)

Create endpoint để manually trigger (chỉ dùng cho testing):

```csharp
// In SignalMonitoringService or new controller
[HttpPost("api/admin/process-signals")]
public async Task<IActionResult> ManualProcess()
{
    // Trigger processing immediately
    await ProcessSignalsAsync();
    return Ok(new { message = "Processing triggered" });
}
```

## 🚨 Troubleshooting

### Service không chạy

**Check logs:**
```bash
heroku logs --tail -a tradingsignals | grep "Service started"
```

**Expected:**
```
Signal Monitoring Service started
```

**If not found:** Service registration có vấn đề. Check Program.cs.

### Signals không được resolved

**Check logs:**
```bash
heroku logs --tail -a tradingsignals | grep "Found.*active"
```

**Expected:**
```
Found 10 active unresolved signals
```

**If "Found 0":** Không có signals hoặc tất cả đã resolved.

### Performance issues

**Check cycle time:**
```bash
heroku logs --tail -a tradingsignals | grep "Processing complete"
```

**Expected:** < 2 seconds per cycle

**If > 5 seconds:** 
- Too many signals → Add pagination
- Database slow → Add indexes
- Complex logic → Optimize queries

## 📝 Best Practices

1. **Keep it simple**
   - Mỗi cycle nên < 5 seconds
   - Tránh complex calculations

2. **Log important events**
   - Signal resolved
   - Conflicts detected
   - Errors

3. **Handle errors gracefully**
   - Try-catch trong ExecuteAsync
   - Service sẽ tự động retry sau 1 phút

4. **Database transactions**
   - SaveChanges() một lần cuối mỗi cycle
   - Không save trong loops

5. **Monitor resource usage**
   ```bash
   heroku ps -a tradingsignals
   ```

## 🔮 Future Enhancements

### Ideas to implement:

1. **Notification System**
   - Send Discord/Telegram alert khi detect pattern
   - Alert khi signals conflict

2. **Price Tracking**
   - Fetch current price từ broker API
   - Compare với signal price
   - Auto-resolve nếu price đã hit target

3. **Statistics**
   - Track success rate
   - Win/loss ratio
   - Store in separate table

4. **Machine Learning**
   - Predict signal quality
   - Auto-filter weak signals

5. **Web Dashboard**
   - Real-time monitoring UI
   - Manual intervention controls
   - Statistics charts

## 📞 Support

Check documentation:
- `README.md` - Main documentation
- `BusinessRules/CHOCH_BOS_RULES.md` - Strategy rules (deprecated, now in service)

Questions? Check logs first:
```bash
heroku logs --tail -a tradingsignals
```
