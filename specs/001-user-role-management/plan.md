# Implementation Plan: تعریف کاربر و نقش اولیه

**Branch**: `001-user-role-management` | **Date**: 2026-11-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-user-role-management/spec.md`

## Summary

این Feature یک عملیات HTTP برای ایجاد اتمیک User، Credential و یک یا دو Role توسط Anonymous دارای Setup Code معتبر فراهم می‌کند. طراحی، Layered Monolith و ساختار چهارپروژه‌ای مصوب را حفظ می‌کند: Domain ناورداهای User و Role را اعمال می‌کند؛ Application اعتبار Setup Code، سیاست رمز عبور و orchestration را بر عهده دارد؛ Infrastructure داده را با EF Core و Provider فعلی SQLite در یک تراکنش ذخیره می‌کند؛ Endpoint قرارداد HTTP و Problem Details را map می‌کند. رمز عبور به hash نسخه‌دار تبدیل می‌شود، Setup Code خارج از Repository نگهداری می‌شود و unique constraint شناسه normalized مرجع نهایی تعارض create است. SQLite فقط انتخاب محلی و آموزشی این Feature است و Provider سراسری پروژه را تثبیت نمی‌کند.

## Technical Context

**Language/Version**: C# روی `net11.0` با SDK سازگار و pin‌شده در `global.json`؛ بدون قابلیت preview

**Primary Dependencies**: ASP.NET Core، Entity Framework Core، Microsoft.EntityFrameworkCore.Sqlite، ASP.NET Core Identity Core `PasswordHasher<TUser>`؛ برای آزمون xUnit و `WebApplicationFactory`

**Storage**: SQLite file-backed با Migrationهای EF Core برای اجرای محلی و آموزشی Feature نخست؛ Setup Code در منبع پیکربندی امن خارج از Repository و دیتابیس

**Testing**: `dotnet test` با آزمون‌های Domain، Application، Integration روی SQLite file-backed واقعی و HTTP contract/end-to-end با `WebApplicationFactory`

**Target Platform**: ASP.NET Core HTTP API روی میزبان سازگار با .NET 11؛ انتخاب deployment/hosting خارج از Scope این Feature است

**Project Type**: Web service در قالب Layered Monolith و سازمان‌دهی Feature-based

**Performance Goals**: مطابق SC-002، حداقل 95٪ ایجادهای معتبر حداکثر ظرف 2 ثانیه نتیجه قابل‌مشاهده داشته باشند

**Constraints**: create اتمیک؛ uniqueness بدون حساسیت به case؛ دقیقاً یک موفقیت در رقابت یک شناسه؛ transaction کوتاه SQLite با `Default Timeout=5`؛ retry خطاهای `BUSY/LOCKED` حداکثر سه بار با وقفه‌های ۵۰، ۱۰۰ و ۲۰۰ میلی‌ثانیه و `DbContext`/transaction جدید؛ بررسی نهایی شناسه normalized پس از پایان retry برای تبدیل رقابت همان شناسه به `LoginIdentifierConflict`؛ عدم persistence/log/response برای plaintext password و Setup Code؛ فقط نقش‌های `Requester` و `Reviewer`

**Scale/Scope**: هدف حجمی صریحی در Spec تعیین نشده است؛ طراحی برای یک API چنددرخواستی با datastore مشترک است و بدون distributed lock به constraint دیتابیس تکیه می‌کند

## Constitution Check

*GATE: پیش از Research و پس از Phase 1 بررسی شده است.*

| اصل / Gate | ارزیابی پیش از Research | ارزیابی پس از Design |
|-------------|-------------------------|----------------------|
| Specification-first و traceability | PASS — Spec و Clarification تأییدشده منبع رفتار است | PASS — قرارداد، مدل و Quickstart به FR/SCها ارجاع دارند |
| Layered Monolith و Feature-based | PASS — هیچ پروژه یا لایه جدیدی پیشنهاد نشده است | PASS — تمام مسیرها زیر Feature `Users` در چهار پروژه مصوب قرار می‌گیرند |
| C# / ASP.NET Core / `net11.0` | PASS — تصمیم Constitution بدون بازانتخاب استفاده شده است | PASS — dependencies و ساختار با پلتفرم ثابت سازگارند |
| جهت وابستگی | PASS — Domain مستقل، Application فقط وابسته به Domain | PASS — ORM/Crypto/Config در Infrastructure و mapping HTTP در Endpoint محصور است |
| Authorization در Application | PASS — Setup Code یک مجوز use case است و فقط Endpoint بررسی نمی‌کند | PASS — `ISetupCodeValidator` در مرز Application و adapter در Infrastructure طراحی شده است |
| ناوردا و state transition | PASS — User هنگام creation کامل ساخته می‌شود و state machine اضافی ندارد | PASS — نقش‌ها و شناسه canonical در Domain؛ رفتار تعریف‌نشده اضافه نشده است |
| Concurrency و اتمیک‌بودن | PASS — نیاز به تحقیق ثبت شد | PASS — unique constraint + transaction واحد + retry محدود `BUSY/LOCKED` + بررسی نهایی شناسه و نگاشت صریح `409` طراحی شده است |
| امنیت و logging | PASS — secrets و credentialها شناسایی شدند | PASS — hashing، constant-time compare، redaction و Problem Details عمومی تعریف شده است |
| آزمون‌پذیری | PASS — سطوح لازم شناسایی شد | PASS — Domain/Application مستقل از HTTP و integration با Provider واقعی طراحی شده است |
| سادگی و توسعه تدریجی | PASS — فقط use case تأییدشده طراحی شد | PASS — Identity کامل، Generic Repository، distributed lock و پروژه جدید رد شدند |
| Technical Baseline | PASS — `docs/architecture/technical-baseline.md` خوانده شد؛ EF Core مصوب و Provider سراسری باز است | PASS — SQLite فقط انتخاب محلی/آموزشی Feature نخست است و به Domain/Application نشت نمی‌کند |

**Gate result**: هیچ نقض Constitution یا Technical Baseline وجود ندارد. انتخاب SQLite در [research.md](research.md) به‌عنوان تصمیم Feature-specific ثبت شده و Provider سراسری پروژه همچنان باز است.

## Project Structure

### Documentation (this feature)

```text
specs/001-user-role-management/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── openapi.yaml
├── checklists/
│   └── requirements.md
└── spec.md
```

`tasks.md` در این مرحله ایجاد نمی‌شود و خروجی `$speckit-tasks` خواهد بود.

### Source Code (repository root)

```text
ChangeFlow.slnx
global.json
src/
├── 01.Core/
│   ├── Domain/
│   │   ├── ChangeFlow.Core.Domain.csproj
│   │   └── Users/
│   │       ├── User.cs
│   │       ├── UserId.cs
│   │       ├── LoginIdentifier.cs
│   │       └── UserRole.cs
│   └── Application/
│       ├── ChangeFlow.Core.Application.csproj
│       └── Users/CreateUser/
│           ├── CreateUserCommand.cs
│           ├── CreateUserResult.cs
│           ├── CreateUserUseCase.cs
│           ├── IUserProvisioningStore.cs
│           ├── IPasswordHasher.cs
│           └── ISetupCodeValidator.cs
├── 02.Infra/
│   └── DataAccess/
│       ├── ChangeFlow.Infra.DataAccess.csproj
│       └── Users/
│           ├── Persistence/
│           │   ├── UserConfiguration.cs
│           │   ├── UserCredentialConfiguration.cs
│           │   ├── UserRoleConfiguration.cs
│           │   └── EfUserProvisioningStore.cs
│           └── Security/
│               ├── IdentityPasswordHasher.cs
│               └── ConfigurationSetupCodeValidator.cs
└── 03.Endpoints/
    └── Api/
        ├── ChangeFlow.Endpoints.Api.csproj
        └── Users/CreateUser/
            ├── CreateUserEndpoint.cs
            ├── CreateUserRequest.cs
            └── CreateUserResponse.cs
tests/
└── ChangeFlow.Tests/
    ├── ChangeFlow.Tests.csproj
    └── Users/
        ├── Domain/
        ├── Application/
        ├── Integration/
        └── Contract/
```

**Structure Decision**: ساختار سطح‌بالا دقیقاً مطابق Constitution باقی می‌ماند. اجزای Feature در هر پروژه زیر `Users` قرار می‌گیرند. قراردادهای فنی Application فقط برای use case ایجاد کاربر هستند؛ Generic Repository یا پوشه عمومی `Services` ایجاد نمی‌شود. Adapterهای امنیتی در پروژه Infrastructure موجود و زیر Feature نگهداری می‌شوند تا پروژه پنجمی افزوده نشود.

## Phase 0 — Research Outcome

[research.md](research.md) تصمیم‌های SQLite/EF Core برای Feature نخست، transaction boundary، uniqueness/concurrency، password hashing، Setup Code، HTTP errors، observability و testing را همراه دلیل و گزینه‌های جایگزین ثبت می‌کند. Provider سراسری پروژه همچنان باز است و هیچ ابهام فنی حل‌نشده‌ای برای اجرای این Feature باقی نمانده است.

## Phase 1 — Design Outcome

- [data-model.md](data-model.md) Aggregate، جداول، قیود، normalization، atomicity و concurrency semantics را تعریف می‌کند.
- [contracts/openapi.yaml](contracts/openapi.yaml) قرارداد `POST /users`، security scheme و پاسخ‌های `201/400/401/409/500` را تعریف می‌کند.
- [quickstart.md](quickstart.md) سناریوهای قابل‌اجرای build/test و اعتبارسنجی end-to-end را بدون کد پیاده‌سازی مستند می‌کند.

## Complexity Tracking

هیچ نقض Constitution یا پیچیدگی نیازمند استثنا شناسایی نشد.

## Baseline Alignment Gate

پیش از `$speckit-implement` باید در Analyze تأیید شود که SQLite فقط در Infrastructure این Feature باقی مانده، EF Core abstraction به Core نشت نکرده و هیچ انتخابی از این Plan به‌عنوان Provider سراسری تفسیر نشده است.
