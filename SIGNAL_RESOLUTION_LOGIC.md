# Signal Resolution Logic

## 📋 Overview

SignalMonitoringService tự động xử lý và resolve các trading signals dựa trên price action và swing levels.

---

## 🔄 Signal Resolution Rules

### CHoCH (Change of Character) Resolution Rules

### Rule 1: Opposite Action Matching

CHoCH signals chỉ được resolve khi có BOS signal với **OPPOSITE action** trước đó:

- **CHoCH BUY** → Tìm **BOS SELL** trước đó
- **CHoCH SELL** → Tìm **BOS BUY** trước đó

### Rule 2: Price Break Conditions

**Scenario 1: CHoCH BUY + BOS SELL**
```
Timeline: BOS SELL (earlier) → CHoCH BUY (later)

BOS SELL Swing: 2650.00
CHoCH BUY enters at: 2645.00

Resolution: CHoCH BUY resolved khi price > 2650.00 (breaks above BOS SELL Swing)

Ý nghĩa: Trend đã thay đổi, giá vượt qua swing cũ
```

**Scenario 2: CHoCH SELL + BOS BUY**
```
Timeline: BOS BUY (earlier) → CHoCH SELL (later)

BOS BUY Swing: 2650.00
CHoCH SELL enters at: 2655.00

Resolution: CHoCH SELL resolved khi price < 2650.00 (breaks below BOS BUY Swing)

Ý nghĩa: Trend đã thay đổi, giá phá vỡ swing cũ
```

### Rule 3: Price Calculation

**Mid Price = (Bid + Ask) / 2**

Sử dụng giá trung bình (Mid Price) để so sánh với Swing levels:
- Bid: 2651.50
- Ask: 2651.52
- **Mid: 2651.51** ← Dùng giá này

---

## 📊 BOS (Break of Structure) Resolution Rules

### Rule 1: CHoCH Dependency

BOS signals tự động resolve khi có CHoCH **SAU BOS** đã resolved:

```
Timeline:
10:00 - BOS SELL (Swing: 2650.00) ← Trước
10:05 - CHoCH BUY (Price: 2645.00) ← Sau
10:15 - Price breaks to 2651.00 → CHoCH BUY resolved

→ BOS SELL cũng tự động resolved (vì CHoCH sau nó đã resolved)
```

**Logic:**
- Find all CHoCH signals với `Timestamp > BOS.Timestamp`
- Nếu bất kỳ CHoCH nào đã `Resolved = true` → BOS cũng `Resolved = true`

### Rule 2: Price Break Conditions

Nếu không có CHoCH resolved, check price break:

**Scenario 1: BOS SELL**
```
BOS SELL Swing: 2650.00
Current Price: 2651.51

Resolution: BOS SELL resolved khi price > 2650.00
Ý nghĩa: Giá đã phá vỡ swing level
```

**Scenario 2: BOS BUY**
```
BOS BUY Swing: 2650.00
Current Price: 2648.50

Resolution: BOS BUY resolved khi price < 2650.00
Ý nghĩa: Giá đã phá vỡ swing level
```

### Rule 3: Auto-Expire (8 Hours)

BOS signals tự động resolve sau 8 giờ:

```csharp
var age = now - signal.Timestamp;
if (age.TotalHours > 8) {
    signal.Resolved = true;
}
```

---

## ⏰ Auto-Resolution Rules

### Time-based Resolution (4 Hours)

CHoCH signals tự động resolve sau 4 giờ nếu không có price break:

```csharp
var age = now - signal.Timestamp;
if (age.TotalHours > 4) {
    signal.Resolved = true;
}
```

### Trend Reversal Detection

CHoCH signals tự động resolve khi có CHoCH ngược chiều trong vòng 1 giờ:

```
10:00 - CHoCH BUY
10:30 - CHoCH SELL (opposite)

→ CHoCH BUY (10:00) auto-resolved
```

---

## 📊 Processing Flow

```
┌─────────────────────────────────────────────────────┐
│        Signal Monitoring Service (Every 60s)        │
├─────────────────────────────────────────────────────┤
│                                                      │
│  1. Query Active Signals (Resolved = 0)            │
│     ↓                                                │
│                                                      │
│  2. Group by Symbol                                 │
│     ↓                                                │
│                                                      │
│  3. For each Symbol:                                │
│     ├─ Get Current Price from MetaAPI              │
│     ├─ Calculate Mid Price                         │
│     │                                                │
│     ├─ Process CHoCH Signals:                      │
│     │  ├─ Rule 1: Auto-expire (> 4 hours)         │
│     │  ├─ Rule 2: Trend reversal (< 1 hour)       │
│     │  └─ Rule 3: Price break vs BOS Swing        │
│     │                                                │
│     └─ For each unresolved CHoCH:                  │
│        ├─ Find opposite action BOS before CHoCH   │
│        ├─ Compare current price with BOS Swing    │
│        └─ Resolve if condition met               │
│                                                      │
│  4. Save Changes to Database                        │
│                                                      │
└─────────────────────────────────────────────────────┘
```

---

## 🎯 Code Examples

### CHoCH: Finding Opposite Action BOS

```csharp
// CHoCH BUY → look for BOS SELL
// CHoCH SELL → look for BOS BUY
string oppositeAction = chochSignal.Action == "BUY" ? "SELL" : "BUY";

var relevantBOS = bosSignals
    .Where(b => b.Timestamp < chochSignal.Timestamp 
             && b.Action == oppositeAction)
    .FirstOrDefault();
```

### CHoCH: Price Break Check

```csharp
var price = currentPrice.MidPrice;
var bosSwing = relevantBOS.Swing.Value;

// CHoCH BUY + BOS SELL: Resolve if price > swing
if (chochSignal.Action == "BUY" && relevantBOS.Action == "SELL" && price > bosSwing)
{
    chochSignal.Resolved = true;
}

// CHoCH SELL + BOS BUY: Resolve if price < swing
else if (chochSignal.Action == "SELL" && relevantBOS.Action == "BUY" && price < bosSwing)
{
    chochSignal.Resolved = true;
}
```

### BOS: CHoCH Dependency Check

```csharp
// Get all CHoCH signals that came AFTER this BOS
var subsequentChochs = allChochSignals
    .Where(c => c.Timestamp > bosSignal.Timestamp)
    .ToList();

// If any subsequent CHoCH is resolved → BOS is resolved
var resolvedChoch = subsequentChochs.FirstOrDefault(c => c.Resolved);
if (resolvedChoch != null)
{
    bosSignal.Resolved = true;
}
```

### BOS: Price Break Check

```csharp
var price = currentPrice.MidPrice;
var bosSwing = bosSignal.Swing.Value;

// BOS SELL: Resolve if price > swing
if (bosSignal.Action == "SELL" && price > bosSwing)
{
    bosSignal.Resolved = true;
}

// BOS BUY: Resolve if price < swing
else if (bosSignal.Action == "BUY" && price < bosSwing)
{
    bosSignal.Resolved = true;
}
```

---

## 📝 Database Fields

### ActiveTradingSignals Table

| Field      | Type    | Description                              |
|------------|---------|------------------------------------------|
| Id         | int     | Primary key                              |
| Symbol     | string  | Trading pair (e.g., XAUUSD)             |
| Action     | string  | BUY or SELL                             |
| Price      | decimal | Entry price                             |
| Swing      | decimal | Swing level for BOS                     |
| Timestamp  | DateTime| Signal time (UTC)                       |
| Type       | string  | EntryCHoCH or EntryBOS                  |
| **Resolved** | **int** | **0 = Active, 1 = Resolved**          |
| Used       | int     | 0 = Not used, 1 = Used by MT5          |

---

## 🔍 Logging & Debugging

### CHoCH Debug Logs

```
[CHoCH QueryBOS] Found 2 active BOS signals for XAUUSD
  - BOS SELL, Swing: 2650.00, Time: 10:00
  - BOS BUY, Swing: 2645.00, Time: 09:30

[CHoCH CheckPrice] Checking CHoCH BUY XAUUSD: Price=2651.51 vs BOS SELL Swing=2650.00
  CHoCHId: 123
  CHoCHAction: BUY
  BOSAction: SELL
  CurrentPrice: 2651.51
  BOSSwing: 2650.00
  PriceDifference: 1.51

[CHoCH PriceBreakResolved] CHoCH BUY XAUUSD resolved: Price 2651.51 broke above BOS SELL Swing 2650.00
  ✅ Signal #123 resolved
```

### BOS Debug Logs

```
[BOS ProcessStart] Starting to process 3 BOS signals
  TotalSignals: 3
  Symbols: ["XAUUSD", "EURUSD"]

[BOS ProcessSymbol] Processing 2 BOS signals for XAUUSD
  SignalCount: 2
  Actions: ["SELL", "SELL"]
  Swings: [2650.00, 2648.00]

[BOS ResolvedByChoCH] BOS SELL XAUUSD resolved: Subsequent CHoCH BUY is resolved
  BOSId: 456
  BOSAction: SELL
  BOSSwing: 2650.00
  BOSTimestamp: 2025-10-18T03:00:00Z
  CHoCHId: 123
  CHoCHAction: BUY
  CHoCHTimestamp: 2025-10-18T03:05:00Z
  CHoCHResolved: true
  ✅ BOS #456 resolved by CHoCH

[BOS PriceBreakResolved] BOS SELL XAUUSD resolved: Price 2651.51 broke above Swing 2650.00
  BOSId: 457
  BOSAction: SELL
  BOSSwing: 2650.00
  CurrentPrice: 2651.51
  Difference: 1.51
  ✅ BOS #457 resolved by price break
```

### ServiceLogs Table

All resolution events are logged to `ServiceLogs` table:
- Source: "CHoCH"
- Action: "PriceBreakResolved"
- Level: "Info"
- Data: JSON with full details

---

## 🚀 Testing

### Manual Test Script

```powershell
# 1. Create BOS SELL signal
$bosPayload = @{
    secret = "your-secret"
    symbol = "XAUUSD"
    action = "SELL"
    price = 2655.00
    swing = 2650.00
    timestamp = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://your-app.com/webhook/EntryBOS" -Method POST -Body $bosPayload -ContentType "application/json"

# 2. Create CHoCH BUY signal (after BOS)
Start-Sleep -Seconds 2
$chochPayload = @{
    secret = "your-secret"
    symbol = "XAUUSD"
    action = "BUY"
    price = 2645.00
    timestamp = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://your-app.com/webhook/EntryCHoCH" -Method POST -Body $chochPayload -ContentType "application/json"

# 3. Wait for price to break above 2650.00
# Service runs every 60 seconds, will auto-resolve when price > 2650.00

# 4. Check active signals
Invoke-RestMethod -Uri "https://your-app.com/api/active-signals" -Headers @{"X-API-Key"="your-key"}
```

### Expected Behavior

1. BOS SELL created at 2655.00 with Swing 2650.00
2. CHoCH BUY created at 2645.00
3. When price reaches 2651.00 (> 2650.00):
   - CHoCH BUY is resolved
   - Logs show resolution event
   - Frontend shows signal as resolved

---

## ⚠️ Important Notes

1. **Resolved signals are skipped:** `symbolSignals.Where(s => !s.Resolved)`
2. **BOS must come BEFORE CHoCH:** `b.Timestamp < chochSignal.Timestamp`
3. **Only opposite actions match:** CHoCH BUY matches BOS SELL only
4. **Mid Price is used:** Not Bid or Ask individually
5. **UTC timestamps:** All time comparisons in UTC

---

## 🔧 Configuration

### Service Interval

```csharp
private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);
```

Change this to adjust how often signals are checked.

### Auto-Expire Duration

```csharp
if (age.TotalHours > 4)  // Change 4 to desired hours
```

---

## 📈 Future Enhancements

1. Add configurable timeouts per signal type
2. Support multiple BOS swing levels
3. Add manual override for resolution
4. Track resolution reasons in database
5. Add notification when signals are resolved
