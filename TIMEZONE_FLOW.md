# Timezone Flow Documentation

## 📅 Timezone Strategy: UTC Storage, GMT+7 Display

### Overview
All timestamps in the system are stored in **UTC** and displayed in **GMT+7** (Indochina Time) for user convenience.

---

## 🔄 Timezone Flow

### 1. **Webhook Input (TradingView → API)**
**Location:** `WebhookController.cs`

```csharp
// Line 76-103: Extract timestamp from payload
DateTime signalTimestamp = DateTime.UtcNow;  // Default to current UTC

if (payload contains "timestamp") {
    // Parse from ISO 8601 string → convert to UTC
    parsedTimestamp.ToUniversalTime()
    
    // OR parse from Unix timestamp → already UTC
    DateTimeOffset.FromUnixTimeSeconds(unixTs).UtcDateTime
}
```

**Result:** `signalTimestamp` is always UTC (`DateTime.Kind = Utc`)

---

### 2. **Database Storage (PostgreSQL)**
**Column Type:** `timestamp with time zone`

**What happens:**
- PostgreSQL stores all timestamps in UTC internally
- The `with time zone` annotation ensures proper conversion
- Entity Framework Core maps `DateTime` (UTC) ↔ PostgreSQL timestamp

**Example:**
```
User sends: "2025-10-18T10:30:00+07:00"
Converted to: "2025-10-18T03:30:00Z" (UTC)
Stored in DB: 2025-10-18 03:30:00+00
```

---

### 3. **MetaAPI Price Time**
**Location:** `MetaApiService.cs`

**MetaAPI returns 2 time fields:**
```json
{
  "time": "2020-04-07T03:45:23.345Z",     // UTC time with 'Z'
  "brokerTime": "2020-04-07 06:45:23.345" // Broker local time (string)
}
```

**Code handling:**
```csharp
// Line 72-80: Ensure MetaAPI Time is UTC
DateTime priceTime = DateTime.UtcNow;
if (priceData.Time.HasValue) {
    // MetaAPI returns ISO 8601 UTC: "2025-10-17T20:57:58.000Z"
    // Handle unspecified DateTimeKind
    priceTime = priceData.Time.Value.Kind == DateTimeKind.Unspecified 
        ? DateTime.SpecifyKind(priceData.Time.Value, DateTimeKind.Utc)
        : priceData.Time.Value.ToUniversalTime();
}

// BrokerTime is stored for reference/logging only
price.BrokerTime = priceData.BrokerTime;
```

**Result:** 
- `priceTime` is always UTC (used for calculations)
- `BrokerTime` is stored for reference (shows broker's local timezone)

**Example:** If broker is GMT+3:
- `time`: "2020-04-07T03:45:23.345Z" (UTC)
- `brokerTime`: "2020-04-07 06:45:23.345" (GMT+3)
- Time difference: 3 hours

---

### 4. **SignalMonitoringService Processing**
**Location:** `SignalMonitoringService.cs`

```csharp
// Line 57: Current time in UTC
var now = DateTime.UtcNow;

// Line 187: Calculate age (both timestamps are UTC)
var age = now - signal.Timestamp;

// Line 209: Calculate time difference (both timestamps are UTC)
var timeDiff = newest.Timestamp - older.Timestamp;
```

**Important:** All time calculations use UTC to avoid timezone issues

---

### 5. **API Response (JSON)**
**Location:** `Program.cs`

```csharp
// JSON Serializer Configuration
options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
```

**Result:** DateTime serializes as ISO 8601 UTC
```json
{
  "timestamp": "2025-10-18T03:30:00Z",  // UTC with 'Z' suffix
  "time": "2025-10-18T03:30:00Z"
}
```

---

### 6. **Frontend Display (GMT+7)**
**Location:** `wwwroot/js/site.js`

```javascript
// Line 72-87: Format timestamp to GMT+7
function formatTimestampGMT7(timestamp) {
    const date = new Date(timestamp);  // Parse ISO 8601 UTC string
    
    return date.toLocaleString('vi-VN', {
        timeZone: 'Asia/Bangkok',  // GMT+7 (Indochina Time)
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: false
    });
}
```

**Example Conversion:**
```
API returns:  "2025-10-18T03:30:00Z"  (UTC)
Display:      "18/10/2025, 10:30:00"  (GMT+7)
```

---

## ✅ Benefits

1. **Consistent Storage:** All timestamps stored in UTC (no timezone confusion)
2. **Accurate Calculations:** Time difference calculations always correct
3. **User-Friendly Display:** Users see timestamps in their local timezone (GMT+7)
4. **MetaAPI Compatibility:** Price timestamps from MetaAPI match signal timestamps
5. **Cross-Region Support:** Easy to add support for other timezones in the future

---

## 🔧 Testing Timezone Consistency

### Test Script: `test-timezone-flow.ps1`

```powershell
# 1. Send signal with explicit timestamp (GMT+7)
$payload = @{
    timestamp = "2025-10-18T10:30:00+07:00"  # 10:30 AM GMT+7
    # ... other fields
}

# Expected in database: 2025-10-18 03:30:00+00 (UTC)
# Expected in frontend: 18/10/2025, 10:30:00 (GMT+7)
```

### Verification Checklist

- [ ] Webhook receives timestamp in any timezone → converts to UTC
- [ ] Database stores timestamp as UTC (`timestamp with time zone`)
- [ ] MetaAPI price time is UTC
- [ ] SignalMonitoringService uses UTC for all time calculations
- [ ] API response returns ISO 8601 UTC with 'Z' suffix
- [ ] Frontend displays timestamps in GMT+7

---

## 📝 Important Notes

1. **Never use `DateTime.Now`** - always use `DateTime.UtcNow`
2. **Always specify `DateTimeKind.Utc`** when creating DateTime objects
3. **PostgreSQL migration:** Ensure column type is `timestamp with time zone`
4. **Frontend timezone:** Can be changed by modifying `timeZone` in `formatTimestampGMT7()`

---

## 🌍 Broker Timezone Detection

MetaAPI provides both UTC and broker local time, allowing us to determine broker timezone:

### Automatic Detection
```
UTC Time:    "2020-04-07T03:45:23.345Z"
Broker Time: "2020-04-07 06:45:23.345"
Difference:  +3 hours → Broker is GMT+3
```

### Common Broker Timezones
- **GMT+2/+3:** European brokers (Cyprus, Russia)
- **GMT+0:** London brokers
- **GMT-5:** New York brokers  
- **GMT+8:** Singapore/Hong Kong brokers

### Verification in Logs
Check ServiceLogs for MetaAPI fetch:
```
Price fetched for XAUUSD: 
  UTC=2025-10-18 03:30:00
  BrokerTime=2025-10-18 06:30:00 
  → Broker is GMT+3
```

---

## 🐛 Common Issues & Solutions

### Issue 1: Time calculations are wrong
**Cause:** Mixing UTC and local time
**Solution:** Ensure all DateTime objects have `Kind = Utc`

### Issue 2: Frontend shows wrong time
**Cause:** API not sending UTC with 'Z' suffix
**Solution:** Check JSON serializer config in `Program.cs`

### Issue 3: MetaAPI time doesn't match signal time
**Cause:** Unspecified `DateTimeKind` from JSON deserialize
**Solution:** Use `DateTime.SpecifyKind()` in `MetaApiService.cs`

### Issue 4: Broker time seems wrong
**Cause:** Broker timezone differs from expected
**Solution:** Check MetaAPI logs for actual broker timezone. BrokerTime is for reference only - always use UTC for calculations

---

## 🚀 Future Enhancements

1. Add user preference for timezone selection
2. Support multiple timezone displays per user
3. Add timezone info to API documentation
4. Create timezone conversion utilities
