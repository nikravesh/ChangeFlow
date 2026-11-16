# ChangeFlow

ChangeFlow یک HTTP API برای ثبت و بررسی درخواست‌های تغییر است. Requester درخواست را ایجاد، ویرایش و ارسال می‌کند. Reviewer مستقل می‌تواند آن را تأیید کند یا با بازخورد برای اصلاح برگرداند. مالک حتی با داشتن نقش Reviewer نمی‌تواند درخواست خودش را بررسی کند. درخواست Approved تغییرناپذیر است.

## Stack و پیش‌نیازها

- C# و ASP.NET Core Minimal API روی `net11.0`، با SDK `11.0.100` طبق [global.json](global.json).
- Entity Framework Core و SQLite فایل‌محور برای اجرای محلی. فرایند API باید اجازهٔ نوشتن در پوشهٔ فایل SQLite را داشته باشد.
- xUnit و WebApplicationFactory برای آزمون‌های واحد، قرارداد HTTP و یکپارچگی روی SQLite.
- برای اجرای کامل گردش‌کار، یک صادرکنندهٔ سازگار با OpenID Connect با نشانی HTTPS قابل‌دسترسی از API، کلید عمومی امضا و امکان اتصال شناسهٔ حساب محلی به claim `sub` لازم است.

## اجرای محلی

فرمان‌ها را از ریشهٔ مخزن اجرا کنید. برای استفاده از پروفایل HTTPS، در صورت نیاز گواهی توسعه را با `dotnet dev-certs https --trust` برای محیط محلی مورد اعتماد کنید.

```powershell
dotnet restore ChangeFlow.slnx
dotnet build ChangeFlow.slnx --no-restore
dotnet test ChangeFlow.slnx --no-build
dotnet run --project src/03.Endpoints/Api/ChangeFlow.Endpoints.Api.csproj --launch-profile https
```

نشانی HTTPS پروفایل `https://localhost:7228` است. API هنگام راه‌اندازی Migrationهای ثبت‌شده را اعمال می‌کند. قبل از راه‌اندازی، متغیرهای زیر را برای همان فرایند تنظیم کنید.

| متغیر محیطی | مقدار موردنیاز |
|---|---|
| `ConnectionStrings__ChangeFlow` | رشتهٔ اتصال SQLite. نمونهٔ محلی `Data Source=changeflow.local.db;Foreign Keys=True;Default Timeout=5` است. پیش‌فرض برنامه `changeflow.db` است. |
| `Bootstrap__SetupCodeDigest` | خروجی هگز SHA-256 از بایت‌های UTF-8 کد راه‌اندازی فعال که از منبع امن دریافت می‌شود. |
| `ChangeRequestsAuthentication__Authority` | نشانی HTTPS صادرکنندهٔ مورد اعتماد برای دریافت OpenID metadata و JWKS. |
| `ChangeRequestsAuthentication__Audience` | مقدار مخاطب API که صادرکننده در access token قرار می‌دهد. |

## احراز هویت و گردش‌کار

1. برای ساخت حساب، `POST /users` را با هدر `Authorization: Setup <setup-code>` و فیلدهای `displayName`، `loginIdentifier`، `password` و `roles` فراخوانی کنید. نقش‌های پذیرفته‌شده `Requester` و `Reviewer` هستند. مقدار Setup Code و توکن‌ها را خارج از مخزن نگه دارید.
2. شناسهٔ `id` حساب برگشتی را از مسیر مدیریتی مورد اعتماد به صادرکننده متصل کنید تا access token همان شناسه را در `sub` داشته باشد. مسیرهای `/change-requests` از هدر `Authorization: Bearer <access-token>` استفاده می‌کنند. API امضا، صادرکننده، مخاطب و انقضا را بررسی و نقش جاری را از دیتابیس محلی می‌خواند.
3. درخواست را به‌عنوان Requester ایجاد و با مسیر `/change-requests/{id}/submit` ارسال کنید. Reviewer مستقل از `/approve` یا `/request-changes` روی همان درخواست استفاده می‌کند. برای ویرایش و فرمان‌های تغییر وضعیت، `expectedVersion` را به‌صورت رشته از نسخهٔ جاری بفرستید. نسخهٔ نامنطبق با `409 version_conflict` رد می‌شود.

[قرارداد ثبت کاربر](specs/001-user-role-management/contracts/openapi.yaml) · [قرارداد گردش‌کار](specs/002-change-request-workflow/contracts/http-api.md) · [راهنمای اعتبارسنجی](specs/002-change-request-workflow/quickstart.md)

## تصمیم‌های معماری

ChangeFlow یک Layered Monolith است. **Domain** ناورداها و انتقال وضعیت را اعمال می‌کند. **Application** گردش عملیات، مالکیت و مجوز جاری را بررسی می‌کند. **Infrastructure** جزئیات EF Core، SQLite و Migration را نگه می‌دارد. **API** ورودی و خروجی HTTP، احراز هویت و نگاشت خطا به ProblemDetails را بر عهده دارد.

ثبت تصمیم Review و تغییر وضعیت در یک تراکنش انجام می‌شوند. نسخهٔ هر درخواست با هر تغییر موفق افزایش می‌یابد. تاریخچه فقط در دسترس مالک یا Reviewer دارای نقش جاری و تصمیم موفق روی همان درخواست است.

[Constitution](.specify/memory/constitution.md) · [خط مبنای فنی](docs/architecture/technical-baseline.md) · [پژوهش و تصمیم‌های طراحی](specs/002-change-request-workflow/research.md)

## محدودیت‌های شناخته‌شده

- نسخهٔ فعلی HTTP API است. دریافت توکن به صادرکنندهٔ بیرونی وابسته است و مسیر ورود یا صدور توکن در برنامه وجود ندارد.
- حذف Draft، لغو، بازگشایی، واگذاری بررسی و فهرست عمومی خارج از محدوده‌اند. قابلیت‌های AI، RAG، MCP و Multi-Agent نیز در محدودهٔ تمرین نیستند.
- SQLite برای اجرای محلی انتخاب شده است. Provider محیط Production هنوز تعیین نشده است.
- مسیرهای فنی با صادرکنندهٔ آزمایشی و SQLite اعتبارسنجی شده‌اند. سنجش با کاربران نماینده و آزمون صادرکننده و TLS محیط استقرار هنوز انجام نشده‌اند. [گزارش اعتبارسنجی](specs/002-change-request-workflow/validation/build-test-results.md)

## نقش Coding Agent

از Codex به همراه Spec Kit برای تدوین و بازبینی مشخصات، طرح و کارها، پیاده‌سازی مرحله‌ای و اجرای آزمون‌ها استفاده شد. تصمیم‌های محصولی و معماری با بازبینی انسانی در Constitution، بخش Clarifications و Research ثبت می‌شوند. Analyze سازگاری اسناد را بررسی می‌کند و Converge شکاف‌های پیاده‌سازی را به کارهای قابل پیگیری تبدیل می‌کند.
