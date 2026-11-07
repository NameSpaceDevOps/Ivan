# راهنمای بومی‌سازی و افزودن زبان جدید (Ivan Localization & Translation Guide)

سامانه ایوان از روز اول با معماری تفکیک‌شده بومی‌سازی طراحی شده است:
- جهت و متن رابط کاربری از طریق `IStringLocalizer` و فایل‌های منابع استاندارد (.resx).
- فرهنگ، تقویم و شیوه نمایش ارقام به عنوان سه تنظیم مجزا در وضعیت کاربر (`UserPreferencesState`).
- طراحی پوسته و استایل‌ها با خصوصیات منطقی CSS (نظیر `margin-inline`, `padding-inline`, `start`, `end`) جهت جابجایی طبیعی بین راست‌به‌چپ (RTL) و چپ‌به‌راست (LTR).

---

## ۱. ساختار تنظیمات کاربر (`UserPreferencesState`)

تنظیمات بومی‌سازی شامل ۳ بعد مستقل هستند:
1. **فرهنگ (Culture)**: کد زبان (مانند `fa-IR`، `en-US`، `ar-SA`).
2. **نوع تقویم (CalendarType)**:
   - `Jalali` (تقویم هجری شمسی خورشیدی)
   - `Gregorian` (تقویم میلادی)
   - `Hijri` (تقویم هجری قمری)
3. **شیوه نمایش ارقام (DigitStyle)**:
   - `Persian` (ارقام فارسی: ۰، ۱، ۲، ...)
   - `Latin` (ارقام انگلیسی: 0, 1, 2, ...)
   - `ArabicIndic` (ارقام عربی-مشرقی: ٠، ١، ٢، ...)

---

## ۲. افزودن فایل‌های ترجمه (Resource Files)

برای افزودن یک زبان جدید (برای مثال زبان عربی `ar-SA` یا ترکی `tr-TR`):
1. به مسیر `src/Ivan.Web/Resources/` بروید.
2. برای هر صفحه یا کامپوننت، فایل `.resx` جدیدی با پسوند فرهنگ بسازید:
   - `Home.ar-SA.resx`
   - `Money.ar-SA.resx`
   - `Settings.ar-SA.resx`
3. کلیدها را ترجمه کنید.
4. فرهنگ جدید را به آرایه `supportedCultures` در فایل `src/Ivan.Web/Program.cs` اضافه نمایید.

---

## ۳. محاسبات تقویم شمسی جلالی (`ICalendarService`)

کلیه عملیات تاریخ، ماه‌های سال، بررسی کبیسه بودن (۳۰ اسفند) و فرمت‌بندی تاریخ‌ها از طریق سرویس `ICalendarService` انجام می‌شود. در نوشتن کدهای جدید هرگز نباید از تبدیل مستقیم `DateTime.ToString()` بدون تقویم کاربر استفاده شود.
```csharp
var displayDate = CalendarService.FormatDate(date, "yyyy/MM/dd", Preferences.Calendar, Preferences.DigitStyle);
```
سرویس همچنین ارقام تایپ‌شده را به طور خودکار پذیرفته و نرمال‌سازی می‌کند (`NormalizeDigitsToLatin` و `ConvertDigits`).
