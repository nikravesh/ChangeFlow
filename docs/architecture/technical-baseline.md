# خط مبنای فنی پروژه ChangeFlow

**نسخه سند**: 1.0.0  
**تاریخ تصویب**: 2026-11-14  
**وضعیت**: مصوب برای استفاده به‌عنوان پیش‌فرض معماری Repository

## هدف و دامنه

این سند تصمیم‌های فنی سراسری ChangeFlow را ثبت می‌کند؛ تصمیم‌هایی که برای همه Featureها معتبرند یا از این تاریخ به بعد پیش‌فرض معماری پروژه محسوب می‌شوند. این سند رفتار محصول را تعریف نمی‌کند و جایگزین Specification، Plan یا Research هر Feature نیست.

منابع حاکم بر این سند عبارت‌اند از:

- [Constitution پروژه](../../.specify/memory/constitution.md) که مرجع بالادستی و الزام‌آور است؛
- [راهنمای عامل‌های توسعه](../../AGENTS.md) که ترتیب مطالعه و قواعد کار در Repository را تعیین می‌کند؛
- [Plan قابلیت تعریف کاربر و نقش](../../specs/001-user-role-management/plan.md) و [Research همان قابلیت](../../specs/001-user-role-management/research.md) که منشأ ارزیابی نخستین تصمیم‌های فنی بوده‌اند.

در صورت تعارض، Constitution اولویت دارد. تصمیم Feature-specific فقط زمانی می‌تواند این سند را تغییر دهد که نیاز سراسری آن مستند، گزینه‌های جایگزین در Research بررسی و اثر تغییر بر Featureهای موجود بازبینی شده باشد.

## پلتفرم و ابزار ساخت

- زبان اصلی پروژه C# است.
- Target Framework همه پروژه‌های اجرایی و کتابخانه‌ای `net11.0` است.
- چارچوب اصلی HTTP API پروژه ASP.NET Core است.
- نسخه .NET SDK باید در `global.json` تثبیت شود و Build محلی و CI از نسخه سازگار یکسان استفاده کنند.
- قابلیت‌های preview به‌صورت پیش‌فرض مجاز نیستند؛ استفاده از آن‌ها به تصمیم مستند و بازبینی اثر نیاز دارد.

این موارد از Constitution ناشی می‌شوند و Featureها مجاز نیستند آن‌ها را دوباره انتخاب یا به‌صورت محلی بازتعریف کنند.

## معماری و جهت وابستگی‌ها

ChangeFlow یک Layered Monolith با سازمان‌دهی Feature-based درون هر پروژه است. ساختار پروژه‌ها و جهت وابستگی‌ها همان ساختار الزام‌شده در Constitution است.

- `ChangeFlow.Core.Domain` مالک Entityها، Value Objectها، ناورداهای ذاتی و قواعد انتقال وضعیت است و به پروژه دیگری وابسته نمی‌شود.
- `ChangeFlow.Core.Application` مالک orchestration موارد کاربرد، کنترل Permission وابسته به Use Case و قرارداد وابستگی‌های بیرونی است و فقط به Domain وابسته می‌شود.
- `ChangeFlow.Infra.DataAccess` پیاده‌سازی Persistence و adapterهای وابسته به فناوری بیرونی را در محدوده ساختار مصوب فراهم می‌کند و جزئیات آن را از Core پنهان نگه می‌دارد.
- `ChangeFlow.Endpoints.Api` مسئول قرارداد HTTP، اعتبارسنجی شکلی، mapping، composition و فراخوانی Application است و Business Logic را در خود نگهداری نمی‌کند.
- وابستگی چرخه‌ای، نشت ORM یا Provider به Domain و تصمیم‌گیری کسب‌وکار در Endpoint یا Infrastructure ممنوع است.
- فایل‌ها در هر پروژه براساس Feature سازمان‌دهی می‌شوند. پوشه یا abstraction عمومی فقط زمانی ایجاد می‌شود که چند نیاز واقعی و مستند آن را توجیه کنند.

قراردادهای Application باید هدف Use Case را بیان کنند. Generic Repository یا abstraction عمومی CRUD پیش‌فرض پروژه نیست و صرفاً با نیاز مستند قابل معرفی است.

## Persistence و Schema

- ORM رابطه‌ای استاندارد پروژه Entity Framework Core است.
- Provider دیتابیس سراسری هنوز انتخاب نشده است. هر Plan باید با Provider مصوب زمان خود سازگار باشد و نباید انتخاب محلی را به تصمیم کل Repository تبدیل کند.
- SQLite فقط انتخاب فعلی برای اجرای محلی و آموزشی Feature نخست است و Database Baseline پروژه محسوب نمی‌شود.
- `DbContext`، mapping، Migration، exceptionهای Provider و سایر جزئیات ORM باید در Infrastructure محصور بمانند.
- Domain و Application نباید به Entity Framework Core، نوع داده اختصاصی Provider یا محصول دیتابیس وابسته شوند.
- تغییر Schema فقط از طریق Migration نسخه‌بندی‌شده، قابل‌تکرار و قابل‌اعتبارسنجی انجام می‌شود.
- قیدهای قطعی داده مانند uniqueness و referential integrity باید در مرز مناسب Domain و datastore حفاظت شوند. بررسی Application به‌تنهایی جایگزین constraint اتمیک datastore نیست.
- exceptionهای فنی Persistence نباید مستقیم به Application یا HTTP نشت کنند؛ Infrastructure آن‌ها را فقط در صورت شناخت دقیق علت به نتیجه قابل‌پیش‌بینی ترجمه می‌کند.

انتخاب محصول دیتابیس، Provider EF Core، راهبرد اجرای Migration و تنظیمات عملیاتی datastore همچنان تصمیم سراسری باز هستند.

## Authentication و Authorization

Authentication و Authorization دو مسئولیت مستقل‌اند.

- شناسایی هویت کاربر به‌تنهایی Permission انجام عملیات را ایجاد نمی‌کند.
- Permission جاری، نقش‌ها، مالکیت منبع و وضعیت مرتبط باید هنگام هر عملیات حساس از منبع معتبر سمت سرور بررسی شوند.
- claimهای Client، Token، Header، Body یا سایر ورودی‌های HTTP به‌تنهایی Source of Truth برای Permission نیستند.
- Endpoint می‌تواند اطلاعات هویت را استخراج کند، اما تصمیم Authorization وابسته به Use Case در Application اعمال می‌شود.
- عملیات ردشده نباید تغییر پایدار یا اثر جانبی ناقص ایجاد کند.
- مکانیزم Authentication، Identity Provider، قالب Token و چرخه session هنوز به‌صورت سراسری تثبیت نشده‌اند و باید در Plan/Research قابلیت مرتبط تعیین شوند.

در صورت استفاده از password محلی، plaintext نباید persist، log یا بازگردانده شود. hashing باید با primitive استاندارد، salt‌شده و دارای فرمت نسخه‌پذیر، پشت abstraction مناسب انجام شود. این قاعده استفاده از ASP.NET Core Identity کامل را الزامی نمی‌کند.

## Secret و اطلاعات حساس

- password، Token، API key، Setup Code، connection credential و سایر Secretها نباید در کد، فایل commit‌شده، Fixture، Snapshot، Log یا پاسخ HTTP ثبت شوند.
- Secretها باید از منبع پیکربندی امن خارج از Repository دریافت شوند.
- مقایسه Secret در جایی که خطر timing disclosure وجود دارد باید با primitive زمان‌ثابت انجام شود.
- logging یا tracing کامل request/response برای مسیرهای دارای Credential مجاز نیست؛ redaction باید پیش از ثبت داده اعمال شود.
- انتخاب محصول Secret Store، راهبرد rotation و نحوه reload تنظیمات هنوز تصمیم عملیاتی باز است.

## قرارداد خطای HTTP

- تمام خطاهای HTTP باید با `ProblemDetails` یا شکل توسعه‌یافته سازگار با آن نمایش داده شوند.
- خطاهایی که Client می‌تواند براساس آن‌ها تصمیم بگیرد باید `code` پایدار و machine-readable داشته باشند.
- status code و error code باید معنای خطا را بدون افشای جزئیات داخلی بیان کنند؛ نگاشت دقیق هر Use Case در قرارداد همان Feature ثبت می‌شود.
- StackTrace، متن داخلی Exception، جزئیات Provider، Credential، Secret و داده حساس نباید در پاسخ خطا قرار گیرند.
- خطای غیرمنتظره باید پیام عمومی و correlation identifier قابل‌ارجاع داشته باشد.
- خطاهای مورد انتظار Domain یا Application نباید به‌عنوان خرابی زیرساختی گزارش شوند.

این Baseline سبک یگانه‌ای مانند Minimal API یا Controller را تحمیل نمی‌کند. هر سبک باید مرز Endpoint و Application و قرارداد خطای فوق را حفظ کند.

## مشاهده‌پذیری و Logging

- Logها باید ساخت‌یافته و دارای correlation identifier باشند.
- اطلاعات کافی برای تشخیص Use Case، نتیجه، کد خطا و مدت اجرا ثبت می‌شود، بدون ثبت payload حساس.
- خطاهای مورد انتظار با سطح متناسب ثبت می‌شوند و نباید به‌طور پیش‌فرض در سطح خطای زیرساختی گزارش شوند.
- exceptionهای غیرمنتظره با context فنی کافی در سمت سرور ثبت می‌شوند، اما جزئیات آن‌ها به Client بازگردانده نمی‌شود.
- ابزار و backend نهایی Logging، Metrics و Tracing هنوز انتخاب سراسری نشده‌اند.

## Concurrency و Atomicity

- هر Use Case که اجرای آن براساس وضعیت قدیمی می‌تواند نتیجه کسب‌وکاری نادرست یا lost update ایجاد کند باید Concurrency Control صریح داشته باشد.
- مکانیزم دقیق Concurrency متناسب با Aggregate، Persistence Provider، نوع عملیات و قرارداد بیرونی در Plan همان Feature انتخاب می‌شود.
- unique constraint، optimistic concurrency token، قفل، isolation level یا ETag ابزارهای ممکن‌اند؛ هیچ‌کدام بدون تحلیل Use Case پیش‌فرض اجباری همه موجودیت‌ها نیستند.
- `Guid Version` برای همه Aggregateها و `ETag`/`If-Match` برای همه APIها Baseline پروژه نیستند.
- تعارض باید نتیجه صریح و قابل‌تشخیص تولید کند و نباید تغییر جدیدتر را بی‌صدا بازنویسی کند.
- تغییراتی که از دید کسب‌وکار یک عملیات واحد هستند باید در مرز تراکنشی مناسب به‌صورت اتمیک commit یا به‌طور کامل rollback شوند.
- مرز تراکنش براساس Use Case تعریف می‌شود؛ transaction توزیع‌شده یا lock سراسری فقط با نیاز مستند مجاز است.

## آزمون و اعتبارسنجی

- Framework اصلی آزمون پروژه xUnit است.
- برای Integration Testهای ASP.NET Core از `WebApplicationFactory` استفاده می‌شود.
- قواعد Domain و Application باید بدون HTTP Server و بدون وابستگی غیرضروری به سرویس بیرونی واقعی قابل‌آزمون باشند.
- تست‌های Integration باید در سطح مناسب با Provider واقعی Production یا محیطی نزدیک به آن اجرا شوند؛ test double جایگزین آزمون constraint، transaction یا رفتار Provider نیست.
- SQLite، EF Core InMemory یا mock datastore فقط زمانی مجازند که رفتار مورد آزمون به تفاوت Provider وابسته نباشد؛ آن‌ها مدرک کافی برای سازگاری Production نیستند.
- هر Functional Requirement و Acceptance Scenario باید پوشش خودکار قابل‌ردیابی داشته باشد.
- در سناریوهای حساس علاوه بر نتیجه بیرونی، وضعیت persisted و نبود تغییر ناقص نیز بررسی می‌شود.
- ابزار orchestration محیط Integration، از جمله Testcontainers، می‌تواند در Plan انتخاب شود و در حال حاضر Baseline اجباری کل پروژه نیست.

## تصمیم‌های عمداً خارج از Baseline

موارد زیر تصمیم‌های Feature-specific، محیطی یا هنوز باز هستند و نباید به‌عنوان قاعده سراسری تفسیر شوند:

- SQLite به‌عنوان دیتابیس اصلی پروژه؛
- PostgreSQL، SQL Server یا هر Provider مشخص دیگر تا زمان تصویب سراسری؛
- Minimal API به‌عنوان تنها سبک مجاز API؛
- opaque bearer credential یا هر scheme مشخص دیگر به‌عنوان روش دائمی Authentication؛
- Setup Code، نحوه انتقال آن، digest آن و چرخه reuse/rotation مربوط به Feature نخست؛
- URL، status mapping جزئی، password policy، Roleها و مدل داده قابلیت تعریف کاربر؛
- `Guid Version` برای تمام Aggregateها؛
- `ETag`/`If-Match` برای تمام APIهای آینده؛
- Testcontainers به‌عنوان تنها ابزار مجاز Integration Test؛
- normalization مشخص یک شناسه یا نام constraintهای دیتابیس خارج از Feature تعریف‌کننده آن.

## تصمیم‌های سراسری باز

تصمیم‌های زیر هنوز باید در Research و Plan قابلیت مرتبط بررسی و پس از بازبینی انسانی تثبیت شوند:

1. محصول دیتابیس و Provider سراسری Entity Framework Core؛
2. راهبرد اجرای Migration در محیط‌های توسعه، CI و Production؛
3. مکانیزم Authentication، Identity Provider، قالب Token و مدیریت session؛
4. محصول Secret Store و فرایند عملیاتی rotation/reload؛
5. backend و استاندارد عملیاتی Logging، Metrics و Distributed Tracing؛
6. مدل deployment و hosting؛
7. مکانیزم Concurrency هر Aggregate یا API که هنوز Specification و Plan ندارد؛
8. سبک Endpoint برای هر Feature، مادامی که مرزبندی لایه‌ها حفظ شود.

## حاکمیت و تغییر سند

- تغییر این سند نباید اصل یا ساختار الزام‌شده Constitution را دور بزند.
- هر تصمیم جدید باید نیاز سراسری، دلیل انتخاب، گزینه‌های جایگزین و اثر بر Spec، Plan، Code و Tests موجود را مستند کند.
- تصمیم موقت یا آزمایشی ابتدا در Research/Plan Feature باقی می‌ماند و فقط پس از بازبینی انسانی به Baseline منتقل می‌شود.
- اگر تصمیم پیشنهادی با Constitution تعارض دارد، ابتدا باید فرایند اصلاح Constitution طی شود؛ تغییر این سند به‌تنهایی مجاز نیست.
- Featureها باید این سند را پس از Constitution و پیش از Plan و Implementation بخوانند و هر انحراف را صریح ثبت کنند.
