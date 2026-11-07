# راهنمای نصب و راه‌اندازی سامانه ایوان (Ivan Installation Guide)

ایوان (Ivan) یک سامانه مدیریت خانه و خانواده خودمیزبان (Self-Hosted)، سبک و مبتنی بر وب است که با دات‌نت ۱۰ (.NET 10 LTS)، Blazor Web App (Interactive Server)، پایگاه‌داده SQLite و طراحی فارسی‌محور (RTL-First) پیاده‌سازی شده است.

---

## ۱. پیش‌نیازها

- **سیستم‌عامل**: ویندوز (Windows 10/11، Windows Server 2019/2022) یا لینوکس (Ubuntu 22.04+, Debian 12+, Alpine).
- **محیط اجرایی**: [.NET 10 Runtime / SDK](https://dotnet.microsoft.com/) یا موتور کانتینری Docker.
- **منابع سخت‌افزاری حداقلی**:
  - پردازنده: ۲ هسته
  - حافظه رم: ۱ گیگابایت
  - دیسک: حداقل ۵۰۰ مگابایت فضای خالی برای دیتابیس SQLite و فایل‌های پیوست.

---

## ۲. روش اول: اجرا به عنوان سرویس ویندوز (Windows Service - توصیه‌شده)

سامانه ایوان دارای پشتیبانی بومی از `UseWindowsService` در کسترل (Kestrel) است و بدون نیاز به ورود کاربر، با راه‌اندازی ویندوز به عنوان Background Service شروع به کار می‌کند.

### مراحل نصب:

1. **انتشار پروژه (Publish)**:
   ```powershell
   dotnet publish src/Ivan.Web/Ivan.Web.csproj -c Release -o C:\Services\Ivan
   ```

2. **ایجاد سرویس در ویندوز با `sc.exe`**:
   یک پنجره PowerShell یا Command Prompt را به صورت **Administrator** باز کنید:
   ```cmd
   sc.exe create Ivan binPath= "C:\Services\Ivan\Ivan.Web.exe" start= auto
   ```

3. **شروع به کار سرویس**:
   ```cmd
   sc.exe start Ivan
   ```

4. **دسترسی به سامانه**:
   مرورگر وب را باز کرده و به نشانی زیر بروید:
   ```
   http://localhost:5000  (یا پورتی که در appsettings.json تنظیم کرده‌اید)
   ```

---

## ۳. روش دوم: اجرا در کانتینر Docker

ایوان دارای Dockerfile چندمرحله‌ای بهینه و فایل Docker Compose است.

### مراحل اجرا:

1. اجرای کانتینر در پس‌زمینه:
   ```bash
   docker compose up -d --build
   ```

2. بررسی وضعیت سلامت و لاگ‌ها:
   ```bash
   docker ps
   docker logs -f ivan-app
   ```

3. دسترسی به برنامه:
   نشانی در مرورگر: `http://localhost:8080`
   داده‌های پایگاه‌داده به طور خودکار در Volume با نام `ivan_data` ذخیره و حفظ می‌گردند.

---

## ۴. روش سوم: اجرا به عنوان سرویس لینوکس (systemd)

1. انتشار باینری‌ها در مسیر دلخواه:
   ```bash
   dotnet publish src/Ivan.Web/Ivan.Web.csproj -c Release -o /var/www/ivan
   ```

2. ایجاد فایل سرویس در `/etc/systemd/system/ivan.service`:
   ```ini
   [Unit]
   Description=Ivan Home & Family Manager
   After=network.target

   [Service]
   WorkingDirectory=/var/www/ivan
   ExecStart=/usr/bin/dotnet /var/www/ivan/Ivan.Web.dll
   Restart=always
   RestartSec=10
   KillSignal=SIGINT
   SyslogIdentifier=ivan-app
   User=www-data
   Environment=ASPNETCORE_ENVIRONMENT=Production
   Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

   [Install]
   WantedBy=multi-user.target
   ```

3. فعال‌سازی و شروع سرویس:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable ivan.service
   sudo systemctl start ivan.service
   ```

---

## ۵. اولین ورود و ویزارد راه‌اندازی (Setup Wizard)

در اولین اجرای سامانه، به مسیر `/setup` هدایت می‌شوید:
1. ایجاد حساب کاربری مدیر / سرپرست خانواده (Parent/Owner).
2. تنظیم نام خانوار، تقویم پیش‌فرض (جلالی / میلادی)، زبان (فارسی / انگلیسی)، و واحد پول (تومان / ریال).
3. انتخاب قالب اولیه کدینگ حساب‌های مالی (خانوادگی کامل یا جمع‌وجور).
4. پس از اتمام ویزارد، دسترسی مجدد به صفحه راه‌اندازی اولیه جهت حفظ امنیت به طور خودکار مسدود می‌گردد.
