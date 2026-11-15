# Quickstart Validation: تعریف کاربر و نقش اولیه

**Feature**: `001-user-role-management`  
**Contract**: [contracts/openapi.yaml](contracts/openapi.yaml)  
**Data model**: [data-model.md](data-model.md)

این سند راهنمای اعتبارسنجی پس از Implementation است و کد پیاده‌سازی یا Task اجرایی ایجاد نمی‌کند.

## Prerequisites

- .NET 11 SDK مطابق `global.json`
- دسترسی نوشتن به مسیر فایل SQLite محلی و پوشه موقت آزمون
- digest یک Setup Code آزمایشی در secret store محلی؛ مقدار خام نباید در Repository، فایل تنظیمات یا Log ثبت شود
- متغیرهای محیطی محلی برای Setup Code و password آزمایشی؛ از secret واقعی استفاده نشود

پیکربندی مورد انتظار:

| Key | Purpose |
|-----|---------|
| `ConnectionStrings__ChangeFlow` | اتصال SQLite file-backed محلی، برای نمونه `Data Source=.local/changeflow.db;Default Timeout=5` |
| `Bootstrap__SetupCodeDigest` | digest `SHA-256` ثابت‌طول Setup Code فعال |

برای توسعه محلی، digest را از طریق user-secrets یا سازوکار secret تأییدشده تنظیم کنید؛ آن را در `appsettings*.json` قرار ندهید.

## Build and Automated Validation

از ریشه Repository اجرا شود:

```powershell
dotnet restore ChangeFlow.slnx
dotnet build ChangeFlow.slnx --no-restore
dotnet test ChangeFlow.slnx --no-build
```

نتیجه مورد انتظار:

- Build بدون warning/error امنیتی یا وابستگی ناسازگار موفق شود.
- آزمون‌های Domain و Application بدون HTTP Server و datastore بیرونی اجرا شوند.
- آزمون‌های Integration با فایل موقت SQLite واقعی و مستقل برای هر مجموعه آزمون اجرا شوند.
- آزمون قرارداد، خروجی Endpoint را با [OpenAPI contract](contracts/openapi.yaml) تطبیق دهد.

## Start the API

پس از آماده‌بودن مسیر SQLite و secret configuration:

```powershell
dotnet run --project src/03.Endpoints/Api/ChangeFlow.Endpoints.Api.csproj
```

Base URL واقعی را از خروجی امن برنامه دریافت کنید. نمونه‌های زیر فرض می‌کنند `$baseUrl`، `$env:CHANGEFLOW_SETUP_CODE` و `$env:CHANGEFLOW_TEST_PASSWORD` در همان shell با مقادیر آزمایشی تنظیم شده‌اند. هیچ‌یک را چاپ یا log نکنید.

## Scenario 1 — Successful Requester Creation

```powershell
$headers = @{ Authorization = "Setup $env:CHANGEFLOW_SETUP_CODE" }
$body = @{
  displayName = 'Test Requester'
  loginIdentifier = 'requester.one'
  password = $env:CHANGEFLOW_TEST_PASSWORD
  roles = @('Requester')
} | ConvertTo-Json
$result = Invoke-RestMethod -Method Post -Uri "$baseUrl/users" -Headers $headers -ContentType 'application/json' -Body $body
$result
```

Expected:

- HTTP `201`.
- پاسخ شامل `id`، `displayName`، `loginIdentifier` و `roles` است.
- پاسخ فاقد `password`، `passwordHash`، Setup Code و normalized identifier است.
- یک User، یک Credential hash و یک `Requester` assignment در datastore وجود دارد.

## Scenario 2 — Both Roles and Role Deduplication

درخواست معتبر با `roles: ["Requester", "Reviewer"]` باید `201` برگرداند و هر Role دقیقاً یک‌بار ثبت شود. آزمون Application/Domain باید ورودی تکراری یک Role را به یک assignment یکتا تبدیل کند و constraint مرکب نیز duplicate persisted را غیرممکن سازد.

## Scenario 3 — Missing or Invalid Setup Code

درخواست یکسان را یک بار بدون Authorization header و یک بار با کد نامعتبر ارسال کنید.

Expected for both:

- HTTP `401` و `WWW-Authenticate: Setup`.
- `code` عمومی یکسان برای missing و invalid.
- هیچ User، Credential یا Role جدیدی ثبت نشود.
- پاسخ و captured log هیچ مقدار credential یا Authorization header نداشته باشند.

## Scenario 4 — Password Policy

چهار درخواست جداگانه با passwordهای آزمایشی کمتر از ۸ نویسه و فاقد هر یک از uppercase، lowercase، number یا symbol ارسال کنید.

Expected:

- HTTP `400` با field error مربوط به `password`.
- همه قواعد نقض‌شده اعلام شوند، ولی مقدار password echo نشود.
- datastore بدون تغییر بماند.

## Scenario 5 — Case-Insensitive Duplicate

ابتدا `User1` و سپس `user1` را با داده معتبر ایجاد کنید.

Expected:

- درخواست اول `201`.
- درخواست دوم `409` با error code پایدار login conflict.
- فقط یک ردیف User با canonical identifier مشترک وجود داشته باشد.

## Scenario 6 — Concurrent Duplicate

آزمون Integration دو `POST /users` هم‌زمان با LoginIdentifierهای هم‌ارز از نظر case اجرا کند.

Expected:

- دقیقاً یک `201` و یک `409`.
- دقیقاً یک User، یک Credential و مجموعه کامل Roleهای درخواست موفق persist شود.
- exception مربوط به constraint از Infrastructure عبور نکند و جزئیات Provider در response دیده نشود.

## Scenario 7 — Atomic Rollback

در Integration test، failure کنترل‌شده در ذخیره Credential یا Role assignment ایجاد شود.

Expected:

- عملیات شکست بخورد و هیچ User بدون Credential یا Role باقی نماند.
- پاسخ عمومی فاقد stack trace و داده حساس باشد.
- خطای غیرمنتظره با correlation identifier قابل‌ردیابی باشد.

## Scenario 8 — Setup Code Reuse and Rotation

1. با کد فعال دو User متفاوت ایجاد کنید؛ هر دو باید `201` بگیرند.
2. digest کد را به‌صورت دستی در secret configuration تعویض و reload/redeployment را کامل کنید.
3. با کد قبلی درخواست جدید بفرستید؛ باید `401` بگیرد.
4. با کد جدید درخواست معتبر بفرستید؛ باید `201` بگیرد.

در تمام مراحل، مقدار کدها نباید در response یا captured log مشاهده شود.

## Acceptance Evidence

تحویل Feature باید شواهد زیر را نگه دارد، بدون ذخیره secret:

- فرمان و نتیجه واقعی `dotnet build` و `dotnet test`؛
- نتیجه آزمون‌های تمام Functional Requirementها و Acceptance Scenarioها؛
- assertion وضعیت persisted برای validation failure، duplicate، concurrency و rollback؛
- assertion عدم حضور password، hash و Setup Code در response و log capture؛
- اندازه‌گیری اینکه حداقل 95٪ درخواست‌های معتبر در محیط پذیرش طی حداکثر 2 ثانیه نتیجه می‌دهند.
