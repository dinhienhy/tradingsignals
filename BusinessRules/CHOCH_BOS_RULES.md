# EntryCHoCH & EntryBOS Business Rules

## 📚 Tổng quan

Hai rules này xử lý signals từ Smart Money Concepts (SMC) trading strategy:
- **EntryCHoCH** - Change of Character (Thay đổi xu hướng)
- **EntryBOS** - Break of Structure (Phá vỡ cấu trúc)

## 🎯 EntryCHoCH Rule

### Mục đích
Validate và xử lý signals CHoCH - điểm đảo chiều xu hướng quan trọng.

### Logic xử lý

**1. Xác nhận đảo chiều (Reversal Confirmation)**
- Tìm signal ngược chiều gần nhất (trong 1 giờ)
- Nếu tìm thấy → Xác nhận CHoCH hợp lệ
- Auto-resolve signal cũ vì xu hướng đã thay đổi
- Kế thừa Swing level từ điểm đảo chiều

**2. Phát hiện thị trường choppy**
- Đếm số lượng CHoCH signals trong 2 giờ qua
- Nếu ≥ 3 signals → Market choppy, reject signal mới
- Lý do: Quá nhiều CHoCH = không có xu hướng rõ ràng

**3. Validate price movement**
- CHoCH phải có price movement hợp lý:
  - Tối thiểu: 0.05% (5 pips cho Forex)
  - Tối đa: 2.0% (nếu lớn hơn → có thể là news event)

**4. Auto-expire old CHoCH**
- CHoCH là short-term signals
- Auto-resolve signals > 4 giờ

### Example

```json
{
  "symbol": "EURUSD",
  "action": "BUY",
  "price": 1.05500,
  "type": "EntryCHoCH",
  "swing": "1.05000",
  "timestamp": "2025-10-16T10:00:00Z"
}
```

**Kịch bản:**
1. Trước đó có SELL CHoCH tại 1.06000
2. Giờ có BUY CHoCH tại 1.05500
3. Rule sẽ:
   - ✅ Xác nhận đảo chiều từ SELL → BUY
   - ✅ Mark SELL signal cũ = Resolved
   - ✅ Set Swing = 1.05000 (điểm đảo chiều)

---

## 🎯 EntryBOS Rule

### Mục đích
Validate và xử lý signals BOS - điểm phá vỡ cấu trúc, xác nhận tiếp tục xu hướng.

### Logic xử lý

**1. Kiểm tra alignment với trend**
- Tìm CHoCH ngược chiều gần nhất (trong 30 phút)
- Nếu có → BOS không hợp lệ (trend đã đảo chiều)
- Reject signal vì mâu thuẫn với CHoCH

**2. Validate tần suất BOS**
- BOS cùng chiều quá gần (< 15 phút) → Reject (duplicate)
- BOS cùng chiều trong 2 giờ → Xác nhận strong trend
- Kế thừa Swing level từ BOS trước để nhất quán

**3. Validate price broke structure**
- BUY BOS: Price phải > Swing level
- SELL BOS: Price phải < Swing level
- Nếu không phá vỡ level → Reject signal
- Log % break để monitor

**4. Auto-detect swing nếu không có**
- Nếu webhook không gửi Swing:
  - BUY BOS: Tìm recent high và break nó
  - SELL BOS: Tìm recent low và break nó

**5. Check directional bias**
- Đếm signals cùng chiều trong 4 giờ
- Nếu nhiều → Strong directional bias

**6. Auto-expire old BOS**
- BOS là longer-term hơn CHoCH
- Auto-resolve signals > 8 giờ

### Example

```json
{
  "symbol": "GBPUSD",
  "action": "SELL",
  "price": 1.24500,
  "type": "EntryBOS",
  "swing": "1.25000",
  "timestamp": "2025-10-16T10:00:00Z"
}
```

**Kịch bản:**
1. Swing level (resistance) tại 1.25000
2. Price break xuống 1.24500 (SELL BOS)
3. Rule sẽ:
   - ✅ Validate price đã break swing (1.24500 < 1.25000)
   - ✅ Tính % break = 0.4%
   - ✅ Check không có opposite CHoCH gần đây
   - ✅ Xác nhận BOS hợp lệ

---

## 🔄 Interaction giữa CHoCH và BOS

### Scenario 1: Strong Uptrend
```
1. CHoCH BUY    → Đảo chiều lên
2. BOS BUY      → Xác nhận uptrend
3. BOS BUY      → Tiếp tục uptrend
4. CHoCH SELL   → Đảo chiều xuống (resolve tất cả BOS BUY)
```

### Scenario 2: Invalid BOS (Conflict)
```
1. CHoCH SELL   → Đảo chiều xuống (10:00)
2. BOS BUY      → REJECTED (10:15) - Mâu thuẫn với CHoCH SELL
```

### Scenario 3: Choppy Market
```
1. CHoCH BUY    → Đảo chiều lên
2. CHoCH SELL   → Đảo chiều xuống (15 min sau)
3. CHoCH BUY    → Đảo chiều lên (20 min sau)
4. CHoCH SELL   → REJECTED - Quá nhiều CHoCH = choppy
```

---

## 📊 Rule Priorities

```
Priority 5:  PriceValidationRule    → Validate giá trị cơ bản
Priority 10: DuplicateSignalRule    → Ngăn duplicate
Priority 20: SwingDetectionRule     → Auto-detect swing
Priority 25: EntryCHoCHRule         → Validate CHoCH logic
Priority 25: EntryBOSRule           → Validate BOS logic
Priority 50: SignalExpirationRule   → Clean up
```

CHoCH và BOS chạy cùng priority (25) vì:
- Không conflict với nhau
- Mỗi rule chỉ xử lý type của mình
- Đều cần basic validation pass trước

---

## 🧪 Testing

### Test EntryCHoCH

```bash
curl -X POST https://tradingsignals.herokuapp.com/api/activesignals/validate \
  -H "Content-Type: application/json" \
  -H "X-API-Key: kyuoj1KRGILRy4Le9i8NtXGDdFIspy07" \
  -d '{
    "symbol": "EURUSD",
    "action": "BUY",
    "price": 1.05500,
    "type": "EntryCHoCH",
    "swing": "1.05000",
    "timestamp": "2025-10-16T10:00:00Z"
  }'
```

### Test EntryBOS

```bash
curl -X POST https://tradingsignals.herokuapp.com/api/activesignals/validate \
  -H "Content-Type: application/json" \
  -H "X-API-Key: kyuoj1KRGILRy4Le9i8NtXGDdFIspy07" \
  -d '{
    "symbol": "GBPUSD",
    "action": "SELL",
    "price": 1.24500,
    "type": "EntryBOS",
    "swing": "1.25000",
    "timestamp": "2025-10-16T10:00:00Z"
  }'
```

### Test với PowerShell

```powershell
cd d:\Workspace\CascadeProjects\BoxTradeDiscord\tradingsignals
.\test-business-rules.ps1 -BaseUrl "https://tradingsignals.herokuapp.com" -ApiKey "kyuoj1KRGILRy4Le9i8NtXGDdFIspy07"
```

---

## 📝 Webhook Configuration

Đảm bảo đã tạo 2 webhook paths trong hệ thống:

**1. EntryCHoCH Webhook**
```
Path: EntryCHoCH
Secret: your-secret-key
Description: Change of Character signals
```

**2. EntryBOS Webhook**
```
Path: EntryBOS
Secret: your-secret-key
Description: Break of Structure signals
```

---

## 🎓 SMC Trading Concepts

### Change of Character (CHoCH)
- Điểm thị trường thay đổi từ uptrend → downtrend hoặc ngược lại
- Là dấu hiệu sớm nhất của reversal
- High probability entries khi kết hợp với Order Blocks

### Break of Structure (BOS)
- Price phá vỡ High/Low quan trọng
- Xác nhận trend continuation
- Tìm kiếm pullback entries sau BOS

### Swing Levels
- High/Low quan trọng trước đó
- Là reference point cho CHoCH và BOS
- Được sử dụng để tính toán breakout

---

## 💡 Best Practices

1. **Luôn gửi Swing field**
   - Giúp rules validate chính xác hơn
   - Tránh auto-detection không chính xác

2. **Timestamp chính xác**
   - Dùng UTC time
   - Format: ISO 8601

3. **Type field phải match webhook path**
   - EntryCHoCH → type = "EntryCHoCH"
   - EntryBOS → type = "EntryBOS"

4. **Monitor rejected signals**
   - Check logs để hiểu tại sao bị reject
   - Adjust strategy nếu cần

5. **Combine với price action**
   - CHoCH + Order Block = High probability
   - BOS + Pullback = Continuation entry

---

## 🔧 Customization

Để adjust parameters:

**EntryCHoCH:**
```csharp
// File: BusinessRules/Rules/EntryCHoCHRule.cs

// Thay đổi choppy detection (default: 3 signals trong 2h)
if (recentChochCount >= 3) // <- Adjust số này

// Thay đổi expiration (default: 4 giờ)
if (age.TotalHours > 4) // <- Adjust time này
```

**EntryBOS:**
```csharp
// File: BusinessRules/Rules/EntryBOSRule.cs

// Thay đổi conflict window (default: 30 phút)
if (timeSinceChoch.TotalMinutes < 30) // <- Adjust time này

// Thay đổi duplicate window (default: 15 phút)
if (timeSinceLastBOS.TotalMinutes < 15) // <- Adjust time này

// Thay đổi expiration (default: 8 giờ)
if (age.TotalHours > 8) // <- Adjust time này
```

Deploy sau khi thay đổi:
```bash
git add .
git commit -m "Adjust CHoCH/BOS parameters"
git push heroku main
```
