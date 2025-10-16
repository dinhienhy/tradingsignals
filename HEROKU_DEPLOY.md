# Hướng dẫn Deploy lên Heroku

## Yêu cầu
- Tài khoản Heroku
- Heroku CLI đã cài đặt
- Git đã cài đặt

## Cách 1: Deploy qua Heroku CLI (Khuyến nghị)

### 1. Login vào Heroku
```bash
heroku login
```

### 2. Tạo app mới (hoặc sử dụng app có sẵn)
```bash
# Tạo app mới
heroku create your-app-name

# Hoặc thêm remote cho app có sẵn
heroku git:remote -a your-existing-app-name
```

### 3. Thêm PostgreSQL addon
```bash
heroku addons:create heroku-postgresql:mini
```

### 4. Set environment variables
```bash
heroku config:set API_KEY=kyuoj1KRGILRy4Le9i8NtXGDdFIspy07
heroku config:set CONFIG_API_KEY=uHJuLHD70Ju6N97mkQcmWzVTBUxsnscI
heroku config:set ASPNETCORE_ENVIRONMENT=Production
```

### 5. Set buildpack
```bash
heroku buildpacks:set https://github.com/jincod/dotnetcore-buildpack
```

### 6. Deploy
```bash
cd d:\Workspace\CascadeProjects\BoxTradeDiscord\tradingsignals
git init (nếu chưa có)
git add .
git commit -m "Deploy to Heroku"
git push heroku main
```

### 7. Scale up dyno
```bash
heroku ps:scale web=1
```

### 8. Mở app
```bash
heroku open
```

## Cách 2: Deploy qua GitHub Integration

1. Đăng nhập vào Heroku Dashboard
2. Tạo app mới hoặc chọn app có sẵn
3. Vào tab **Deploy**
4. Chọn **GitHub** làm deployment method
5. Connect đến repository của bạn
6. Enable **Automatic Deploys** từ branch `main`
7. Click **Deploy Branch** để deploy ngay

## Cách 3: Deploy qua Container (Docker)

### 1. Login vào Heroku Container Registry
```bash
heroku container:login
```

### 2. Build và push Docker image
```bash
cd d:\Workspace\CascadeProjects\BoxTradeDiscord\tradingsignals
heroku container:push web -a your-app-name
```

### 3. Release image
```bash
heroku container:release web -a your-app-name
```

## Kiểm tra sau khi deploy

### 1. Kiểm tra logs
```bash
heroku logs --tail
```

### 2. Kiểm tra database
```bash
heroku pg:info
```

### 3. Test API endpoints
```bash
# Get Swagger UI
curl https://your-app-name.herokuapp.com/swagger

# Test webhook endpoint (cần tạo webhook config trước)
curl -X POST https://your-app-name.herokuapp.com/webhook/test \
  -H "Content-Type: application/json" \
  -d '{"secret":"your-secret","symbol":"EURUSD","action":"BUY","price":1.0545}'

# Test active signals
curl https://your-app-name.herokuapp.com/api/activesignals \
  -H "X-API-Key: kyuoj1KRGILRy4Le9i8NtXGDdFIspy07"
```

## Cấu hình đã có

Project đã được cấu hình sẵn cho Heroku:

### ✅ Files
- `Procfile` - Chỉ định cách chạy app
- `app.json` - Cấu hình app, addons, environment variables
- `Dockerfile` - Container configuration
- `.deployment` - Deployment configuration
- `.slugignore` - Loại bỏ files không cần thiết khi deploy

### ✅ Database
- Tự động chuyển đổi giữa SQLite (local) và PostgreSQL (Heroku)
- Auto-apply migrations khi deploy lên production
- `DatabaseUtils.cs` xử lý DATABASE_URL từ Heroku

### ✅ Environment Variables
Được set trong `app.json`:
- `API_KEY` - API key cho /api/activesignals endpoints
- `CONFIG_API_KEY` - API key cho /config/webhooks endpoints  
- `ASPNETCORE_ENVIRONMENT` - Set thành "Production"

## Troubleshooting

### App không start
```bash
# Kiểm tra logs
heroku logs --tail

# Restart app
heroku restart
```

### Database migration error
```bash
# Run migrations manually
heroku run dotnet ef database update
```

### Cannot connect to database
```bash
# Kiểm tra DATABASE_URL
heroku config:get DATABASE_URL

# Kiểm tra PostgreSQL addon
heroku addons:info postgresql
```

### Build failed
```bash
# Xóa build cache và thử lại
heroku plugins:install heroku-repo
heroku repo:purge_cache -a your-app-name
git commit --allow-empty -m "Purge cache"
git push heroku main
```

## Update Migration khi có thay đổi database

Khi bạn thêm migration mới (như migration Swing và Resolved vừa tạo):

```bash
# Local: tạo migration
dotnet ef migrations add YourMigrationName

# Commit changes
git add .
git commit -m "Add new migration"

# Push lên Heroku
git push heroku main

# Migrations sẽ tự động apply khi deploy (xem Program.cs)
```

## Notes

- **Free dyno** sẽ sleep sau 30 phút không hoạt động
- **Database mini plan** có giới hạn 10,000 rows
- SSL/HTTPS được Heroku tự động cung cấp
- Mỗi lần deploy, Heroku sẽ build lại project từ đầu

## Monitoring

### Xem metrics
```bash
heroku metrics -a your-app-name
```

### Xem dyno status  
```bash
heroku ps -a your-app-name
```

### Xem database info
```bash
heroku pg:info -a your-app-name
```

---

**Lưu ý:** Thay `your-app-name` bằng tên app thực tế của bạn trên Heroku.
