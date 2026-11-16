# قرارداد HTTP گردش‌کار درخواست تغییر

تمام مسیرها زیر `/change-requests` هستند و `Authorization: Bearer <access-token>` می‌خواهند. شناسه‌های مسیری UUID هستند. بدنه‌های ورودی JSON بوده و فیلدهای مالک، نقش، وضعیت و تاریخچه از Client پذیرفته نمی‌شوند. شناسهٔ مالک از حساب احراز‌شده به دست می‌آید.

| روش و مسیر | ورودی | شرط موفقیت | پاسخ |
|---|---|---|---|
| POST /change-requests | title, description, reason | Requester جاری | 201، Request و Location مسیر خواندن |
| GET /change-requests/{id} | — | مالک در هر وضعیت یا Reviewer جاری غیرمالک در PendingReview | 200، Request |
| PUT /change-requests/{id} | title, description, reason, expectedVersion | Requester جاری مالک در Draft یا ChangesRequested | 200، Request |
| POST /change-requests/{id}/submit | expectedVersion | Requester جاری مالک در Draft یا ChangesRequested | 200، Request |
| POST /change-requests/{id}/approve | expectedVersion | Reviewer جاری غیرمالک در PendingReview | 200، Request |
| POST /change-requests/{id}/request-changes | expectedVersion, feedback | Reviewer جاری غیرمالک در PendingReview | 200، Request |
| GET /change-requests/{id}/review-history | — | مالک احراز‌شده یا Reviewer جاری دارای تصمیم موفق روی همان درخواست | 200، History |

**Request** یک شیء با `id` و `requesterId` از نوع UUID، رشته‌های `title`، `description` و `reason`، `status` از enum مدل داده و `version` است. `currentFeedback` فقط در نمایش مالک در ChangesRequested و در پاسخ موفق RequestChanges حضور دارد. در پاسخ RequestChanges، بازخورد همان ورودی trim‌شده است. ReviewDecisionهای پیشین فقط در مسیر History ظاهر می‌شوند.

**History** آرایهٔ تصمیم‌ها به ترتیب صعودی نسخهٔ حاصل است. هر تصمیم `reviewerId` از نوع UUID، `decidedAtUtc` در قالب RFC 3339 UTC، `action` با مقدار Approve یا RequestChanges، و `feedback` از نوع رشته یا null دارد. تاریخچهٔ مجاز بدون تصمیم، آرایهٔ خالی برمی‌گرداند.

`version` و `expectedVersion` رشتهٔ ده‌دهی بدون علامت و صفر پیشرو، در بازهٔ ۱ تا ۹۲۲۳۳۷۲۰۳۶۸۵۴۷۷۵۸۰۷ هستند. هر تغییر معتبر نسخه را یک واحد افزایش می‌دهد. Create نسخهٔ مورد انتظار ندارد. مقادیر متنی محتوا و feedback پس از trim باید غیرخالی باشند. PUT همهٔ فیلدهای قابل‌ویرایش را دریافت می‌کند.

## خطاها

خطا پاسخ `application/problem+json` با `status`، `title`، `code` و شناسهٔ هم‌بستگی است. اعتبارسنجی می‌تواند `errors` فیلدمحور داشته باشد. دادهٔ درخواست جاری در خطا بازگردانده نمی‌شود.

| HTTP | code | وضعیت |
|---|---|---|
| 400 | validation_failed | JSON یا شناسهٔ نامعتبر، فیلد لازم یا نسخهٔ غایب/نامعتبر، یا مقدار متنی خالی |
| 401 | authentication_required | Bearer معتبر یا حساب محلی متناظر وجود ندارد. پاسخ شامل WWW-Authenticate: Bearer است. |
| 403 | forbidden | نقش جاری یا شرط مالکیت/سابقهٔ لازم برقرار نیست |
| 403 | self_review_forbidden | مالک دارای نقش Reviewer روی درخواست خودش فرمان Review اجرا می‌کند |
| 404 | change_request_not_found | شناسهٔ معتبر در datastore یافت نشد |
| 409 | version_conflict | نسخهٔ معتبر ولی قدیمی یا نامنطبق، یا شکست شرط هم‌زمانی هنگام ذخیره |
| 409 | invalid_state | نقش و مالکیت و نسخه معتبرند، ولی وضعیت اجازهٔ عملیات را نمی‌دهد |
| 503 | temporarily_unavailable | رقابت زیرساختی موقت پس از پایان بودجهٔ اجرا، بدون تعارض نسخهٔ احراز‌شده |
| 500 | unexpected_failure | خرابی پیش‌بینی‌نشده همراه شناسهٔ هم‌بستگی |

در فرمان‌ها پس از احراز هویت و اعتبارسنجی شکلی، ابتدا وجود رکورد و شرط نقش/مالکیت بررسی می‌شود، سپس نسخه و بعد وضعیت. در خواندن، وضعیت جزء سیاست مشاهده است و عدم مجوز پاسخ 403 دارد. تکرار فرمان با نسخهٔ قدیمی تعارض نسخه است. تکرار با نسخهٔ فعلی در وضعیت غیرمجاز، invalid_state است. پس از تعارض، Client در صورت داشتن مجوز نسخهٔ جدید را دوباره دریافت می‌کند.
