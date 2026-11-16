# مدل دادهٔ فیچر ۰۰۲

## ChangeRequest

| فیلد | نوع و قید |
|---|---|
| Id | GUID غیرتهی سمت سرور، کلید اصلی |
| RequesterId | UserId موجود از فیچر ۰۰۱، کلید خارجی اجباری با حذف محدود، ثابت پس از ایجاد |
| Title، Description، Reason | رشتهٔ اجباری، trim‌شده و دارای حداقل یک کاراکتر |
| Status | یکی از Draft، PendingReview، ChangesRequested، Approved با قید مقادیر معتبر |
| Version | عدد صحیح مثبت ۶۴ بیتی، شروع از ۱ و افزایش دقیقاً یک واحد در هر تغییر موفق. در EF به‌عنوان concurrency token نگاشت می‌شود. افزایش نباید wrap شود. |

ارتباط User با ChangeRequest یک‌به‌چند است. بازخورد جاری از آخرین ReviewDecision از نوع RequestChanges در وضعیت ChangesRequested به دست می‌آید. پس از Submit مجدد از نمایش جاری حذف می‌شود و در تاریخچه باقی می‌ماند. خواندن فیلدهای درخواست و بازخورد متناظر از یک snapshot انجام می‌شود.

## ReviewDecision

| فیلد | نوع و قید |
|---|---|
| ChangeRequestId | کلید خارجی اجباری با حذف محدود |
| ResultingVersion | نسخهٔ مثبت درخواست پس از تصمیم، جزء کلید مرکب با ChangeRequestId |
| ReviewerId | UserId موجود، کلید خارجی اجباری با حذف محدود |
| DecidedAtUtc | زمان UTC از ساعت سمت سرور |
| Action | Approve یا RequestChanges |
| Feedback | برای RequestChanges رشتهٔ trim‌شده و غیرخالی. برای Approve خالی به‌شکل null. |

هر درخواست صفر تا چند تصمیم دارد. تاریخچه به‌ترتیب صعودی ResultingVersion است. کلید مرکب ثبت دو تصمیم در یک نسخه را رد می‌کند. جست‌وجوی تصمیم متعلق به Reviewer با شاخص `(ChangeRequestId, ReviewerId)` انجام می‌شود. مسیر Persistence فقط افزودن و خواندن تصمیم را در use caseهای این فیچر ارائه می‌دهد. بازخورد و تطابق Action با Feedback در Domain بررسی می‌شوند و قید سازگاری NULL/action در datastore اعمال می‌شود.

## رفتار و مرز تراکنش

| عملیات | مبدأ | مقصد | اثر |
|---|---|---|---|
| Create | — | Draft | شناسه و نسخهٔ اولیه |
| Edit | Draft یا ChangesRequested | همان وضعیت | جایگزینی محتوای قابل ویرایش |
| Submit | Draft یا ChangesRequested | PendingReview | افزایش نسخه |
| RequestChanges | PendingReview | ChangesRequested | ثبت یک تصمیم با بازخورد |
| Approve | PendingReview | Approved | ثبت یک تصمیم و پایان گردش کار |

Domain وضعیت را با عملیات صریح تغییر می‌دهد. Application مالکیت، هویت، نقش جاری و صلاحیت مشاهدهٔ تاریخچه را طبق [Spec](spec.md) اعمال می‌کند. برای غیرمالک، تاریخچه به نقش جاری Reviewer و تصمیم موفق روی همان درخواست نیاز دارد. درخواست Approved هیچ مسیر تغییر ندارد. تراکنش و reverse mapping خطا در Infrastructure انجام می‌شود. جزئیات HTTP در [قرارداد](contracts/http-api.md) آمده است.
