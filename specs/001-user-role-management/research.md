# Research: تعریف کاربر و نقش اولیه

**Feature**: `001-user-role-management`  
**Date**: 2026-11-14  
**Source of truth**: [spec.md](spec.md), `.specify/memory/constitution.md`

## وضعیت خط مبنای فنی

سند `docs/architecture/technical-baseline.md` مرجع تصمیم‌های سراسری است. تصمیم‌های زیر فقط جزئیات لازم Feature فعال را تکمیل می‌کنند و مواردی مانند SQLite که در Baseline صراحتاً Feature-specific شناخته شده‌اند، تصمیم سراسری محسوب نمی‌شوند.

## R-001 — Provider فعلی Feature و ORM

**Decision**: Entity Framework Core ORM مصوب پروژه است و SQLite با Provider `Microsoft.EntityFrameworkCore.Sqlite` فقط برای اجرای محلی و آموزشی Feature نخست انتخاب می‌شود. این تصمیم Provider سراسری Repository را تثبیت نمی‌کند.

**Rationale**: SQLite برای محیط آموزشی Feature نخست بدون سرویس دیتابیس مستقل قابل‌اجراست و تراکنش، unique constraint، foreign key و Migrationهای EF Core موردنیاز را فراهم می‌کند. استفاده از فایل واقعی SQLite به‌جای EF InMemory رفتار relational را حفظ می‌کند. جزئیات Provider در `Infra.DataAccess` محصور می‌شوند تا انتخاب Provider سراسری آینده به Domain/Application نشت نکند.

**Alternatives considered**:

- PostgreSQL + Npgsql: برای Production و concurrency گسترده گزینه قوی‌تری است، اما Provider سراسری هنوز تصویب نشده و برای اجرای آموزشی فعلی سرویس بیرونی اضافه می‌کند.
- SQL Server + EF Core: با اکوسیستم Microsoft سازگار است، اما انتخاب سراسری آن هنوز بررسی و تصویب نشده است.
- Dapper/ADO.NET: کنترل بیشتری می‌دهد، اما Mapping و Migration و orchestration دستی بیشتری تحمیل می‌کند.
- Persistence حافظه‌ای یا فایل: اتمیک‌بودن و uniqueness هم‌زمان را قابل‌اعتماد تأمین نمی‌کند.

**Baseline status**: Entity Framework Core قبلاً در Technical Baseline تثبیت شده است؛ SQLite تصمیم Feature-specific و محیطی است و **کاندید Technical Baseline پروژه نیست**. Provider سراسری باز می‌ماند.

## R-002 — مرز Persistence و تراکنش

**Decision**: Application یک قرارداد Feature-specific برای ایجاد اتمیک User و Credential و Role assignmentها تعریف می‌کند؛ پیاده‌سازی EF Core در `Infra.DataAccess` تمام داده‌ها را با یک `SaveChangesAsync` و یک تراکنش دیتابیس commit می‌کند. Generic Repository و افشای `DbContext` ممنوع است.

**Rationale**: قرارداد صریح، جهت وابستگی Constitution را حفظ می‌کند و Aggregate boundary را بدون abstraction عمومی CRUD نشان می‌دهد. یک commit واحد تضمین می‌کند یا User، Credential و تمام Roleها ثبت شوند یا هیچ‌کدام.

**Alternatives considered**:

- Generic Repository: abstraction زودهنگام و API مبهم ایجاد می‌کند.
- استفاده مستقیم Application از `DbContext`: Application را به ORM وابسته می‌کند.
- Repository جدا برای هر جدول یا چند commit: اتمیک‌بودن use case را تضعیف می‌کند.
- تراکنش توزیع‌شده: منبع داده دومی وجود ندارد.

**Baseline status**: با Technical Baseline مصوب هم‌راستاست؛ نام قرارداد و Aggregate مختص Feature است.

## R-003 — یکتایی شناسه ورود و تعارض هم‌زمان

**Decision**: مقدار اصلی `LoginIdentifier` برای نمایش و یک `NormalizedLoginIdentifier` برای مقایسه ذخیره می‌شوند. Normalization شامل trim و `ToUpperInvariant()` است. یک unique constraint روی مقدار normalized مرجع نهایی uniqueness است. خطای Provider فقط برای constraint شناخته‌شده به نتیجه typed تعارض و سپس HTTP `409` ترجمه می‌شود.

**Rationale**: pre-check به‌تنهایی race condition دارد. قید یکتای SQLite تضمین نهایی uniqueness است و با transaction کوتاه و busy timeout محدود اجازه می‌دهد درخواست رقیب پس از آزادشدن writer، constraint را مشاهده کند. نگهداری مقدار normalized رفتار کسب‌وکار را از collation و قابلیت اختصاصی Provider جدا می‌کند.

**Alternatives considered**:

- فقط query پیش از insert: race-prone است.
- `citext` یا collation case-insensitive: رفتار را Provider-specific می‌کند.
- lock درون process: در چند instance معتبر نیست.
- `Serializable` یا distributed lock: برای invariant یکتایی سربار و پیچیدگی غیرضروری دارد.
- Idempotency key: نیازمندی تأییدشده‌ای ندارد؛ تکرار سریالی باید `409` بگیرد.

**Baseline status**: حفاظت datastore از uniqueness و محصورسازی خطای Provider با Technical Baseline هم‌راستاست؛ normalization و نام constraint مختص Feature است.

### سیاست SQLite برای رقابت writerها

**Decision**: عملیات create با transaction کوتاه و `Default Timeout=5` اجرا می‌شود. خطاهای SQLite `BUSY` یا `LOCKED` باعث retry محدود کل عملیات، حداکثر سه بار با وقفه‌های ۵۰، ۱۰۰ و ۲۰۰ میلی‌ثانیه می‌شوند. هر retry باید با `DbContext` و transaction جدید انجام شود. پس از commit درخواست برنده، retry درخواست رقیب باید unique constraint را مشاهده و به `LoginIdentifierConflict` تبدیل شود. اگر retry budget پایان یافت، Infrastructure با `DbContext` جدید وجود `NormalizedLoginIdentifier` را بررسی می‌کند: وجود آن MUST به `LoginIdentifierConflict` تبدیل شود تا تمام بازنده‌های رقابت همان شناسه نتیجه تعارض بگیرند؛ فقط اگر شناسه وجود نداشته باشد، نتیجه transient infrastructure failure مجاز است و نباید به conflict جعلی تبدیل شود.

**Rationale**: SQLite نوشتن‌ها را serialize می‌کند و ممکن است پیش از ارزیابی unique constraint خطای lock برگرداند. retry محدود و بررسی نهایی شناسه، رفتار FR-010 و SC-004 را برای رقابت روی یک شناسه حفظ می‌کنند، بدون آنکه lock failure نامرتبط و فاقد User برنده به conflict کسب‌وکار تبدیل شود.

**Alternatives considered**:

- نگاشت مستقیم `BUSY/LOCKED` به `409`: علت فنی نامرتبط را به تعارض کسب‌وکار جعلی تبدیل می‌کند.
- retry نامحدود: زمان پاسخ غیرقابل‌کنترل و خطر ازدحام ایجاد می‌کند.
- distributed lock: برای SQLite محلی و آموزشی پیچیدگی غیرضروری دارد.

**Baseline status**: سیاست retry مختص Provider فعلی Feature است و Technical Baseline سراسری محسوب نمی‌شود.

## R-004 — Hash رمز عبور

**Decision**: `PasswordHasher<TUser>` از ASP.NET Core Identity Core پشت یک abstraction در Application استفاده می‌شود. فقط hash نسخه‌دار و salt‌شده ذخیره می‌شود؛ plaintext فقط در طول اجرای use case در حافظه باقی می‌ماند و هرگز persist یا log نمی‌شود.

**Rationale**: Hasher استاندارد چارچوب فرمت نسخه‌دار، salt تصادفی و امکان ارتقای پارامترها را بدون پذیرش مدل کامل ASP.NET Core Identity فراهم می‌کند. Domain و Application به implementation رمزنگاری وابسته نمی‌شوند.

**Alternatives considered**:

- ASP.NET Core Identity کامل: Schema و مدل دامنه بیشتری از نیاز فعلی وارد می‌کند.
- PBKDF2 سفارشی با `Rfc2898DeriveBytes`: مسئولیت format، salt و migration امنیتی را به پروژه منتقل می‌کند.
- Argon2 ثالث: گزینه معتبری است، اما dependency و سیاست سراسری جدیدی می‌افزاید و برای نسخه اولیه ساده‌ترین انتخاب نیست.
- رمز خام یا رمزنگاری برگشت‌پذیر: به دلایل امنیتی مردود است.

**Baseline status**: با قاعده مصوب Technical Baseline برای hash استاندارد، salt‌شده و نسخه‌پذیر هم‌راستاست؛ انتخاب `PasswordHasher<TUser>` مختص این Plan است.

## R-005 — اعتبارسنجی و تعویض Setup Code

**Decision**: digest `SHA-256` ثابت‌طول Setup Code از منبع پیکربندی امن خارج از Repository دریافت می‌شود. digest `SHA-256` ورودی محاسبه و با `CryptographicOperations.FixedTimeEquals` مقایسه می‌شود. کد چندبارمصرف است؛ تعویض دستی یعنی جایگزینی digest فعال و اعمال reload یا redeployment، و از لحظه اعمال، digest قبلی نامعتبر است.

**Rationale**: این طراحی با چرخه عمر تأییدشده Spec سازگار است، secret را از کد و فایل تنظیمات commit‌شده دور نگه می‌دارد و نشت timing را کاهش می‌دهد. Setup Code state دامنه‌ای یا رکورد دیتابیس نیست.

**Alternatives considered**:

- مقایسه رشته‌ای یا نگهداری plaintext مورد انتظار: در برابر timing و افشای configuration ضعیف‌تر است.
- ذخیره در دیتابیس: برای یک secret مشترک state و migration غیرضروری ایجاد می‌کند.
- کد یک‌بارمصرف یا دارای انقضا: با تصمیم Clarify تعارض دارد.
- hard-code یا `appsettings*.json` commit‌شده: مغایر Constitution است.

**Baseline status**: مدیریت secret خارج از Repository و مقایسه زمان‌ثابت در Technical Baseline مصوب است؛ digest و reusable بودن Setup Code مختص این Feature است.

## R-006 — مرزبندی مسئولیت امنیتی

**Decision**: Domain ناورداهای User، LoginIdentifier و Role را نگه می‌دارد. Application سیاست رمز عبور، بررسی Setup Code و orchestration ایجاد را اجرا و قراردادهای hashing/validation/persistence را تعریف می‌کند. Infrastructure adapterهای hash، configuration و persistence را پیاده‌سازی می‌کند. Endpoint فقط bind، validation شکلی و mapping HTTP را انجام می‌دهد.

**Rationale**: قواعدی که به مجوز caller و سرویس بیرونی وابسته‌اند طبق Constitution در Application باقی می‌مانند و Domain/Application بدون HTTP و سرویس واقعی تست‌پذیر می‌مانند.

**Alternatives considered**:

- بررسی Setup Code یا hash فقط در Endpoint: امکان دورزدن کنترل Application را ایجاد می‌کند.
- وابستگی Domain به Identity/Crypto: استقلال Domain را نقض می‌کند.
- پروژه Infrastructure جدید: نیاز تأییدشده‌ای ندارد و ساختار ثابت Solution را تغییر می‌دهد.

**Baseline status**: مرزبندی Credential/Authorization مطابق Technical Baseline و Constitution است.

## R-007 — قرارداد HTTP و انتقال secret

**Decision**: `POST /users` تعریف می‌شود. Setup Code در `Authorization: Setup <code>` ارسال می‌شود. Body شامل `displayName`، `loginIdentifier`، `password` و `roles` است. پاسخ موفق `201 Created` فقط `id`، `displayName`، `loginIdentifier` و `roles` را برمی‌گرداند. چون endpoint خواندن User در Scope نیست، `Location` به مسیر تعریف‌نشده تولید نمی‌شود.

**Rationale**: POST با ساخت Resource سازگار است. Authorization header نسبت به query/body مسیر عملیاتی شناخته‌شده‌تری برای redaction دارد و secret را از مدل دامنه جدا نگه می‌دارد.

**Alternatives considered**:

- `X-Setup-Code`: قابل‌استفاده است، اما redaction استاندارد کمتری دارد.
- query string: در URL، proxy و access log بیشتر افشا می‌شود.
- body: ممکن است در body logging یا validation snapshot ثبت شود.
- `PUT` یا `202`: شناسه توسط سامانه تولید می‌شود و عملیات فوری است؛ بنابراین مناسب نیستند.

**Baseline status**: حذف secret از خروجی مطابق Technical Baseline است؛ scheme اختصاصی `Setup` تصمیم Feature-specific باقی می‌ماند.

## R-008 — نگاشت خطا و مشاهده‌پذیری

**Decision**: خطاها با Problem Details و `code` پایدار ماشینی نگاشت می‌شوند: `400` برای validation، `401` یکسان برای Setup Code غایب/نامعتبر همراه `WWW-Authenticate: Setup`، `409` برای login conflict، و `500` عمومی همراه correlation identifier. Log ساخت‌یافته فقط use case، outcome، error code، duration و correlation id را ثبت می‌کند؛ Authorization header، body کامل، password، password hash، Setup Code و login identifier خام ثبت نمی‌شوند.

**Rationale**: Client می‌تواند validation، credential failure و conflict را تفکیک کند، بدون دریافت exception داخلی یا oracle درباره Setup Code. correlation id قابلیت ردیابی امن را حفظ می‌کند.

**Alternatives considered**:

- `403` برای Setup Code نامعتبر: credential معتبر ارائه نشده و `401` دقیق‌تر است.
- `422` برای validation: معتبر است، اما `400` با convention ساده‌تر پروژه یکنواخت‌تر است.
- log کامل request/response یا secret ماسک‌شده: همچنان خطر افشای اطلاعات احراز هویت دارد.
- ثبت rejectionهای مورد انتظار در سطح Error: نویز و مغایرت با Constitution ایجاد می‌کند.

**Baseline status**: Problem Details، error code پایدار، correlation logging و redaction در Technical Baseline مصوب‌اند؛ status mapping جزئی مختص قرارداد Feature است.

## R-009 — راهبرد آزمون

**Decision**: آزمون‌ها در همان پروژه `tests/ChangeFlow.Tests` و به‌صورت Feature-based در چهار سطح سازمان‌دهی می‌شوند: Domain unit، Application unit با test double، Integration با فایل موقت SQLite واقعی، و HTTP contract/end-to-end با `WebApplicationFactory`. رقابت هم‌زمان، constraintها، transaction rollback و عدم ذخیره plaintext با Provider فعلی Feature اعتبارسنجی می‌شوند.

**Rationale**: Constitution تست Domain/Application بدون HTTP را الزام می‌کند و آزمون حساس باید وضعیت persisted را نیز بررسی کند. mock دیتابیس قادر به اثبات رفتار unique constraint و transaction واقعی نیست.

**Alternatives considered**:

- فقط unit tests: رفتار Provider، Migration و transaction را اثبات نمی‌کند.
- فقط end-to-end: کندتر است و محل شکست قواعد را مشخص نمی‌کند.
- EF InMemory یا SQLite in-memory با connection ناپایدار: constraint، transaction و lifecycle فایل Provider فعلی را به‌اندازه SQLite file-backed بازنمایی نمی‌کند.
- Testcontainers: برای Provider فعلی SQLite نیاز نیست؛ اگر Provider سراسری آینده سرویس‌محور باشد در Plan همان مرحله بازبینی می‌شود.
- mock برای concurrency: race واقعی را پوشش نمی‌دهد.

**Baseline status**: xUnit، `WebApplicationFactory` و آزمون Provider واقعی/نزدیک به واقعیت در Technical Baseline مصوب‌اند؛ SQLite file-backed انتخاب فعلی Feature است.

## نتیجه Phase 0

همه تصمیم‌های فنی لازم برای Phase 1 حل شده‌اند و هیچ ابهام فنی حل‌نشده‌ای باقی نمانده است. SQLite و سایر جزئیات علامت‌خورده فقط در محدوده Plan این Feature معتبرند و Provider سراسری را تعیین نمی‌کنند.
