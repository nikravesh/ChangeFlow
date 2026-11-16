# کارهای اجرایی گردش‌کار درخواست تغییر

ورودی‌ها شامل [مشخصات](spec.md)، [برنامهٔ فنی](plan.md)، [پژوهش](research.md)، [مدل داده](data-model.md)، [قرارداد HTTP](contracts/http-api.md) و [راهنمای اعتبارسنجی](quickstart.md) هستند. مسیرها نسبت به ریشهٔ مخزن‌اند. [P] یعنی پس از تکمیل پیش‌نیازهای تعیین‌شده، کار را می‌توان هم‌زمان با مورد مشخص‌شده در بخش اجرای موازی انجام داد.

## فاز ۱ — آماده‌سازی

- [X] T001 اتصال پروژهٔ API به JWT Bearer سازگار با SDK تثبیت‌شده و تعریف تنظیمات Authority و Audience بدون credential در `src/03.Endpoints/Api/ChangeFlow.Endpoints.Api.csproj` و `src/03.Endpoints/Api/ChangeRequests/ChangeRequestAuthenticationOptions.cs` طبق R1.
- [X] T002 آماده‌سازی fixture اختصاصی با SQLite فایل‌محور، کلید امضای تولیدشده در زمان اجرا، ساعت قابل‌کنترل و حساب‌های فیچر ۰۰۱ در `tests/ChangeFlow.Tests/ChangeRequests/Infrastructure/ChangeRequestApiFixture.cs`.

## فاز ۲ — زیرساخت مشترک

- [X] T003 [P] افزودن آزمون Bearer برای امضای نامعتبر یا غایب، issuer و audience نادرست، انقضا و subject بدون حساب محلی و آزمون عدم اعطای نقش از claim در `tests/ChangeFlow.Tests/ChangeRequests/Contract/ChangeRequestAuthenticationTests.cs` (FR-001، FR-014/015، FR-019).
- [X] T004 [P] تعریف قرارداد تراکنش use case، خواندن حساب و نقش جاری و نتایج کاربردی در `src/01.Core/Application/ChangeRequests/IChangeRequestTransaction.cs` و `src/01.Core/Application/ChangeRequests/ChangeRequestResult.cs`. تصمیم مجوز و ترتیب نقش/مالکیت، نسخه و وضعیت در Application بماند. پس از پایان بودجهٔ retry، اگر خواندن تازه ممکن است، ابتدا مجوز کاربر را دوباره بررسی کنید. برای کاربر مجاز، اختلاف نسخهٔ احرازشده را به نتیجهٔ کاربردی تعارض نسخه تبدیل کنید. اگر تعارض احراز نشود و رقابت زیرساختی ادامه داشته باشد، نتیجهٔ موقت عدم دسترسی را برگردانید.
- [X] T005 در `src/02.Infra/DataAccess/ChangeRequests/EfChangeRequestTransaction.cs` تراکنش non-deferred را پیش از خواندن نقش و درخواست آغاز کنید. حساب، نقش و درخواست را از همان DbContext بخوانید و snapshot خواندن، rollback و retry محدود را طبق R3 پیاده‌سازی کنید. هر retry مجاز را با DbContext و تراکنش تازه و همان نسخهٔ مورد انتظار آغاز کنید. پس از پایان بودجه، نتیجهٔ فنی رقابت را به Application برگردانید. نتیجهٔ نامعلوم commit را خودکار تکرار نکنید.
- [X] T006 ثبت سیاست Bearer فقط برای گروه مسیرهای ۰۰۲، اعتبارسنجی توکن و استخراج UserId از sub معتبر، نگاشت ProblemDetails و correlation، بدون ثبت token یا payload در `src/03.Endpoints/Api/ChangeRequests/ChangeRequestRouteGroup.cs` و `src/03.Endpoints/Api/Program.cs`. قرارداد Setup فیچر ۰۰۱ را حفظ کنید. نتیجهٔ کاربردی تعارض نسخه را به `409 version_conflict` و نتیجهٔ موقت عدم دسترسی را به `503 temporarily_unavailable` طبق قرارداد HTTP نگاشت کنید.

## فاز ۳ — US1، ثبت و ارسال درخواست (P1)

**آزمون مستقل** — Requester یک Draft ایجاد و ویرایش می‌کند و با نسخهٔ جاری آن را Submit می‌کند. تغییر کاربر غیرمالک یا فاقد نقش رد می‌شود و وضعیت ذخیره‌شده ثابت می‌ماند.

- [X] T007 [P] [US1] آزمون قرارداد POST ایجاد، GET مالک، PUT و POST Submit با پاسخ، Location، فیلدهای جاری، trim، UUID نامعتبر، expectedVersion غایب یا خارج از بازه و خطاهای 401/403/404/409 در `tests/ChangeFlow.Tests/ChangeRequests/Contract/DraftWorkflowContractTests.cs` (FR-001 تا FR-006، FR-014 تا FR-020).
- [X] T008 [P] [US1] آزمون واحد ایجاد، Edit و Submit، whitespace، ورودی دستکاری‌شدهٔ مالک، نبود نقش و نسخهٔ کهنه بدون ذخیره در `tests/ChangeFlow.Tests/ChangeRequests/Domain/DraftWorkflowTests.cs` و `tests/ChangeFlow.Tests/ChangeRequests/Application/DraftAuthorizationTests.cs` (US1.1–4).
- [X] T009 [US1] ایجاد Aggregate و عملیات Create، Edit و Submit در `src/01.Core/Domain/ChangeRequests/ChangeRequest.cs` و `src/01.Core/Domain/ChangeRequests/ChangeRequestStatus.cs`. قیود مدل شامل «GUID غیرتهی سمت سرور، کلید اصلی»، «UserId موجود از فیچر ۰۰۱، کلید خارجی اجباری با حذف محدود، ثابت پس از ایجاد»، «رشتهٔ اجباری، trim‌شده و دارای حداقل یک کاراکتر»، «یکی از Draft، PendingReview، ChangesRequested، Approved با قید مقادیر معتبر» و «عدد صحیح مثبت ۶۴ بیتی، شروع از ۱ و افزایش دقیقاً یک واحد در هر تغییر موفق. در EF به‌عنوان concurrency token نگاشت می‌شود. افزایش نباید wrap شود.» هستند.
- [X] T010 [US1] نگاشت ChangeRequest، FK مالک، قید وضعیت و نسخه و concurrency token، ثبت mapping در DbContext و Migration افزایشی Draft در `src/02.Infra/DataAccess/ChangeRequests/ChangeRequestConfiguration.cs`، `src/02.Infra/DataAccess/Common/ChangeFlowDbContext.cs`، `src/02.Infra/DataAccess/Migrations/202611160001_AddChangeRequests.cs` و `src/02.Infra/DataAccess/Migrations/ChangeFlowDbContextModelSnapshot.cs`.
- [X] T011 [US1] پیاده‌سازی Create، خواندن مستقیم مالک، Edit و Submit در `src/01.Core/Application/ChangeRequests/DraftWorkflow.cs` و ثبت عملیات بارگیری و ذخیره در `src/02.Infra/DataAccess/ChangeRequests/EfChangeRequestTransaction.cs`. مالک را از هویت جاری تعیین و ممانعت از تغییر مالک، stale write و تغییر پس از رد را اعمال کنید.
- [X] T012 [US1] افزودن مسیرهای Create، GET، PUT و Submit، نگاشت DTO و نسخهٔ رشته‌ای و ثبت وابستگی‌ها در `src/03.Endpoints/Api/ChangeRequests/DraftEndpoints.cs`، `src/03.Endpoints/Api/ChangeRequests/ChangeRequestRouteGroup.cs` و `src/02.Infra/DataAccess/DependencyInjection.cs`.
- [X] T013 [US1] آزمون دو Edit/Submit رقیب با نسخهٔ اولیهٔ یکسان و دو scope روی SQLite واقعی در `tests/ChangeFlow.Tests/ChangeRequests/Integration/DraftConcurrencyTests.cs`. یک موفقیت، 409 برای بازنده و افزایش دقیقاً یک‌بارهٔ نسخه را از datastore اثبات کنید (FR-016/017، SC-004).

## فاز ۴ — US2، بررسی مستقل و تاریخچه (P2)

**آزمون مستقل** — با fixture درخواست PendingReview و حساب‌های مستقل، Reviewer می‌تواند تصمیم ثبت کند. مالک دارای هر دو نقش نمی‌تواند Review کند. از دست دادن نقش، دسترسی Reviewer قبلی به تاریخچه را قطع می‌کند.

- [X] T014 [P] [US2] آزمون قرارداد GET برای Review، Approve، RequestChanges و review-history، شامل بازخورد نامعتبر، Self-Review هر دو فرمان، وضعیت نامعتبر، ترتیب تاریخچه، آرایهٔ خالی مجاز، Reviewer بدون تصمیم یا دارای تصمیم روی درخواست دیگر و لغو نقش با توکن قبلی در `tests/ChangeFlow.Tests/ChangeRequests/Contract/ReviewWorkflowContractTests.cs` (US2.1–5، FR-007 تا FR-010، FR-018/019، FR-021/022). برای هر دو فرمان Review روی درخواست خارج از `PendingReview`، با Reviewer غیرمالک و ورودی معتبر، `409 invalid_state` را با نسخهٔ جاری و `409 version_conflict` را با نسخهٔ قدیمی آزمون کنید. پس از هر دو پاسخ، ثابت‌ماندن درخواست، نسخه و تاریخچه را در datastore بررسی کنید.
- [X] T015 [P] [US2] آزمون واحد transitionهای Review، بازخورد، زمان و اقدام، Snapshot نقش، منع Self-Review و سیاست سابقهٔ تاریخی در `tests/ChangeFlow.Tests/ChangeRequests/Domain/ReviewDecisionTests.cs` و `tests/ChangeFlow.Tests/ChangeRequests/Application/ReviewAuthorizationTests.cs`.
- [X] T016 [US2] پیاده‌سازی Approve و RequestChanges و تصمیم append-only در `src/01.Core/Domain/ChangeRequests/ChangeRequest.cs` و `src/01.Core/Domain/ChangeRequests/ReviewDecision.cs`. قیود مدل شامل «کلید خارجی اجباری با حذف محدود» برای ChangeRequestId، «نسخهٔ مثبت درخواست پس از تصمیم، جزء کلید مرکب با ChangeRequestId»، «UserId موجود، کلید خارجی اجباری با حذف محدود» برای ReviewerId، «زمان UTC از ساعت سمت سرور»، «Approve یا RequestChanges» و «برای RequestChanges رشتهٔ trim‌شده و غیرخالی. برای Approve خالی به‌شکل null.» هستند.
- [X] T017 [US2] نگاشت تصمیم، کلید مرکب و شاخص (ChangeRequestId, ReviewerId)، قیود FK و NULL/action، خواندن مرتب‌شده و Migration افزایشی را در `src/02.Infra/DataAccess/ChangeRequests/ReviewDecisionConfiguration.cs`، `src/02.Infra/DataAccess/Common/ChangeFlowDbContext.cs`، `src/02.Infra/DataAccess/Migrations/202611160002_AddReviewDecisions.cs` و `src/02.Infra/DataAccess/Migrations/ChangeFlowDbContextModelSnapshot.cs` اضافه کنید.
- [X] T018 [US2] اجرای Review با نقش جاری و غیرمالک، خواندن PendingReview برای Reviewer و خواندن تاریخچه از snapshot واحد برای مالک یا Reviewer جاری دارای تصمیم موفق روی همان درخواست در `src/01.Core/Application/ChangeRequests/ReviewWorkflow.cs` و `src/02.Infra/DataAccess/ChangeRequests/EfChangeRequestTransaction.cs`. تصمیم و تغییر Aggregate را در همان commit ثبت کنید.
- [X] T019 [US2] افزودن مسیرهای فرمان و تاریخچه و گسترش GET برای Review، نگاشت feedback و کدهای خطا مطابق قرارداد در `src/03.Endpoints/Api/ChangeRequests/ReviewEndpoints.cs`، `src/03.Endpoints/Api/ChangeRequests/DraftEndpoints.cs` و `src/03.Endpoints/Api/ChangeRequests/ChangeRequestRouteGroup.cs`.
- [X] T020 [US2] آزمون رقابت دو Reviewer روی یک نسخه، rollback هنگام شکست درج تصمیم، هر دو ترتیب commit لغو نقش و Review و بررسی عدم تغییر ذخیره‌شده پس از رد در `tests/ChangeFlow.Tests/ChangeRequests/Integration/ReviewAtomicityTests.cs` (FR-014 تا FR-017، FR-021، SC-003/004/007).

## فاز ۵ — US3، اصلاح و ارسال مجدد (P3)

**آزمون مستقل** — با یک ChangesRequested دارای تصمیم قبلی، مالک بازخورد را مشاهده می‌کند، محتوا را ویرایش و دوباره Submit می‌کند. Reviewer مستقل نسخهٔ جدید را تأیید می‌کند و تمام عملیات تغییر بعدی رد می‌شوند.

- [X] T021 [P] [US3] آزمون قرارداد GET بازخورد مالک، PUT حفظ وضعیت ChangesRequested، Submit مجدد و پایان Approved با رد همهٔ فرمان‌ها و حفظ دو تصمیم دورهای متوالی در `tests/ChangeFlow.Tests/ChangeRequests/Contract/ResubmissionContractTests.cs` (US3.1–3، FR-011 تا FR-013، FR-018/021).
- [X] T022 [P] [US3] آزمون واحد امکان Edit/Submit مجدد مالک و منع همان عملیات برای غیرمالک یا مالک فاقد نقش جاری و تمام تغییرهای Approved در `tests/ChangeFlow.Tests/ChangeRequests/Domain/ResubmissionTests.cs` و `tests/ChangeFlow.Tests/ChangeRequests/Application/ResubmissionAuthorizationTests.cs`.
- [X] T023 [US3] تکمیل قواعد Edit و Submit روی ChangesRequested، حفظ مالک و تاریخچه و رد تمام تغییرهای Approved در `src/01.Core/Domain/ChangeRequests/ChangeRequest.cs` و `src/01.Core/Application/ChangeRequests/DraftWorkflow.cs`. بازخورد جاریِ آخرین تصمیم مرتبط را همراه snapshot درخواست مالک تا Submit مجدد از `src/02.Infra/DataAccess/ChangeRequests/EfChangeRequestTransaction.cs` برگردانید و در `src/03.Endpoints/Api/ChangeRequests/DraftEndpoints.cs` نگاشت کنید.
- [X] T024 [US3] نگهداشت تصمیم‌ها و نسخه پس از اجرای چرخهٔ کامل و راه‌اندازی مجدد و ثابت‌ماندن رکورد Approved پس از رد عملیات را در `tests/ChangeFlow.Tests/ChangeRequests/Integration/ResubmissionPersistenceTests.cs` بررسی کنید (SC-006/007).

## فاز ۶ — اعتبارسنجی سراسری

- [X] T025 [P] آزمون Migration از دیتابیس خالی و از نسخهٔ دارای کاربران و نقش‌های ۰۰۱، اجرای مجدد و حفظ داده‌ها در `tests/ChangeFlow.Tests/ChangeRequests/Integration/ChangeRequestMigrationTests.cs`.
- [X] T026 [P] آزمون حفظ قرارداد POST /users و نبود مسیرها و قابلیت‌های خارج از Scope، و کنترل correlation و عدم ثبت token یا متن درخواست و feedback در `tests/ChangeFlow.Tests/ChangeRequests/Contract/WorkflowBoundaryTests.cs` (FR-019/020).
- [ ] T027 اجرای restore، build، test و سناریوهای Quickstart، ثبت فرمان و نتیجهٔ واقعی، پوشش FR-001 تا FR-022 و همهٔ Acceptance Scenarioها، و شواهد معیارهای SC-001 تا SC-007 از جمله سنجش کاربری طبق Spec در `specs/002-change-request-workflow/validation/requirements-traceability.md` و `specs/002-change-request-workflow/validation/build-test-results.md`. موارد اجرا‌نشده را با علت ثبت کنید.

## وابستگی‌ها و ترتیب اجرا

```text
آماده‌سازی → زیرساخت مشترک → US1 → US2 → US3 → اعتبارسنجی سراسری
```

- T003 و T004 پس از آماده‌سازی مستقل‌اند. T005 پس از T004 و T006 پس از آماده‌شدن آزمون‌های T003 و قراردادهای مشترک انجام می‌شود. آزمون‌های نیازمند endpoint در پایان US1 قابل اجرای کامل‌اند.
- در US1، T009 پیش از T010 و T011 است. T012 پس از آماده‌شدن use caseها و T013 پس از آماده‌شدن مسیرها اجرا می‌شود.
- در US2، T016 پیش از T017 و T018 است. T019 به T018 نیاز دارد و T020 پس از آماده‌شدن مسیرهای Review اجرا می‌شود.
- در US3، T023 پس از تعریف آزمون‌های T021 و T022 و T024 پس از تکمیل چرخه اجرا می‌شود.
- T027 به تکمیل T025 و T026 و تمام فازهای داستان وابسته است.

## اجرای موازی

- US1 پس از زیرساخت، تعریف آزمون‌های HTTP در T007 هم‌زمان با آزمون‌های واحد T008.
- US2 پس از US1، تعریف آزمون‌های HTTP در T014 هم‌زمان با آزمون‌های واحد T015.
- US3 پس از US2، تعریف آزمون‌های HTTP در T021 هم‌زمان با آزمون‌های واحد T022.
- پس از US3، آزمون Migration در T025 هم‌زمان با آزمون مرز قرارداد در T026.

## راهبرد تحویل

US1 کوچک‌ترین برش قابل آزمون است و پس از آن به‌ترتیب بررسی مستقل با تاریخچه و چرخهٔ اصلاح تحویل داده می‌شوند. پیش از آغاز پیاده‌سازی، مرحلهٔ Analyze و بازبینی Taskها انجام می‌شود. نمونهٔ عملیاتی Bearer به صادرکنندهٔ معتبر و امکان اتصال امن subject به UserId نیاز دارد. آزمون خودکار از صادرکنندهٔ آزمایشی استفاده می‌کند.



## Phase 7: Convergence

- [X] T028 آزمون انتها‌به‌انتهای JWT Bearer با صادرکنندهٔ آزمایشی مستقل در `tests/ChangeFlow.Tests/ChangeRequests/Contract/ChangeRequestAuthenticationTests.cs` اضافه کنید. Authority و Audience را از پیکربندی بگیرید و دریافت metadata و کلید امضا و پذیرش توکن معتبر در یک مسیر واقعی ۰۰۲ را بدون تزریق مستقیم کلید به JWT options بررسی کنید. نتیجه را در `specs/002-change-request-workflow/validation/build-test-results.md` ثبت کنید. per plan: R1 و T027 (partial)
- [X] T029 در `tests/ChangeFlow.Tests/ChangeRequests/Contract/DraftWorkflowContractTests.cs` دو درخواست با محتوای یکسان و مالک یکسان ثبت کنید و پاسخ 201، دو شناسهٔ متمایز، نسخهٔ اولیهٔ مستقل و وجود هر دو رکورد در SQLite را بررسی کنید. شواهد این آزمون و اجرای ۲۰ Requester و ۲۰ Reviewer را در `specs/002-change-request-workflow/validation/requirements-traceability.md` و `specs/002-change-request-workflow/validation/build-test-results.md` ثبت کنید. per spec: Edge Cases و FR-020 (partial)
