# Data Model: تعریف کاربر و نقش اولیه

**Feature**: `001-user-role-management`  
**Date**: 2026-11-14  
**References**: [spec.md](spec.md), [research.md](research.md)

## Aggregate Boundary

`User` مرز Aggregate این Feature است. User باید هنگام creation همراه Credential و حداقل یک Role و حداکثر دو Role معتبر در یک transaction ثبت شود. Credential به‌دلیل حساسیت و وابستگی به hashing در مدل persistence جداست، اما lifecycle ایجاد آن از User مستقل نیست.

```text
User (aggregate root)
├── LoginIdentifier (value object: original + normalized)
└── 1..2 UserRoleAssignment

Provisioning transaction
├── User
└── UserCredential
```

Setup Code عضو Aggregate و رکورد دیتابیس نیست؛ digest فعال آن از منبع پیکربندی امن دریافت می‌شود.

## Entity: User

| Field | Logical type | Required | Rules |
|-------|--------------|----------|-------|
| `Id` | UUID | Yes | شناسه پایدار، تولیدشده توسط سامانه و پس از creation تغییرناپذیر |
| `DisplayName` | Text | Yes | trim می‌شود و نتیجه نباید خالی باشد |
| `LoginIdentifier` | Text | Yes | مقدار trim‌شده برای نمایش و پاسخ؛ نباید خالی باشد |
| `NormalizedLoginIdentifier` | Text | Yes | مقدار canonical فرهنگ‌ناوابسته برای مقایسه؛ unique در کل سامانه |

### Domain invariants

- User بدون DisplayName، LoginIdentifier یا حداقل یک Role معتبر ساخته نمی‌شود.
- Application باید ایجاد User، UserCredential و Role assignmentها را در یک transaction واحد هماهنگ کند.
- مجموعه Roleها فقط شامل `Requester` و/یا `Reviewer` است.
- مجموعه Roleها حداقل یک عضو دارد و duplicate در آن وجود ندارد.
- `Id` و LoginIdentifier پس از creation در Scope فعلی قابل‌تغییر نیستند؛ عملیات update تعریف نشده است.

## Value Object: LoginIdentifier

| Component | Purpose |
|-----------|---------|
| `Original` | مقدار trim‌شده‌ای که در پاسخ نمایش داده می‌شود |
| `Normalized` | مقدار حاصل از trim و `ToUpperInvariant()` برای uniqueness |

Normalization باید deterministic باشد و در Domain/Application تعریف شود؛ رفتار آن نباید به collation دیتابیس وابسته باشد. `User1` و `user1` مقدار normalized یکسان تولید می‌کنند.

## Entity: UserCredential

| Field | Logical type | Required | Rules |
|-------|--------------|----------|-------|
| `UserId` | UUID | Yes | کلید اصلی و کلید خارجی به User؛ رابطه دقیقاً یک‌به‌یک |
| `PasswordHash` | Opaque text | Yes | خروجی نسخه‌دار `PasswordHasher`; plaintext و مقدار قابل‌بازیابی نیست |

### Credential rules

- plaintext password قبل از hashing باید حداقل ۸ نویسه و با classification فرهنگ‌ناوابسته شامل حداقل یک حرف بزرگ، حرف کوچک، عدد و نماد باشد؛ نماد هر نویسه‌ای است که letter یا digit نباشد.
- فقط hash persist می‌شود؛ plaintext در response، log، exception detail، fixture یا snapshot قرار نمی‌گیرد.
- حذف یا failure در ذخیره Credential کل transaction ایجاد User را rollback می‌کند.
- عملیات login، verify، تغییر یا recovery رمز عبور خارج از Scope است.

## Entity: UserRoleAssignment

| Field | Logical type | Required | Rules |
|-------|--------------|----------|-------|
| `UserId` | UUID | Yes | کلید خارجی به User |
| `Role` | Enumeration | Yes | فقط `Requester` یا `Reviewer` |

کلید مرکب `(UserId, Role)` از انتساب duplicate جلوگیری می‌کند. حذف duplicate ورودی پیش از persistence مجاز است، اما constraint دیتابیس نیز invariant را حفظ می‌کند.

## Configuration Secret: Setup Code

| Field | Location | Rules |
|-------|----------|-------|
| Active setup-code digest | Secure external configuration | الزامی؛ digest ثابت‌طول `SHA-256`؛ خارج از Repository و فایل‌های commit‌شده |
| Candidate code | Request `Authorization` header | فقط در حافظه use case؛ هرگز log یا persist نمی‌شود |

### Lifecycle

```text
Configured/Active ──manual replacement applied──> Replaced/Invalid
        │
        └──successful use──> Configured/Active
```

- استفاده موفق کد را مصرف یا منقضی نمی‌کند.
- rotation با جایگزینی digest و اعمال reload/redeployment تکمیل می‌شود.
- از لحظه اعمال configuration جدید، کد قبلی نامعتبر است.

## Relational Schema

### `users`

| Column | SQLite type | Constraints |
|--------|-----------------|-------------|
| `id` | `TEXT` | PRIMARY KEY, NOT NULL؛ نگاشت Guid توسط EF Core |
| `display_name` | `text` | NOT NULL |
| `login_identifier` | `text` | NOT NULL |
| `normalized_login_identifier` | `text` | NOT NULL |

Constraint/index:

- `ux_users_normalized_login_identifier`: UNIQUE (`normalized_login_identifier`)

### `user_credentials`

| Column | SQLite type | Constraints |
|--------|-----------------|-------------|
| `user_id` | `TEXT` | PRIMARY KEY, FOREIGN KEY → `users.id`, NOT NULL؛ نگاشت Guid توسط EF Core |
| `password_hash` | `text` | NOT NULL |

### `user_roles`

| Column | SQLite type | Constraints |
|--------|-----------------|-------------|
| `user_id` | `TEXT` | FOREIGN KEY → `users.id`, NOT NULL؛ نگاشت Guid توسط EF Core |
| `role` | `text` | NOT NULL, CHECK value in (`Requester`, `Reviewer`) |

Primary key: (`user_id`, `role`).

## Creation Flow and Validation Order

1. Authorization header از مدل request جدا و بدون logging دریافت می‌شود.
2. Application با adapter زمان‌ثابت، Setup Code را اعتبارسنجی می‌کند؛ missing و invalid نتیجه یکسان دارند.
3. DisplayName، LoginIdentifier، password و roles اعتبارسنجی می‌شوند؛ تمام نقض‌های password policy بدون بازنمایی password اعلام می‌شوند.
4. LoginIdentifier canonical می‌شود و Roleهای تکراری به مجموعه یکتا تبدیل می‌شوند.
5. Password به hash opaque تبدیل می‌شود.
6. User، UserCredential و UserRoleAssignmentها به یک unit of work افزوده می‌شوند.
7. یک commit/transaction انجام می‌شود؛ هر failure همه تغییرات را rollback می‌کند.

## Concurrency and Conflict Semantics

- unique constraint داور نهایی رقابت روی LoginIdentifier است؛ pre-check احتمالی جایگزین آن نیست.
- اگر دو transaction مقدار normalized یکسان insert کنند، دقیقاً یکی commit می‌شود.
- transactionها کوتاه نگه داشته می‌شوند و connection SQLite از busy timeout محدود استفاده می‌کند تا رقابت writer به خطای گذرای فوری تبدیل نشود.
- Infrastructure فقط violation مربوط به `ux_users_normalized_login_identifier` را به `LoginIdentifierConflict` ترجمه می‌کند.
- Application نتیجه typed را برمی‌گرداند و Endpoint آن را به `409 Conflict` نگاشت می‌کند.
- سایر خطاهای persistence تعارض کسب‌وکار تلقی نمی‌شوند و به مسیر خطای غیرمنتظره می‌روند.
- این Feature update/delete ندارد؛ بنابراین concurrency token برای User در Schema فعلی اضافه نمی‌شود.

## Result Projection

پاسخ موفق فقط شامل `Id`، `DisplayName`، `LoginIdentifier` و Roleها است. `NormalizedLoginIdentifier`، `PasswordHash`، plaintext password و Setup Code هیچ projection عمومی ندارند.
