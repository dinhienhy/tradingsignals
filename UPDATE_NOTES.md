# Cập nhật ActiveTradingSignal - Thêm Swing và Resolved Fields

## Tổng quan

Đã thêm 2 field mới vào model `ActiveTradingSignal`:

1. **Swing** - Giá swing từ TradingView (kiểu `decimal`, nullable)
2. **Resolved** - Đánh dấu tín hiệu đã được xử lý (kiểu `boolean`, mặc định `false`)

## Thay đổi Model

### ActiveTradingSignal

```csharp
public class ActiveTradingSignal
{
    // ... các field cũ ...
    
    // Swing price level từ TradingView
    public decimal? Swing { get; set; }
    
    // Flag đánh dấu tín hiệu đã được xử lý
    public bool Resolved { get; set; }  // Mặc định: false
}
```

## Cách nhận dữ liệu

### Từ TradingView Webhook

Field `Swing` và `Resolved` sẽ được cập nhật tự động từ webhook TradingView:

**Webhook Payload Example:**
```json
{
  "secret": "your-webhook-secret",
  "symbol": "EURUSD",
  "action": "BUY",
  "price": 1.0545,
  "swing": 1.0500,
  "timestamp": "2025-10-16T08:30:00Z",
  "message": "Strong bullish signal"
}
```

**Lưu ý:**
- Field `swing` là optional - nếu không có sẽ lưu là `null`
- Khi có tín hiệu mới cho cùng Symbol+Type:
  - `Swing` được cập nhật từ payload
  - `Resolved` tự động reset về `false`
  - `Used` tự động reset về `false`

## Cấu trúc Database

### Bảng ActiveTradingSignals

Các cột đã thêm:
- `Swing` - TEXT (nullable) - Lưu giá swing từ TradingView
- `Resolved` - INTEGER (0/1, mặc định 0) - Boolean flag trong database

Migration: `20251016083342_AddSwingAndResolvedFields`

## Ví dụ sử dụng

### 1. Gửi tín hiệu từ TradingView

```bash
curl -X POST "https://your-domain.com/webhook/forex" \
  -H "Content-Type: application/json" \
  -d '{
    "secret": "your-secret",
    "symbol": "EURUSD",
    "action": "BUY",
    "price": 1.0545,
    "swing": 1.0500,
    "timestamp": "2025-10-16T08:30:00Z"
  }'
```

### 2. Lấy tất cả active signals

```bash
curl -X GET "https://your-domain.com/api/activesignals" \
  -H "X-API-Key: your-api-key"
```

**Response:**
```json
[
  {
    "id": 1,
    "symbol": "EURUSD",
    "action": "BUY",
    "price": 1.0545,
    "swing": 1.0500,
    "timestamp": "2025-10-16T08:30:00Z",
    "type": "forex",
    "uniqueKey": "EURUSD_forex",
    "used": false,
    "resolved": false
  }
]
```

### 3. Đánh dấu signal đã sử dụng

```bash
curl -X PUT "https://your-domain.com/api/activesignals/markused/1" \
  -H "X-API-Key: your-api-key"
```

## Logic xử lý

### Khi nhận webhook mới:

1. System kiểm tra xem đã có active signal cho Symbol+Type này chưa
2. **Nếu đã có:**
   - Cập nhật `Action`, `Price`, `Timestamp`, `Swing`
   - Reset `Used` = `false`
   - Reset `Resolved` = `false`
3. **Nếu chưa có:**
   - Tạo mới với các giá trị từ webhook
   - `Resolved` mặc định = `false`

### Workflow điển hình:

1. TradingView gửi signal → System nhận và lưu vào `ActiveTradingSignals`
2. MT5 bot lấy signal chưa used → Process signal
3. MT5 bot đánh dấu `Used` = `true` sau khi xử lý
4. Người dùng/system có thể đánh dấu `Resolved` = `true` khi hoàn tất

## API Endpoints không thay đổi

Các endpoint hiện có vẫn hoạt động bình thường:
- `GET /api/activesignals` - Lấy tất cả signals
- `GET /api/activesignals/bytype/{type}` - Lấy signals theo type
- `GET /api/activesignals/unused` - Lấy signals chưa used
- `PUT /api/activesignals/markused/{id}` - Đánh dấu đã used

## Migration đã áp dụng

```sql
ALTER TABLE "ActiveTradingSignals" ADD "Resolved" INTEGER NOT NULL DEFAULT 0;
ALTER TABLE "ActiveTradingSignals" ADD "Swing" TEXT NULL;
```

---

**Ngày cập nhật:** 16/10/2025  
**Version:** 1.1.0
