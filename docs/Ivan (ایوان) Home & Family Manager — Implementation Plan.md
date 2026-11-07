# Ivan (ایوان) — Implementation Plan

Draft v1.0 · 7 November 2026 · Open source (AGPL-3.0)

## 1. Vision and principles

Ivan (ایوان, pronounced i-vān) is a lightweight, self-hosted, mobile-first web app for running a household: homes and properties, assets, people, checklists, children, library, media, and a minimal ledger. Persian (RTL) first, localization-ready from day one.

- **Fast and light:** server-rendered Blazor, SQLite, no mandatory external services.
- **Simple on the surface, solid underneath:** one set of engines (checklists, recurrence, reminders, attachments, access control) reused by every module.
- **Private by default:** children and external people see only what they are granted.
- **Kid-friendly:** a separate, simplified shell with big targets and icons.
- **Easy to self-host:** one install path, first-run wizard, built-in backup.

## 2. Decisions summary

| Area | Decision |
| --- | --- |
| Framework | Blazor Web App, Interactive Server render mode (current .NET LTS) |
| UI components | Radzen Blazor (RTL), custom mobile shell, custom Jalali date components |
| Database | SQLite (WAL) via EF Core; one database, HouseholdId on every tenant row |
| Tenancy | Multi-household; each household has multiple properties |
| Access | Memberships, roles, per-resource grants with expiry, guardian rule for children |
| Reminders | Web Push, Telegram, Bale, SMS through adapters; BackgroundService plus outbox table |
| Localization | IStringLocalizer and resource files; culture, calendar and digits are separate per-user settings; Persian first |
| Finance | Double-entry ledger underneath, simple Expense/Income/Transfer forms on top |
| AI | Optional, off by default, provider abstraction, household key, monthly cap |
| Hosting | Kestrel as a Windows Service (IIS optional); Docker later |
| License | AGPL-3.0 |
| Offline use | Not required |

## 3. Architecture

### 3.1 Solution structure

- **Domain:** entities, enums, domain rules, no dependencies.
- **Application:** services and interfaces (INotifier, IAiClient, IFileStore, ICalendarService).
- **Infrastructure:** EF Core and SQLite, notifier adapters, file store, background jobs.
- **Web:** Blazor app, layouts (Main shell and Kid shell), components, localization resources.
- **Tests:** unit, integration (SQLite), authorization tests, UI smoke tests (bUnit, Playwright).

### 3.2 Multi-tenancy

- Every tenant table has HouseholdId. EF Core global query filters apply it from the current membership context, and a SaveChanges interceptor stamps it.
- A reflection-based test asserts that, for every entity, a query under household A never returns household B rows.
- A single-household mode (config flag) hides household switching and sign-up for simple self-hosting.

### 3.3 Authorization and access model

- **Authentication:** ASP.NET Core Identity. Password plus optional TOTP 2FA for parents; avatar and PIN quick-login for children on trusted devices; external users join through an invite link.
- **Roles per membership:** Owner, Parent, Teen, Child, External. Each membership has a property scope and a module mask.
- **Visibility on sensitive resources:** Private (owner only), Guardians, Selected (grants), Household.
- **Guardian rule:** a child’s guardians can see that child’s data by default; a child never sees a guardian’s private data unless granted.
- **AccessGrant:** grantee, resource type and id, permission (View, Edit, Manage), optional expiry. Revocation takes effect immediately, and one Sharing screen lists every grant.
- **Enforcement:** ASP.NET policy handlers for actions plus EF filters for data. Reads and writes of Health, Document and Finance data are written to the AuditLog.
- **External presets:** Babysitter, Housekeeper, Lawyer or Consultant (view-only on selected documents), each a template of scope, modules and expiry.

### 3.4 Localization and calendars

- All UI text goes through IStringLocalizer and resource files, with no hard-coded strings.
- Per-user settings: Culture, Calendar (Jalali, Gregorian, later Hijri), DigitStyle (Persian, Latin, Arabic-Indic), first day of week.
- ICalendarService formats, parses and does date arithmetic in the user’s calendar. Recurrence supports calendar-aware rules, for example the first day of every Jalali month.
- Layout uses CSS logical properties; dir comes from the culture; the Radzen RTL option follows it.
- Money is stored as integer minor units plus an ISO currency, with a display option for Toman or Rial.
- Radzen has no Persian-calendar date picker or scheduler, so a custom Jalali date picker, range picker and agenda view are built and proven in Phase 0.

### 3.5 Notifications

- INotifier with adapters: Web Push (VAPID), Telegram bot, Bale bot, and SMS (ISmsProvider abstraction: SMS.ir by default, IPPanel as a configurable provider type).
- Per-user channels with verification (one-time token through a bot deep link, or an SMS code), priority and fallback order, and quiet hours.
- A reminder outbox table processed by a BackgroundService with retry and backoff, an idempotent DedupKey, and a NotificationLog of delivery history.
- Web Push depends on the browser vendor’s push service, so Telegram, Bale and SMS are first-class fallbacks.

### 3.6 Attachments

Files live on disk under a configurable root with metadata in the Attachment table. SHA-256 de-duplication, size and type limits, image thumbnails. Access is always checked through the owning resource, never by raw URL. Optional later: file-level encryption.

### 3.7 Optional AI

- IAiClient on Microsoft.Extensions.AI. Providers: OpenAI-compatible endpoint, Anthropic, and local (Ollama).
- Off by default. Per-household enable, the household’s own API key (stored encrypted), a monthly cap, and an AiUsageLog.
- Features: invoice photo to asset or expense draft, Persian natural-language quick-add, document summary. Output is always a draft a human confirms.
- Privacy rule: Health and Person dossier data is never sent to a provider.

### 3.8 Hosting and deployment

- Recommended: Kestrel as a Windows Service (UseWindowsService). IIS can front it as a reverse proxy or host it in-process.
- With IIS in-process: enable WebSockets; set the app pool to AlwaysRunning, preload on, idle timeout 0. Otherwise the reminder worker stops when nobody is browsing.
- The data root (SQLite, attachments, backups, Data Protection keys) is a configurable path outside the publish folder. All settings can be overridden by environment variables, and no Windows-only APIs are used, so Docker is a later packaging step.
- HTTPS through an IIS binding or a Kestrel certificate. For internet access, put a reverse proxy with rate limiting in front.
- Health endpoint and structured logging (Serilog to file).

### 3.9 Backup and restore

A nightly job does a SQLite online backup plus an attachment folder copy to a configurable target (local disk, optional WebDAV or S3) with retention. A restore wizard and a one-household zip export complete the set.

### 3.10 Security checklist

HTTPS only, secure cookies, antiforgery, login rate limiting and lockout, 2FA for parents, CSP, never store full card numbers or CVV (last four digits only), secrets through configuration, audit log, dependency scanning.

### 3.11 Open-source practices

AGPL-3.0 LICENSE file and SPDX headers; a footer link to the source (AGPL network clause); a dependency license check (Radzen Blazor is MIT as far as known, verify before release); README with screenshots, CONTRIBUTING, CODE\_OF\_CONDUCT, issue templates; demo-data seeding; no telemetry; translations contributed as resource files.

## 4. UX guidelines

- **Mobile first:** bottom navigation (Home, Tasks, Calendar, Money, More), touch targets of at least 44 px, one primary action per screen, a quick-add button everywhere.
- **Persona dashboards:** Parent (today, due soon, approvals), Teen, Child (my routine, my homework, my stars, my wallet).
- **Kid shell:** icons plus short words, avatar login, celebratory feedback on completion, no destructive actions, limited navigation.
- **Accessibility:** WCAG 2.1 AA contrast, keyboard navigation, visible focus, screen-reader labels, reduced-motion support, dark mode.
- **Consistency:** one list, detail and edit pattern; empty states that teach; undo for deletes (soft delete).
- **Persian typography:** Vazirmatn font, correct RTL punctuation, Persian digits, inputs that accept either digit style.

## 5. Module scope (v1)

Home and properties; Assets and maintenance; People dossier; Checklists and tasks; Calendar and important dates; Library; Media tracker; Children (routines, points, rewards); Homework and exams; Finance (accounts, transactions, budgets, cheques, loans and installments, kid wallets); Sharing and roles; Notifications; Dashboard and search; Backup; optional AI.

## 6. Data model

### 6.1 Conventions

- Id is a time-ordered GUID (v7). HouseholdId is on every tenant table.
- Audit columns: CreatedAt/By, UpdatedAt/By, DeletedAt (soft delete), RowVersion (concurrency).
- Instants are UTC DateTimeOffset; calendar dates such as birthdays are DateOnly (ISO), displayed through ICalendarService.
- Money: Amount (long, minor units) plus Currency (char 3).
- Extra (JSON) on core entities holds user-defined fields.
- A Visibility enum (Private, Guardians, Selected, Household) is on sensitive entities.

### 6.2 Tenancy and access

| Entity | Key fields |
| --- | --- |
| Household | Name, DefaultCulture, DefaultCalendar, DefaultCurrency, TimeZone |
| AppUser | UserName, Email?, Phone?, PasswordHash, TotpSecret?, PinHash?, Culture, Calendar, DigitStyle, Theme, PersonId? |
| Membership | UserId, Role, PropertyScope (all or list), ModuleMask, ExpiresAt?, IsActive |
| AccessGrant | GranteeUserId, ResourceType, ResourceId? (null = all of type), Permission, ExpiresAt?, GrantedBy, RevokedAt? |
| Invite | TokenHash, RolePreset, Scope, ExpiresAt, UsedAt, CreatedBy |
| AuditLog | UserId, Action, ResourceType, ResourceId, Summary, At, Ip |

### 6.3 Places

| Entity | Key fields |
| --- | --- |
| Property | Name, Type (Home, Villa, Garden, Warehouse, Office, Other), Address, Geo?, Notes, Extra |
| Space | PropertyId, ParentSpaceId?, Name, Type |
| UtilityAccount | PropertyId, Kind, Provider, AccountRef, Notes |
| MeterReading | UtilityAccountId, ReadingDate, Value, Unit |
| Contact | Name, Category, Phones, Email, Notes, PropertyId? |

### 6.4 People

| Entity | Key fields |
| --- | --- |
| Person | Kind (Member, Relative, Pet, External), DisplayName, FullName, BirthDate, Photo, Relationship, UserId?, Visibility, Extra |
| PersonGuardian | PersonId, GuardianPersonId |
| HealthRecord | PersonId, Type (Vaccine, Medication, Allergy, Condition, Appointment, Measurement, Test), Title, Date, NextDueDate, Details (JSON), Visibility (default Guardians) |
| EducationRecord | PersonId, Institution, Level, StartYear, EndYear, Notes |
| PersonDocument | PersonId, Type, Number?, IssueDate, ExpiryDate, Visibility |
| ImportantDate | PersonId?, Title, Date, RepeatsYearly, ReminderOffsets |
| TimelineEvent | SubjectType, SubjectId, EventDate, Kind, Title, Body |

### 6.5 Things

| Entity | Key fields |
| --- | --- |
| AssetCategory | Name, Icon |
| Asset | PropertyId, SpaceId?, CategoryId, Name, Brand, Model, SerialNo, PurchaseDate, PurchasePrice, Vendor, WarrantyEnd, Status, Extra |
| MaintenancePlan | AssetId, Title, RecurrenceId, LastDoneAt, NextDueAt, EstimatedCost |
| ServiceRecord | AssetId, Date, Type, ProviderContactId?, Cost, Notes, JournalEntryId? |
| Book | Title, Authors, Isbn, Publisher, Year, Language, SpaceId?, Status, Rating, Extra |
| BookRead | BookId, PersonId, Status, FinishedOn |
| BookLoan | BookId, borrower (PersonId, ContactId or free text), LoanedOn, DueOn, ReturnedOn |
| MediaItem | Type (Film, Series, Anime, Documentary), Title, Year, Notes |
| MediaProgress | MediaItemId, PersonId, Status (Watchlist, Watching, Done), Rating, Note |
| MediaEpisode | MediaItemId, Season, Number, Title |
| EpisodeWatched | EpisodeId, PersonId, WatchedOn |

### 6.6 Engine

| Entity | Key fields |
| --- | --- |
| Checklist | Title, OwnerType/OwnerId?, TemplateId?, IsTemplate, RecurrenceId?, Visibility |
| ChecklistItem | ChecklistId, ParentItemId?, Title, Done, DoneAt, DoneBy, DueAt?, AssigneePersonId?, Sort |
| TaskItem | Title, Details, AssigneePersonId?, DueAt?, RecurrenceId?, PropertyId?, LinkType/LinkId?, Status, Priority |
| Recurrence | Rule (RRULE subset), CalendarMode (Jalali or Gregorian), StartDate, EndDate?, TimeOfDay, TimeZone |
| Reminder | SubjectType/SubjectId, FireAt, Offset, RecipientUserIds, State, Attempts, LastError, DedupKey |
| NotificationChannel | UserId, Type (WebPush, Telegram, Bale, Sms), Address, Verified, Enabled, Priority |
| NotificationPreference | UserId, QuietStart, QuietEnd, Categories |
| NotificationLog | ReminderId, ChannelId, Status, At, Error |
| Attachment | OwnerType/OwnerId, FileName, ContentType, Size, Sha256, StoragePath, Visibility, UploadedBy |
| Tag, TagLink | Name, Color; TagId, TargetType, TargetId |

### 6.7 Children

| Entity | Key fields |
| --- | --- |
| RoutineTask | PersonId, Title, Icon, TimeSlot, Days, Points, RequiresApproval |
| RoutineCompletion | RoutineTaskId, Date, CompletedAt, ApprovedBy?, PointsAwarded |
| PointLedger | PersonId, Delta, Reason, SourceType/SourceId, At |
| Reward, RewardRedemption | Title, Cost, Icon; RewardId, PersonId, Status, DecidedBy |
| Subject | PersonId, Name, Teacher |
| HomeworkItem | PersonId, SubjectId, Title, Details, AssignedOn, DueOn, Status, ParentChecked |
| ExamEvent | PersonId, SubjectId, Date, Topics, Score? |
| SchoolSchedule | PersonId, DayOfWeek, Period, SubjectId, StartTime, EndTime |

### 6.8 Finance

| Entity | Key fields |
| --- | --- |
| Account | Name, Type (Asset, Liability, Equity, Income, Expense), SubType (Cash, Bank, Card, Wallet, Receivable, Payable, Loan, Category), Currency, ParentAccountId?, OwnerPersonId?, Visibility, BankInfo (bank name and last four digits only), IsActive |
| JournalEntry | Date, Description, Reference, Status (Posted, Voided), ReversalOfId?, SourceType/SourceId?, CreatedBy |
| JournalLine | EntryId, AccountId, Debit, Credit, Currency, ExchangeRate?, PropertyId?, PersonId?, AssetId?, Memo |
| Budget | CategoryAccountId, Period, Amount, PropertyId?, StartsOn, EndsOn? |
| RecurringTransaction | Template (JSON), RecurrenceId, AutoPost |
| Cheque | Direction (Issued, Received), Number, SayadId?, Bank, Amount, Currency, DueDate, CounterpartyContactId?, Status (Pending, Deposited, Cleared, Bounced, Returned), JournalEntryId? |
| Loan | Direction (Borrowed, Lent), counterparty (ContactId or PersonId), Principal, Currency, StartDate, InterestRate?, InterestFree, InstallmentCount |
| Installment | LoanId, No, DueDate, Amount, PaidAmount, PaidAt?, JournalEntryId?, Status |
| WalletLink | WalletAccountId, PersonId, Mode (Virtual or BankLinked), BankAccountId? (the child’s bank account), CustodianPersonId |
| AllowanceRule | PersonId, Amount, RecurrenceId, FromAccountId, WalletAccountId, Active |
| SavingsGoal | PersonId, WalletAccountId, Title, TargetAmount, TargetDate?, Status |
| WalletRequest | PersonId, Kind (Withdrawal, Purchase), Amount, Reason, Status, DecidedBy? |
| BalanceCheck | AccountId, Date, StatementBalance, Difference |

#### 6.8.1 Default chart of accounts

Ivan seeds a full family chart at setup with Persian and English names; every name can be renamed, hidden or extended. Codes group accounts by type: 1xxx assets, 2xxx liabilities, 3xxx equity, 4xxx income, 5xxx–6xxx expenses.

**Assets**

| Code | Group | گروه | Default sub-accounts |
| --- | --- | --- | --- |
| 1000 | Cash | نقد | Cash on hand (پول نقد); Petty cash (تنخواه) |
| 1010 | Bank accounts | حساب‌های بانکی | Added by the user per real account: Checking (جاری); Savings (پس‌انداز); Child bank account (حساب کودک) |
| 1020 | Prepaid cards and wallets | کارت‌ها و کیف پول‌ها | Prepaid or gift card (کارت هدیه); Digital wallet (کیف پول دیجیتال) |
| 1030 | Kid wallets | کیف پول فرزندان | One account per child, created automatically (کیف پول هر فرزند) |
| 1100 | Receivables | مطالبات | Loans given (وام‌های داده‌شده); Cheques received, pending (چک‌های دریافتی در جریان); Deposits and prepayments (ودیعه و پیش‌پرداخت); Money owed by others (طلب از اشخاص) |
| 1200 | Savings and investments | پس‌انداز و سرمایه‌گذاری | Term deposits (سپرده‌ها); Gold and coins (طلا و سکه); Stocks and funds (سهام و صندوق‌ها); Foreign currency (ارز) |
| 1300 | Property and valuables | دارایی‌ها | Real estate (املاک); Vehicles (خودرو); Jewelry and valuables (جواهرات و اشیای قیمتی) |

**Liabilities and equity**

| Code | Group | گروه | Default sub-accounts |
| --- | --- | --- | --- |
| 2000 | Loans and debts | وام‌ها و بدهی‌ها | Bank loans (وام بانکی); Personal loans (قرض از اشخاص); Interest-free loans (قرض‌الحسنه); Installment purchases (خرید اقساطی); Credit card balance (مانده کارت اعتباری) |
| 2100 | Cheques issued | چک‌های پرداختنی | Cheques issued, pending (چک‌های صادرشده در جریان) |
| 2200 | Payables | بدهی‌های جاری | Bills due (قبوض پرداخت‌نشده); Wages owed (حقوق پرداخت‌نشده) |
| 3000 | Opening balances | تراز افتتاحی | System account, cannot be deleted |
| 3100 | Retained surplus | مازاد انباشته | System account, cannot be deleted |
| 3200 | Valuation adjustments | تعدیل ارزش | Optional, for revaluing gold, currency and property |

**Income**

| Code | Group | گروه | Default sub-accounts |
| --- | --- | --- | --- |
| 4000 | Employment | حقوق و دستمزد | Salary (حقوق); Bonus and Eidi (پاداش و عیدی); Overtime (اضافه‌کار); Allowances (مزایا) |
| 4100 | Business and freelance | کسب‌وکار و کار آزاد | Business profit (سود کسب‌وکار); Freelance (کار پروژه‌ای) |
| 4200 | Rental income | درآمد اجاره | Rent received (اجاره دریافتی) |
| 4300 | Investment income | درآمد سرمایه‌گذاری | Deposit interest (سود سپرده); Dividends (سود سهام); Gains on gold, currency and shares (سود دارایی‌ها) |
| 4400 | Gifts and support | هدیه و کمک | Gifts received (هدیه دریافتی); Family support received (کمک خانوادگی) |
| 4500 | Refunds | بازپرداخت‌ها | Refunds and reimbursements (استرداد و بازپرداخت); Insurance claims (خسارت بیمه) |
| 4900 | Other income | سایر درآمدها | Other income (سایر درآمدها) |

**Expenses**

| Code | Group | گروه | Default sub-accounts |
| --- | --- | --- | --- |
| 5000 | Housing | مسکن | Rent or mortgage installment (اجاره یا قسط وام مسکن); Building charges (شارژ ساختمان); Property fees and taxes (عوارض و مالیات ملک) |
| 5100 | Utilities | قبوض و خدمات | Electricity (برق); Water (آب); Gas (گاز); Internet and phone (اینترنت و تلفن); Heating fuel (سوخت گرمایشی) |
| 5200 | Food | خوراک | Groceries (خواربار); Dining out and delivery (رستوران و سفارش غذا); Household supplies (لوازم مصرفی منزل) |
| 5300 | Transport | حمل‌ونقل | Fuel (بنزین); Vehicle repair and service (تعمیر و سرویس خودرو); Vehicle insurance and fees (بیمه و عوارض خودرو); Taxi and public transport (تاکسی و حمل‌ونقل عمومی); Parking and tolls (پارکینگ و عوارض) |
| 5400 | Health | سلامت | Doctor and clinic (پزشک و درمانگاه); Medicine (دارو); Dental and optical (دندان و عینک); Tests and imaging (آزمایش و تصویربرداری) |
| 5500 | Children and education | فرزندان و آموزش | Tuition and school fees (شهریه); Books and stationery (کتاب و لوازم‌التحریر); Courses and activities (کلاس‌ها و فعالیت‌ها); Childcare and babysitter (مراقبت و پرستار کودک); Allowance paid (پول توجیبی); Toys (اسباب‌بازی) |
| 5600 | Home upkeep | نگهداری منزل | Repairs and maintenance (تعمیرات); Appliances and furniture (لوازم خانگی و مبلمان); Garden and pool (باغ و استخر); Domestic help (خدمات منزل و کمک‌کار); Cleaning and laundry (نظافت و خشکشویی) |
| 5700 | Clothing and personal care | پوشاک و مراقبت شخصی | Clothing and shoes (پوشاک و کفش); Hair and beauty (آرایشگاه و بهداشت) |
| 5800 | Insurance | بیمه‌ها | Life (عمر); Supplementary health (درمان تکمیلی); Home (منزل) |
| 5900 | Leisure | تفریح و سرگرمی | Travel and holidays (سفر و تعطیلات); Entertainment and subscriptions (سرگرمی و اشتراک‌ها); Hobbies and sports (سرگرمی‌ها و ورزش); Books and media (کتاب و رسانه) |
| 6000 | Gifts and giving | هدیه و کمک | Gifts given (هدیه); Charity and donations (صدقه و کمک‌های خیریه); Family support (کمک به خانواده) |
| 6100 | Financial costs | هزینه‌های مالی | Loan interest and fees (سود و کارمزد وام); Bank fees (کارمزد بانکی); Late and bounced-cheque penalties (جریمه دیرکرد و چک برگشتی); Taxes (مالیات) |
| 6200 | Professional services | خدمات تخصصی | Lawyer (وکیل); Accountant and consultant (حسابدار و مشاور) |
| 6300 | Pets | حیوانات خانگی | Food and supplies (خوراک و لوازم); Vet (دامپزشک) |
| 6400 | Government and documents | هزینه‌های اداری | Documents and certificates (مدارک و گواهی‌ها); Fines (جرائم) |
| 6900 | Miscellaneous | متفرقه | Other expenses (سایر هزینه‌ها) |
| 6990 | Uncategorized | طبقه‌بندی‌نشده | System account for unsorted imports and quick entries |

**Rules for the seeded chart**

- **Presets:** the setup wizard offers Full (this chart, about 100 leaf accounts), Compact (about 30 of the most common accounts) and Empty.
- **Localized names:** account names are stored as resource keys and shown in the user’s language; a renamed account keeps the custom name in every language.
- **Groups are headers:** entries post only to leaf accounts; groups exist for reports and navigation.
- **System accounts:** 3000, 3100 and 6990 cannot be deleted or renamed. Any other account that has postings can only be deactivated, never deleted.
- **Merge:** a merge tool moves all postings from one account to another when the user reorganizes categories.
- **Property costs use dimensions:** costs for a villa, garden or warehouse are tagged with the Property dimension on the journal line, so there is no duplicated account tree per property.
- **Created automatically:** each child gets a kid wallet under 1030; cheques and loans use 1100, 2000 and 2100 as their status changes, and a cleared cheque moves to the bank account.
- **Currency:** accounts inherit the household currency; a foreign-currency account (for example under 1200) can have its own.

### 6.9 Settings

| Entity | Key fields |
| --- | --- |
| HouseholdSetting | Key, Value |
| AiSettings | Enabled, Provider, Endpoint, EncryptedApiKey, MonthlyCap, FeatureFlags |
| AiUsageLog | At, Feature, Units |

### 6.10 Invariants

- Per currency, the debits of a journal entry equal its credits. Posted entries are never deleted, only voided by a reversing entry.
- A wallet’s balance is the ledger balance of its wallet account; there is no separate balance column.
- Bank-linked kid wallets mirror a real child bank account. v1 has no bank API: balances are reconciled manually through BalanceCheck.
- A Person can exist without an AppUser (pets, young children).
- Cheque and installment status changes post ledger entries through JournalEntry.SourceType/SourceId, so reports always tie back to the ledger.

## 7. Delivery plan and backlog

Indicative effort for one focused developer: 27 to 35 person-weeks in total. Sizes are rough and should be re-estimated after Phase 0.

### Phase 0: Spike (1–2 weeks)

| ID | Story | Acceptance criteria |
| --- | --- | --- |
| 0.1 | RTL and LTR shell with Radzen | The same layout flips by culture; no physical left/right CSS in app styles |
| 0.2 | Jalali date picker and calendar service | Select or type Jalali dates; round-trip to UTC/ISO; leap years (30 Esfand) correct; Persian and Latin digits accepted |
| 0.3 | Notification proof | A test message arrives through Web Push, Telegram and Bale |
| 0.4 | Hosting proof | Runs as a Windows Service and under IIS with WebSockets; the background worker survives idle periods |

### Phase 1: Foundation (3–4 weeks)

| ID | Story | Acceptance criteria |
| --- | --- | --- |
| 1.1 | Setup wizard | First run creates owner, household, culture, calendar and currency; a second run is blocked |
| 1.2 | Identity and 2FA | Register, login, logout; lockout after repeated failures; optional TOTP for parents |
| 1.3 | Tenancy filters | The cross-household isolation test passes for every entity; HouseholdId is stamped automatically |
| 1.4 | Memberships and roles | Owner can invite, change role and deactivate; changes apply on the next request |
| 1.5 | Properties and spaces | CRUD for the Property and Space tree; property scope limits what a membership sees |
| 1.6 | Grants and visibility | Grant and revoke with expiry; expired grants stop working without a restart; the Sharing screen lists all grants |
| 1.7 | Kid quick-login | Avatar and PIN login on a trusted device; PIN lockout; a parent can reset |
| 1.8 | Audit and soft delete | Sensitive reads and writes are logged; deleted items are recoverable for 30 days |

### Phase 2: Engine (3 weeks)

| ID | Story | Acceptance criteria |
| --- | --- | --- |
| 2.1 | Attachments | Upload, preview and download through the access check; limits enforced; duplicates stored once |
| 2.2 | Recurrence | Daily, weekly, monthly and yearly rules in Jalali and Gregorian; next-occurrence tests cover month ends and leap years |
| 2.3 | Checklists | Templates, nested items, assignment, due dates, progress; clone from a template |
| 2.4 | Tasks | Assign to a person, due date, recurring; list and board views |
| 2.5 | Reminders | Fire at due offsets; no duplicates; survive a restart; failed sends are retried |
| 2.6 | Channels | Link and verify Web Push, Telegram, Bale and SMS; fallback order and quiet hours respected |
| 2.7 | Tags and search v1 | Tag any item; search titles with permission filtering |

### Phase 3: Home, Assets, People (4–5 weeks)

| ID | Story | Acceptance criteria |
| --- | --- | --- |
| 3.1 | Home info | Utilities, meter readings, contacts and documents per property |
| 3.2 | Assets | CRUD with category, location, purchase and warranty data, photos and manual; warranty-expiry reminder |
| 3.3 | Maintenance plans | A plan built on a recurrence; marking it done creates a ServiceRecord and the next due date; reminder sent |
| 3.4 | People | Member, relative, pet and external profiles; guardian links; birthdays as important dates |
| 3.5 | Dossier | Health, education, documents and timeline with Guardians visibility by default; document-expiry reminders |
| 3.6 | Calendar | Month, week and agenda views in the user’s calendar combining tasks, due dates and important dates |

### Phase 4: Finance (5–6 weeks)

| ID | Story | Acceptance criteria |
| --- | --- | --- |
| 4.1 | Chart of accounts | A default family chart is seeded from a Full, Compact or Empty preset (section 6.8.1); add and edit accounts and categories; types enforced |
| 4.2 | Ledger engine | Entries must balance; posting and voiding by reversal; edits are logged; integer money only |
| 4.3 | Simple forms | Expense, income, transfer and split forms produce balanced entries; receipt attachment; property, person and asset dimensions |
| 4.4 | Reports | Monthly summary, category breakdown, cash flow, net worth, cost per property and asset; totals tie to the ledger |
| 4.5 | Budgets and recurring | Budget versus actual per category; recurring transactions that auto-post or remind |
| 4.6 | Cheques | Issued and received with a status flow and due reminders; a cleared cheque posts an entry; Sayad ID optional |
| 4.7 | Loans and installments | Schedule generation; paying an installment posts an entry; overdue view; interest-free loans supported |
| 4.8 | Reconciliation | Enter a statement balance per account; the difference is shown |
| 4.9 | Finance access | Module off for children by default; view-only grants for an accountant or consultant |

### Phase 5: Children, wallet, homework (3–4 weeks)

| ID | Story | Acceptance criteria |
| --- | --- | --- |
| 5.1 | Routines | Per-child daily routine with time slots, icons and points; completion in the kid shell |
| 5.2 | Points and rewards | Points ledger, reward catalogue, redemption request and parent approval |
| 5.3 | Kid shell | Simplified layout; no access to non-granted modules; usable at 360 px width |
| 5.4 | Homework | Subjects, assignments, due dates, status and parent check-off; reminders |
| 5.5 | Exams and schedule | Exam countdown and a school timetable view |
| 5.6 | Kid wallet | Virtual or bank-linked wallet; allowance rule posts transfers; balance comes from the ledger |
| 5.7 | Goals and requests | Savings-goal progress; withdrawal and purchase requests need parent approval |

### Phase 6: Library and Media (3 weeks)

| ID | Story | Acceptance criteria |
| --- | --- | --- |
| 6.1 | Books | CRUD, location in a Space, status, cover, ISBN entry, per-person read status |
| 6.2 | Lending | Loan with due date, reminder and return; a who-has-what view |
| 6.3 | Media | Films and series with watchlist, watching and done states, ratings, per person |
| 6.4 | Episodes | Seasons and episodes generated from counts; mark watched per person; next-to-watch shown |

### Phase 7: Dashboard, backup, AI (3 weeks)

| ID | Story | Acceptance criteria |
| --- | --- | --- |
| 7.1 | Dashboards | Parent, teen and child variants; due-soon and expiring items |
| 7.2 | Search v2 | All modules, permission-aware, Persian normalization of letters and digits |
| 7.3 | Backup UI | Schedule, targets, restore dry-run, household export |
| 7.4 | Import and export | CSV import for books, assets and transactions; JSON export |
| 7.5 | Optional AI | Per-household enable; cap enforced; invoice-to-draft and quick-add; no external calls while disabled |

### Phase 8: Hardening and release (2 weeks)

| ID | Story | Acceptance criteria |
| --- | --- | --- |
| 8.1 | Security review | The checklist in section 3.10 is verified; authorization tests pass |
| 8.2 | Performance | Typical pages respond in under 300 ms on the LAN with 10,000 seeded rows |
| 8.3 | Documentation | Install, upgrade, translate and contribute guides |
| 8.4 | Release | v1.0 tag with release notes; optional Docker packaging |

## 8. Non-functional requirements

- **Performance:** fast first paint on a mid-range phone over home Wi-Fi; no page blocks on external services.
- **Reliability:** background worker restarts cleanly; nightly backup verified by a restore test.
- **Security and privacy:** section 3.10; access rules covered by automated tests.
- **Accessibility:** WCAG 2.1 AA for the main shell and the kid shell.
- **Maintainability:** clear layering, migrations applied at startup, structured logs, CI running build, tests and a license check.

## 9. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Jalali components are missing in Radzen | Build and prove the custom component in Phase 0; keep it behind ICalendarService |
| Web Push may be unreliable on some networks | Telegram, Bale and SMS fallbacks; delivery log |
| Access-control leak between household members | Query filters plus automated isolation and role tests |
| Blazor Server connection drops on mobile | Reconnect UI, state kept in the database, short forms with drafts |
| Scope creep | Phase gates; each module ships only with acceptance criteria met |
| Finance correctness | Ledger invariants enforced in code; report totals tied to the ledger in tests |
| AGPL dependency conflicts | License check in CI |

## 10. After v1 and open items

- **Later:** English and other languages, Hijri calendar, offline PWA mode, bank CSV import, QR labels for assets and books, email channel, home automation integration, read-aloud for young children.
- **Open items:** review the default chart of accounts in section 6.8.1 and adjust it to your household.
