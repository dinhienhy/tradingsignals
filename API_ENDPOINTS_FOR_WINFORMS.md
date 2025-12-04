# 🔌 API Endpoints for WinForms Integration

## ✅ New Endpoints Implemented

### **1. Resolve Signal**
**Mark an active signal as resolved**

```http
PUT /api/active-signals/resolve/{id}
```

**Headers:**
```
X-API-Key: kyuoj1KRGILRy4Le9i8NtXGDdFIspy07
```

**Response (200 OK):**
```json
{
  "id": 67,
  "resolved": true,
  "message": "Signal marked as resolved successfully"
}
```

**Example (PowerShell):**
```powershell
Invoke-RestMethod -Uri "https://tradingsignals-ae14b4a15912.herokuapp.com/api/active-signals/resolve/67" `
  -Method PUT `
  -Headers @{"X-API-Key"="kyuoj1KRGILRy4Le9i8NtXGDdFIspy07"}
```

---

### **2. Update Swing**
**Update swing value for a signal**

```http
PUT /api/active-signals/swing/{id}
```

**Headers:**
```
X-API-Key: kyuoj1KRGILRy4Le9i8NtXGDdFIspy07
Content-Type: application/json
```

**Body:**
```json
{
  "swing": 4203.80
}
```

**Response (200 OK):**
```json
{
  "id": 67,
  "swing": 4203.80,
  "message": "Swing value updated successfully"
}
```

**Example (PowerShell):**
```powershell
$body = @{ swing = 4203.80 } | ConvertTo-Json

Invoke-RestMethod -Uri "https://tradingsignals-ae14b4a15912.herokuapp.com/api/active-signals/swing/67" `
  -Method PUT `
  -Headers @{"X-API-Key"="kyuoj1KRGILRy4Le9i8NtXGDdFIspy07"} `
  -Body $body `
  -ContentType "application/json"
```

---

### **3. Delete Signal**
**Delete an active signal**

```http
DELETE /api/active-signals/{id}
```

**Headers:**
```
X-API-Key: kyuoj1KRGILRy4Le9i8NtXGDdFIspy07
```

**Response (200 OK):**
```json
{
  "id": 67,
  "message": "Signal deleted successfully"
}
```

**Example (PowerShell):**
```powershell
Invoke-RestMethod -Uri "https://tradingsignals-ae14b4a15912.herokuapp.com/api/active-signals/67" `
  -Method DELETE `
  -Headers @{"X-API-Key"="kyuoj1KRGILRy4Le9i8NtXGDdFIspy07"}
```

---

## 📋 Complete Endpoint List

| Method | Endpoint | Purpose | Status |
|--------|----------|---------|--------|
| GET | `/api/active-signals` | Get all active signals | ✅ Existing |
| GET | `/api/active-signals/type/{type}` | Get signals by type | ✅ Existing |
| GET | `/api/active-signals/unused` | Get unused signals | ✅ Existing |
| PUT | `/api/active-signals/mark-used/{id}` | Mark as used | ✅ Existing |
| **PUT** | `/api/active-signals/resolve/{id}` | **Mark as resolved** | ✅ **NEW** |
| **PUT** | `/api/active-signals/swing/{id}` | **Update swing** | ✅ **NEW** |
| **DELETE** | `/api/active-signals/{id}` | **Delete signal** | ✅ **NEW** |

---

## 🔐 Authentication

All endpoints require API Key authentication via header:

```http
X-API-Key: kyuoj1KRGILRy4Le9i8NtXGDdFIspy07
```

**Error Response (401 Unauthorized):**
```json
"Invalid API key"
```

---

## 🧪 Testing Endpoints

### **Test với Swagger:**
```
https://tradingsignals-ae14b4a15912.herokuapp.com/swagger
```

1. Click "Authorize"
2. Nhập API Key: `kyuoj1KRGILRy4Le9i8NtXGDdFIspy07`
3. Test các endpoints mới

### **Test với curl:**

```bash
# Resolve signal
curl -X PUT "https://tradingsignals-ae14b4a15912.herokuapp.com/api/active-signals/resolve/67" \
  -H "X-API-Key: kyuoj1KRGILRy4Le9i8NtXGDdFIspy07"

# Update swing
curl -X PUT "https://tradingsignals-ae14b4a15912.herokuapp.com/api/active-signals/swing/67" \
  -H "X-API-Key: kyuoj1KRGILRy4Le9i8NtXGDdFIspy07" \
  -H "Content-Type: application/json" \
  -d '{"swing": 4203.80}'

# Delete signal
curl -X DELETE "https://tradingsignals-ae14b4a15912.herokuapp.com/api/active-signals/67" \
  -H "X-API-Key: kyuoj1KRGILRy4Le9i8NtXGDdFIspy07"
```

---

## 📊 Usage in WinForms App

### **ApiClient.cs** đã implement:

```csharp
// Resolve signal
await _apiClient.UpdateSignalResolvedStatusAsync(signalId, true);

// Update swing
await _apiClient.UpdateSignalSwingAsync(signalId, 4203.80m);

// Delete signal
await _apiClient.DeleteSignalAsync(signalId);
```

### **SignalResolutionEngine.cs** sử dụng:

```csharp
// M5CHoCH: Delete all entry signals
foreach (var signal in entryChochSignals)
{
    await _apiClient.DeleteSignalAsync(signal.Id);
}

// EntryCHoCH: Update swing
await _apiClient.UpdateSignalSwingAsync(entryChoch.Id, swing);

// Mark as resolved
await _apiClient.UpdateSignalResolvedStatusAsync(signal.Id, true);
```

---

## 🚀 Deployment Steps

### **1. Deploy to Heroku:**

```bash
# Option 1: Via GitHub (Recommended)
# Go to: https://dashboard.heroku.com/apps/tradingsignals/deploy/github
# Click "Deploy Branch"

# Option 2: Via Git
cd tradingsignals
git push heroku main
```

### **2. Verify Deployment:**

Check endpoints are working:
```powershell
# Test resolve endpoint
Invoke-RestMethod -Uri "https://tradingsignals-ae14b4a15912.herokuapp.com/api/active-signals" `
  -Headers @{"X-API-Key"="kyuoj1KRGILRy4Le9i8NtXGDdFIspy07"}
```

### **3. Check Logs:**

```bash
heroku logs --tail -a tradingsignals
```

---

## ⚠️ Error Handling

### **404 Not Found:**
```json
"Signal with ID 67 not found"
```
**Cause:** Signal ID không tồn tại

### **400 Bad Request (Swing only):**
```json
"Invalid swing value"
```
**Cause:** Swing <= 0 hoặc request body invalid

### **401 Unauthorized:**
```json
"Invalid API key"
```
**Cause:** API key sai hoặc thiếu header

---

## 📝 Code Implementation Details

**File:** `Controllers/ActiveSignalsController.cs`

**Lines Added:** 150+ lines

**Features:**
- ✅ API Key authentication
- ✅ Input validation
- ✅ Error handling
- ✅ Logging
- ✅ Swagger documentation
- ✅ HTTP status codes

**Models:**
```csharp
public class SwingUpdateRequest
{
    public decimal Swing { get; set; }
}
```

---

## ✅ Checklist

- [x] Implement resolve endpoint
- [x] Implement swing update endpoint
- [x] Implement delete endpoint
- [x] Add authentication
- [x] Add validation
- [x] Add logging
- [x] Test build locally
- [x] Push to GitHub
- [ ] Deploy to Heroku
- [ ] Test on production
- [ ] Run WinForms app
- [ ] Verify full workflow

---

**Ready for deployment and testing!** 🎉

---

**Date:** December 3, 2025  
**Version:** 1.0.0  
**Status:** ✅ Implemented & Ready for Deploy
