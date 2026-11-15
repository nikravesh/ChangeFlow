# نتایج اجرای Quickstart — تعریف کاربر و نقش اولیه

**تاریخ اجرا**: 2026-11-15  
**Build**: Release  
**Datastore**: SQLite file-backed موقت و مستقل از طریق `ChangeFlowApiFactory`  
**Secret handling**: تمام Setup Codeها و passwordها در زمان اجرا تولید شدند و مقدار آن‌ها در این سند ثبت نشده است.

## فرمان اجرا

```powershell
dotnet test tests/ChangeFlow.Tests/ChangeFlow.Tests.csproj -c Release --no-build --filter "Category!=Performance"
```

نتیجه واقعی:

```text
Passed! - Failed: 0, Passed: 40, Skipped: 0, Total: 40
Duration: 665 ms
```

## نتایج سناریوها

| سناریو | شاهد اجرایی | نتیجه |
|---|---|---|
| 1 — ایجاد موفق Requester | `CreateUserSuccessContractTests` و `CreateUserPersistenceTests`؛ پاسخ `201` و projection عمومی بررسی شد. | PASS |
| 2 — هر دو Role و deduplication | `UserCreationTests`، `CreateUserSuccessTests` و تست persistence؛ هر Role یک‌بار ثبت شد. | PASS |
| 3 — Setup Code غایب/نامعتبر | `CreateUserRejectionTests` و `CreateUserErrorContractTests`؛ پاسخ یکسان `401` همراه `WWW-Authenticate: Setup` و بدون mutation. | PASS |
| 4 — Password policy | تمام پنج قاعده در `CreateUserRejectionTests` مستقل و تجمیعی بررسی شدند؛ پاسخ عمومی password را echo نکرد. | PASS |
| 5 — تکرار بدون حساسیت به case | `CreateUserConflictTests.Serial_case_insensitive_duplicate_returns_conflict`؛ درخواست دوم `409`. | PASS |
| 6 — تکرار هم‌زمان | `CreateUserConflictTests.Concurrent_equivalent_identifiers_produce_one_created_and_one_conflict`؛ دقیقاً یک `201` و یک `409` و بدون نشت خطای SQLite. | PASS |
| 7 — rollback اتمیک | `CreateUserAtomicityTests` برای failure کنترل‌شده Credential و Role؛ شمار هر سه جدول پس از شکست صفر بود. | PASS |
| 8 — reuse و rotation کد | `CreateUserSuccessContractTests` استفاده مجدد و `UserSecurityAdapterTests` پذیرش کد جدید/ابطال فوری کد قبلی را بررسی کردند. | PASS |

## کنترل اطلاعات حساس

`CreateUserSensitiveLoggingTests` با password و hash شناخته‌شده ثابت کرد Authorization header، request body، password، password hash، Setup Code، نام نمایشی و شناسه ورود خام در Log ثبت نمی‌شوند. پاسخ‌های موفق و خطا نیز از نظر عدم افشای secret، stack trace و normalized identifier بررسی شدند.
