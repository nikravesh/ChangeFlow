# برنامهٔ پیاده‌سازی گردش‌کار درخواست تغییر

**شاخهٔ Git** `main` · **فیچر** `002-change-request-workflow` · **تاریخ** ۲۰۲۶-۱۱-۱۶ · [Specification](spec.md)

## خلاصه

درخواست از Draft به PendingReview می‌رود. Reviewer مستقل می‌تواند آن را تأیید کند یا با بازخورد برای اصلاح برگرداند. مجوزها در Application ارزیابی و تصمیم Review در همان تراکنش تغییر درخواست ثبت می‌شود. تاریخچه برای مالک یا Reviewer جاری دارای تصمیم موفق روی همان درخواست در دسترس است.

## بستر فنی

| موضوع | مبنا |
|---|---|
| زبان و اجرا | C# و ASP.NET Core روی net11.0، SDK طبق ../../global.json |
| معماری | Layered Monolith موجود با سازمان‌دهی Feature-based |
| Persistence | EF Core مصوب، دیتابیس SQLite فایل‌محور موجود برای محیط محلی، Migration افزایشی در Infrastructure |
| هویت و مجوز | JWT Bearer در گروه مسیر ۰۰۲، حساب محلی متصل به subject صادرکننده، نقش‌های جاری از datastore فیچر ۰۰۱ |
| هم‌زمانی | expectedVersion، concurrency token و تراکنش کوتاه مشترک برای نقش، وضعیت، تغییر درخواست و ReviewDecision |
| آزمون | xUnit و WebApplicationFactory، SQLite فایل‌محور واقعی در آزمون Integration |
| کارایی و دامنه | معیارهای زمانی کاربر SC-001/002/005 ملاک پذیرش‌اند. دریافت مستقیم با شناسه و تاریخچهٔ یک درخواست، بدون صف یا فهرست. |

جزئیات و گزینه‌های بررسی‌شده در [پژوهش](research.md) و ساختار جدول‌ها در [مدل داده](data-model.md) آمده‌اند. نصب و اتصال صادرکنندهٔ مورد اعتماد و آماده‌سازی حساب‌های متناظر پیش‌نیاز اجرای عملیاتی‌اند. روش اجرا و Provider محیط Production نیازمند تصمیم و آزمون جداگانه در سطح پروژه است.

## بررسی Constitution

| اصل | قبل از پژوهش | پس از طراحی |
|---|---|---|
| I — Specification | گردش‌کار و قاعدهٔ تاریخچه در Spec ثبت شده‌اند | مسیرها و سناریوهای Quickstart به Spec ارجاع دارند |
| II تا IV — معماری، پلتفرم و مرزها | ساختار موجود و SDK تثبیت‌شده مبنا هستند | Domain مستقل، مجوز در Application، EF در Infrastructure و HTTP در Endpoint |
| V و VI — مجوز و وضعیت | ماتریس مجوز و state machine مبنا هستند | نقش جاری، منع Self-Review، فرمان‌های Domain و تغییرناپذیری Approved در مدل و قرارداد لحاظ شده‌اند |
| VII — یکپارچگی | تعارض صریح و ثبت اتمیک الزامی‌اند | R3 مرز تراکنش، نسخه و شکست رقابتی را تعریف می‌کند |
| VIII — امنیت و مشاهده‌پذیری | خط مبنای secret و ProblemDetails اعمال می‌شود | تنظیم هویت بیرون از مخزن، خطای عمومی، log ساخت‌یافته با correlation ID و بدون token، محتوای درخواست یا feedback |
| IX — آزمون | هر FR و سناریو باید پوشش خودکار داشته باشد | Quickstart سطح پوشش و بررسی دادهٔ پایدار را مشخص می‌کند |
| X — سادگی | پروژه و لایهٔ موجود استفاده می‌شوند | وابستگی احراز هویت فقط برای مرز HTTP، وب‌سرویس و datastore موجود |

بررسی طراحی بدون استثنای Constitution پایان یافت. در Application، تراکنش از طریق قراردادی محدود به use case هماهنگ می‌شود تا یک snapshot مشترک در Infrastructure فراهم باشد. تصمیم مجوز در Application و قواعد انتقال و نسخه در Domain می‌مانند.

## ساختار

```text
specs/002-change-request-workflow/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/http-api.md

src/01.Core/Domain/ChangeRequests/
src/01.Core/Application/ChangeRequests/
src/02.Infra/DataAccess/ChangeRequests/
src/02.Infra/DataAccess/Migrations/
src/03.Endpoints/Api/ChangeRequests/
tests/ChangeFlow.Tests/ChangeRequests/
```

Integration هم‌سازگاری ارتقای دیتابیس فیچر ۰۰۱ و هم rollback تغییر درخواست و تصمیم را بررسی می‌کند. تست‌های HTTP قرارداد مجوز، نسخه و خطا را بر اساس [قرارداد HTTP](contracts/http-api.md) می‌سنجند.
