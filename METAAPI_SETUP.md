# MetaApi Setup Guide

## 📚 Tổng quan

Service monitoring sử dụng [MetaApi.cloud](https://metaapi.cloud/) để lấy giá real-time từ tài khoản MT5 của bạn.

## 🔑 Lấy API Credentials

### 1. Đăng ký tài khoản MetaApi

1. Truy cập: https://metaapi.cloud/
2. Đăng ký tài khoản miễn phí
3. Xác nhận email

### 2. Lấy API Token

1. Đăng nhập vào MetaApi Dashboard
2. Vào **Profile** → **API tokens**
3. Click **Create token**
4. Copy token (format: `eyJhbGciOiJSUzUxMi...`)

### 3. Connect MT5 Account

1. Vào **Accounts** → **Add account**
2. Chọn **MetaTrader 5**
3. Điền thông tin:
   - **Name:** Tên tùy chọn
   - **Login:** MT5 login number
   - **Password:** MT5 password
   - **Server:** MT5 server
   - **Platform:** MetaTrader 5
4. Click **Add account**
5. Đợi kết nối (status: **Connected**)
6. Copy **Account ID** (format: `abc123-def456-ghi789`)

## ⚙️ Configuration

### Local Development (appsettings.json)

```json
{
  "MetaApi": {
    "Token": "eyJhbGciOiJSUzUxMi...",
    "AccountId": "abc123-def456-ghi789"
  }
}
```

### Production (Heroku)

Set environment variables:

```bash
heroku config:set METAAPI_TOKEN="eyJhbGciOiJSUzUxMi..." -a tradingsignals
heroku config:set METAAPI_ACCOUNT_ID="abc123-def456-ghi789" -a tradingsignals
```

Verify:
```bash
heroku config -a tradingsignals | grep METAAPI
```

## 🎯 Hoạt động

### Signal Processing Logic

**EntryCHoCH với MetaApi:**

```
1. CHoCH BUY signal xuất hiện (EURUSD @ 1.05500)
2. Service tìm BOS signal trước đó có Swing (1.05000)
3. Mỗi phút, service fetch price từ MetaApi
4. Nếu price < 1.05000 → Resolve CHoCH BUY ✅
```

**Ví dụ:**

| Time  | Event | Price | BOS Swing | CHoCH Status |
|-------|-------|-------|-----------|--------------|
| 10:00 | CHoCH BUY | 1.05500 | 1.05000 | Active |
| 10:01 | Check | 1.05400 | 1.05000 | Active (price > swing) |
| 10:02 | Check | 1.05200 | 1.05000 | Active (price > swing) |
| 10:03 | Check | 1.04950 | 1.05000 | **Resolved** ✅ (price < swing) |

**CHoCH SELL logic ngược lại:**
- Resolve khi price > BOS Swing

## 📊 API Endpoint

Service sử dụng:
```
GET https://mt-client-api-v1.agiliumtrade.agiliumtrade.ai/users/current/accounts/{accountId}/symbols/{symbol}/current-price
Header: auth-token: {token}
```

**Response:**
```json
{
  "symbol": "EURUSD",
  "bid": 1.05495,
  "ask": 1.05505,
  "time": "2025-10-17T04:30:00.000Z"
}
```

Service sử dụng **Mid Price** = (Bid + Ask) / 2

## 🧪 Testing

### 1. Test MetaApi Connection

```bash
curl -H "auth-token: YOUR_TOKEN" \
  "https://mt-client-api-v1.agiliumtrade.agiliumtrade.ai/users/current/accounts/YOUR_ACCOUNT_ID/symbols/EURUSD/current-price"
```

Expected:
```json
{
  "symbol": "EURUSD",
  "bid": 1.05495,
  "ask": 1.05505,
  "time": "2025-10-17T04:30:00.000Z"
}
```

### 2. Test Local

```bash
cd d:\Workspace\CascadeProjects\BoxTradeDiscord\tradingsignals
dotnet run
```

Check logs:
```
[INF] Signal Monitoring Service started
[DBG] Fetching price for EURUSD from MetaApi
[DBG] Price for EURUSD: Bid=1.05495, Ask=1.05505
[INF] CHoCH BUY EURUSD resolved: Price 1.04950 broke below BOS Swing 1.05000
```

### 3. Test on Heroku

```bash
# View logs
heroku logs --tail -a tradingsignals | grep -E "(MetaApi|CHoCH|BOS)"

# Expected logs
[INF] Processing 3 signals for type: entrychoch
[DBG] Fetching price for EURUSD from MetaApi
[DBG] Price for EURUSD: Bid=1.05495, Ask=1.05505
[INF] CHoCH BUY EURUSD resolved: Price 1.04950 broke below BOS Swing 1.05000
[INF] Processing complete. Resolved: 1, Updated: 0
```

## 🚨 Troubleshooting

### "MetaApi credentials not configured"

**Cause:** Token hoặc AccountId không được set

**Fix:**
```bash
# Check current config
heroku config -a tradingsignals | grep METAAPI

# Set if missing
heroku config:set METAAPI_TOKEN="your-token" -a tradingsignals
heroku config:set METAAPI_ACCOUNT_ID="your-account-id" -a tradingsignals

# Restart
heroku restart -a tradingsignals
```

### "Failed to get price: Unauthorized"

**Cause:** Token không hợp lệ hoặc hết hạn

**Fix:**
1. Login vào MetaApi Dashboard
2. Regenerate token mới
3. Update config:
```bash
heroku config:set METAAPI_TOKEN="new-token" -a tradingsignals
```

### "Failed to get price: Not Found"

**Cause:** AccountId sai hoặc account chưa connected

**Fix:**
1. Check account status trên MetaApi Dashboard
2. Ensure status = "Connected"
3. Verify AccountId:
```bash
heroku config -a tradingsignals | grep METAAPI_ACCOUNT_ID
```

### "Could not get current price for EURUSD"

**Cause:** 
- Account không có quyền access symbol
- Market đóng cửa
- Network issue

**Fix:**
1. Check market hours (Forex đóng vào cuối tuần)
2. Test API endpoint manually
3. Check MetaApi logs

## 💰 Pricing

**MetaApi Free Plan:**
- ✅ 1 account
- ✅ Real-time prices
- ✅ Unlimited API calls
- ✅ Đủ cho use case này

**Note:** Nếu cần nhiều accounts hoặc features nâng cao, xem pricing: https://metaapi.cloud/pricing

## 📖 References

- [MetaApi Documentation](https://metaapi.cloud/docs/)
- [API Reference](https://metaapi.cloud/docs/client/restApi/)
- [Price Streaming](https://metaapi.cloud/docs/client/restApi/api/retrieveMarketData/readSymbolPrice/)

## 🔐 Security Best Practices

1. **Never commit tokens to git**
   - Add to `.gitignore`: `appsettings.Production.json`
   - Use environment variables

2. **Rotate tokens regularly**
   - MetaApi allows multiple tokens
   - Rotate every 3-6 months

3. **Monitor usage**
   - Check MetaApi Dashboard for unusual activity
   - Set up alerts

## 📝 Example Workflow

**Complete flow từ TradingView đến Auto-Resolve:**

```
1. TradingView Pine Script phát hiện CHoCH BUY
   ↓
2. Gửi webhook → /webhook/EntryCHoCH
   {
     "symbol": "EURUSD",
     "action": "BUY",
     "price": 1.05500
   }
   ↓
3. WebhookController save to database
   - ActiveTradingSignals table
   - Resolved = false
   ↓
4. Background Service (every 1 minute)
   - Fetch price from MetaApi: 1.05400
   - Find BOS Swing: 1.05000
   - Check: 1.05400 > 1.05000 → Keep Active
   ↓
5. Next minute (price dropped)
   - Fetch price from MetaApi: 1.04950
   - Check: 1.04950 < 1.05000 → Resolve! ✅
   - Update Resolved = true
   ↓
6. Signal không còn hiển thị trong Active Signals
```

## 🎯 Next Steps

Sau khi setup MetaApi:

1. **Test connection:**
   ```bash
   curl -H "auth-token: YOUR_TOKEN" \
     "https://mt-client-api-v1.agiliumtrade.agiliumtrade.ai/users/current/accounts/YOUR_ACCOUNT_ID/symbols/EURUSD/current-price"
   ```

2. **Deploy to Heroku:**
   ```bash
   git add .
   git commit -m "Add MetaApi integration for price monitoring"
   git push heroku main
   ```

3. **Send test signals:**
   ```bash
   .\test-monitoring-service.ps1 -BaseUrl "https://tradingsignals.herokuapp.com" -WebhookSecret "your-secret"
   ```

4. **Monitor logs:**
   ```bash
   heroku logs --tail -a tradingsignals
   ```
