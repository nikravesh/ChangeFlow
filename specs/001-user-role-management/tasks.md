---

description: "فهرست کارهای اجرایی قابلیت تعریف کاربر و نقش اولیه"
---

# Tasks: تعریف کاربر و نقش اولیه

**Input**: اسناد طراحی در `specs/001-user-role-management/`

**Prerequisites**: `plan.md`، `spec.md`، `research.md`، `data-model.md`، `contracts/openapi.yaml` و `docs/architecture/technical-baseline.md`

**Tests**: آزمون‌ها الزامی‌اند؛ Constitution و Specification برای تمام Functional Requirementها و Acceptance Scenarioها پوشش خودکار می‌خواهند. در هر User Story ابتدا آزمون‌ها نوشته و شکست آن‌ها مشاهده می‌شود.

**Organization**: Taskها براساس User Story گروه‌بندی شده‌اند تا هر Story به‌صورت مستقل قابل‌پیاده‌سازی و اعتبارسنجی باشد.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: قابل‌اجرا به‌صورت موازی در فایل‌های متفاوت و بدون وابستگی به Task ناتمام
- **[Story]**: نگاشت به User Story متناظر (`US1` یا `US2`)
- هر Task مسیر دقیق فایل مقصد را مشخص می‌کند.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: ایجاد ساختار ثابت Solution و وابستگی‌های مصوب بدون ورود به رفتار User Story

- [X] T001 ایجاد `global.json` با SDK پایدار .NET 11 و ایجاد `ChangeFlow.slnx` در ریشه Repository مطابق ساختار Constitution
- [X] T002 [P] ایجاد پروژه‌های `src/01.Core/Domain/ChangeFlow.Core.Domain.csproj`، `src/01.Core/Application/ChangeFlow.Core.Application.csproj`، `src/02.Infra/DataAccess/ChangeFlow.Infra.DataAccess.csproj` و `src/03.Endpoints/Api/ChangeFlow.Endpoints.Api.csproj` با Target Framework برابر `net11.0`
- [X] T003 [P] ایجاد پروژه xUnit در `tests/ChangeFlow.Tests/ChangeFlow.Tests.csproj` با Target Framework برابر `net11.0`
- [X] T004 ثبت پروژه‌ها در `ChangeFlow.slnx` و اعمال جهت وابستگی Domain ← Application ← Infrastructure و Composition در Api از طریق ProjectReferenceهای فایل‌های `.csproj`
- [X] T005 افزودن Packageهای سازگار با .NET 11 شامل EF Core/SQLite/EF Design/Identity Core به `src/02.Infra/DataAccess/ChangeFlow.Infra.DataAccess.csproj`، ASP.NET Core composition به `src/03.Endpoints/Api/ChangeFlow.Endpoints.Api.csproj` و xUnit/`Microsoft.AspNetCore.Mvc.Testing` به `tests/ChangeFlow.Tests/ChangeFlow.Tests.csproj`

**Checkpoint**: Solution خالی با چهار پروژه مصوب و یک پروژه آزمون restore و build می‌شود.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: زیرساخت مشترکی که هر دو User Story به آن وابسته‌اند

**پیش‌نیاز مسدودکننده**: پیش از تکمیل این فاز هیچ User Story آغاز نشود.

- [X] T006 ایجاد `ChangeFlowDbContext` و registration پایه EF Core SQLite، شامل factory یا abstraction زیرساختی معادل برای ساخت `DbContext` مستقل در هر تلاش Retry و بررسی نهایی، بدون نشت Provider یا EF Core به Core، در `src/02.Infra/DataAccess/Common/ChangeFlowDbContext.cs` و `src/02.Infra/DataAccess/DependencyInjection.cs`
- [X] T007 [P] ایجاد قرارداد سراسری `ProblemDetails` با `code` پایدار و trace identifier در `src/03.Endpoints/Api/Common/Errors/ProblemDetailsFactory.cs`
- [X] T008 [P] افزودن زیرساخت عمومی شناسه هم‌بستگی و logging ساخت‌یافته با منع ثبت Authorization و request body حساس و redaction فیلدهای password و password hash در `src/03.Endpoints/Api/Common/Observability/CorrelationMiddleware.cs`
- [X] T009 [P] ایجاد fixture آزمون SQLite file-backed با فایل موقت مستقل، foreign keys فعال و `Default Timeout=5` در `tests/ChangeFlow.Tests/Infrastructure/SqliteDatabaseFixture.cs`
- [X] T010 ایجاد `WebApplicationFactory` قابل‌تعویض با SQLite موقت و secretهای آزمایشی بدون ثبت مقدار آن‌ها در `tests/ChangeFlow.Tests/Infrastructure/ChangeFlowApiFactory.cs`

**Checkpoint**: Foundation آماده است؛ SQLite فقط در Infrastructure دیده می‌شود و harness آزمون HTTP و datastore قابل‌استفاده است.

---

## Phase 3: User Story 1 — ایجاد کاربر همراه با نقش (Priority: P1) 🎯 MVP

**Goal**: Anonymous با Setup Code معتبر، User دارای Credential و نقش `Requester`، `Reviewer` یا هر دو را به‌صورت اتمیک ایجاد کند.

**Independent Test**: با Setup Code معتبر، `displayName` غیرخالی، `loginIdentifier` یکتا، password معتبر و یک یا دو Role، درخواست `POST /users` باید `201` بدهد؛ User، hash و Roleها یکجا persist شوند و response هیچ secret یا hash نداشته باشد. همان Setup Code تا پیش از rotation برای ایجاد User دوم نیز معتبر بماند.

### Tests for User Story 1 — ابتدا نوشته و شکست آن‌ها تأیید شود

- [X] T011 [P] [US1] نوشتن آزمون‌های Domain برای ساخت User، trim نام، canonicalization با `ToUpperInvariant()`، پذیرش فقط `Requester`/`Reviewer` و پذیرش چند Role تکراری با یکتاسازی آن‌ها به یک assignment در `tests/ChangeFlow.Tests/Users/Domain/UserCreationTests.cs`
- [X] T012 [P] [US1] نوشتن آزمون‌های Application مسیر موفق برای هر سه ترکیب Role، بازگشت شناسه پایدار و عدم حضور password/Setup Code در نتیجه در `tests/ChangeFlow.Tests/Users/Application/CreateUserSuccessTests.cs`
- [X] T013 [P] [US1] نوشتن آزمون‌های adapter امنیتی برای hash نسخه‌دار و salt‌شده، عدم برابری hash با plaintext، مقایسه زمان‌ثابت digest `SHA-256`، reuse کد تا rotation و اثبات پذیرش کد جدید و ابطال فوری کد قبلی پس از rotation در `tests/ChangeFlow.Tests/Users/Infrastructure/UserSecurityAdapterTests.cs`
- [X] T014 [P] [US1] نوشتن آزمون Integration SQLite برای ثبت اتمیک `users`، `user_credentials` و `user_roles` و ذخیره hash به‌جای plaintext در `tests/ChangeFlow.Tests/Users/Integration/CreateUserPersistenceTests.cs`
- [X] T015 [P] [US1] نوشتن آزمون قرارداد HTTP پاسخ `201` مطابق `contracts/openapi.yaml` و اثبات نبود password، hash، normalized identifier و Setup Code در `tests/ChangeFlow.Tests/Users/Contract/CreateUserSuccessContractTests.cs`

### Implementation for User Story 1

- [X] T016 [P] [US1] پیاده‌سازی `UserId` و enum نقش با مقادیر دقیق `Requester` و `Reviewer` در `src/01.Core/Domain/Users/UserId.cs` و `src/01.Core/Domain/Users/UserRole.cs`
- [X] T017 [P] [US1] پیاده‌سازی Value Object شناسه ورود با قواعد دقیق «مقدار trim‌شده غیرخالی برای نمایش» و «مقدار normalized حاصل از trim و `ToUpperInvariant()`» در `src/01.Core/Domain/Users/LoginIdentifier.cs`
- [X] T018 [US1] پیاده‌سازی Aggregate کاربر با قیود «Id پایدار»، «DisplayName پس از trim غیرخالی» و «مجموعه یکتای ۱ تا ۲ Role معتبر» در `src/01.Core/Domain/Users/User.cs`؛ Credential نباید عضو Domain Aggregate باشد
- [X] T019 [P] [US1] ایجاد `CreateUserCommand.cs`، `CreateUserSuccess.cs`، `IUserProvisioningStore.cs`، `IPasswordCredentialHasher.cs` و `ISetupCodeValidator.cs` در `src/01.Core/Application/Users/CreateUser/`
- [X] T020 [P] [US1] پیاده‌سازی سیاست password با قاعده دقیق «حداقل ۸ نویسه و حداقل یک حرف بزرگ، حرف کوچک، عدد و نماد؛ نماد هر نویسه غیر letter/digit» در `src/01.Core/Application/Users/CreateUser/PasswordPolicy.cs`
- [X] T021 [P] [US1] پیاده‌سازی adapter `PasswordHasher<TUser>` و validator کد راه‌اندازی با digest `SHA-256`، `CryptographicOperations.FixedTimeEquals` و configuration خارج از Repository در `src/02.Infra/DataAccess/Users/Security/IdentityPasswordCredentialHasher.cs` و `src/02.Infra/DataAccess/Users/Security/ConfigurationSetupCodeValidator.cs`
- [X] T022 [US1] پیکربندی مدل‌های EF Core با قیود دقیق `users.id TEXT PRIMARY KEY`، `normalized_login_identifier NOT NULL UNIQUE`، `user_credentials.user_id TEXT PRIMARY/FK`، `password_hash NOT NULL` و `user_roles` با کلید مرکب و CHECK نقش در `src/02.Infra/DataAccess/Users/Persistence/UserConfiguration.cs`، `src/02.Infra/DataAccess/Users/Persistence/UserCredentialConfiguration.cs` و `src/02.Infra/DataAccess/Users/Persistence/UserRoleConfiguration.cs`
- [X] T023 [US1] پیاده‌سازی store اتمیک User/Credential/Roles با یک transaction کوتاه و یک commit در `src/02.Infra/DataAccess/Users/Persistence/EfUserProvisioningStore.cs`
- [X] T024 [US1] ایجاد Migration اولیه تکرارپذیر شامل سه جدول، unique constraint و foreign keyها در `src/02.Infra/DataAccess/Migrations/`
- [X] T025 [US1] پیاده‌سازی orchestration مسیر موفق شامل بررسی Setup Code در Application، validation، hashing و commit بدون نشت plaintext به Persistence در `src/01.Core/Application/Users/CreateUser/CreateUserUseCase.cs`
- [X] T026 [US1] پیاده‌سازی request/response و `POST /users` با `Authorization: Setup <code>`، پاسخ `201` و composition وابستگی‌ها در `src/03.Endpoints/Api/Users/CreateUser/` و `src/03.Endpoints/Api/Program.cs`

**Checkpoint**: User Story 1 مستقل اجرا می‌شود و تمام آزمون‌های T011 تا T015 پاس می‌شوند.

---

## Phase 4: User Story 2 — جلوگیری از ثبت نامعتبر یا تکراری (Priority: P2)

**Goal**: ورودی ناقص، Setup Code نامعتبر، password مغایر، Role ناشناخته و LoginIdentifier تکراری یا هم‌زمان بدون هیچ تغییر ناقص رد شوند.

**Independent Test**: هر خطای validation باید `400`، Setup Code غایب/نامعتبر باید پاسخ یکسان `401` همراه `WWW-Authenticate: Setup` و شناسه تکراری یا رقابت هم‌زمان باید `409` بدهد؛ datastore در تمام مسیرهای ردشده بدون User/Credential/Role ناقص باقی بماند.

### Tests for User Story 2 — ابتدا نوشته و شکست آن‌ها تأیید شود

- [X] T027 [P] [US2] نوشتن آزمون‌های Domain برای DisplayName/شناسه خالی، Role نامعتبر، نبود Role و حفظ invariant بدون ساخت Aggregate ناقص در `tests/ChangeFlow.Tests/Users/Domain/UserRejectionTests.cs`
- [X] T028 [P] [US2] نوشتن آزمون‌های Application برای Setup Code غایب/نامعتبر، تمام شکست‌های password policy، فیلدهای ناقص و اثبات عدم فراخوانی hasher/store در rejection زودهنگام در `tests/ChangeFlow.Tests/Users/Application/CreateUserRejectionTests.cs`
- [X] T029 [P] [US2] نوشتن آزمون Integration برای uniqueness بدون حساسیت به case، تکرار سریالی و دو create هم‌زمان با انتظار دقیقاً یک success و یک conflict و بدون نشت `SQLITE_BUSY`/`SQLITE_LOCKED` به HTTP در `tests/ChangeFlow.Tests/Users/Integration/CreateUserConflictTests.cs`
- [X] T030 [P] [US2] نوشتن آزمون Integration rollback هنگام شکست Credential یا Role و بررسی نبود ردیف ناقص در هر سه جدول در `tests/ChangeFlow.Tests/Users/Integration/CreateUserAtomicityTests.cs`
- [X] T031 [P] [US2] نوشتن آزمون قرارداد پاسخ‌های `400/401/409/500`، `ProblemDetails.code` پایدار، `WWW-Authenticate: Setup` و عدم افشای StackTrace/secret در `tests/ChangeFlow.Tests/Users/Contract/CreateUserErrorContractTests.cs`
- [X] T032 [P] [US2] نوشتن آزمون logging برای عدم ثبت Authorization header، request body، password، hash و Setup Code در `tests/ChangeFlow.Tests/Users/Contract/CreateUserSensitiveLoggingTests.cs`

### Implementation for User Story 2

- [X] T033 [P] [US2] تعریف `CreateUserFailure` و انواع `ValidationFailure`، `InvalidSetupCode`، `LoginIdentifierConflict` و `UnexpectedFailure` در `src/01.Core/Application/Users/CreateUser/CreateUserFailure.cs`
- [X] T034 [US2] تکمیل use case برای جمع‌آوری تمام قواعد نقض‌شده password، رد ورودی ناقص/Role نامعتبر و تضمین عدم mutation در مسیر رد در `src/01.Core/Application/Users/CreateUser/CreateUserUseCase.cs`
- [X] T035 [US2] پیاده‌سازی retry محدود SQLite برای `BUSY`/`LOCKED` حداکثر سه بار با وقفه‌های ۵۰، ۱۰۰ و ۲۰۰ میلی‌ثانیه و `DbContext`/transaction جدید؛ ترجمه violation مربوط به `ux_users_normalized_login_identifier` و همچنین وجود شناسه normalized در بررسی نهایی پس از پایان retry به `LoginIdentifierConflict`، و حفظ transient failure فقط هنگام نبود User برنده در `src/02.Infra/DataAccess/Users/Persistence/EfUserProvisioningStore.cs`
- [X] T036 [US2] تکمیل Endpoint برای نگاشت `400/401/409/500` به ProblemDetails دارای `code` پایدار و trace identifier، با پاسخ یکسان برای Setup Code غایب و نامعتبر در `src/03.Endpoints/Api/Users/CreateUser/CreateUserEndpoint.cs`
- [X] T037 [US2] اتصال نتایج و خطاهای CreateUser به زیرساخت عمومی T008 با سطح logging مناسب برای خطاهای موردانتظار و غیرمنتظره و اعتبارسنجی عدم ثبت Setup Code، password و hash در `src/03.Endpoints/Api/Common/Observability/CorrelationMiddleware.cs`
- [X] T038 [US2] هم‌راستا کردن metadata/schema runtime Endpoint با `specs/001-user-role-management/contracts/openapi.yaml` بدون افزودن GET، Location یا رفتار خارج از Scope در `src/03.Endpoints/Api/Users/CreateUser/CreateUserEndpoint.cs`

**Checkpoint**: User Story 2 مستقل با ورودی‌های ردشده قابل‌آزمون است و هیچ مسیر شکست داده پایدار را تغییر نمی‌دهد.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: اثبات معیارهای موفقیت، امنیت و آمادگی تحویل کل Feature

- [X] T039 [P] افزودن آزمون پذیرش latency در Build نوع Release روی API و SQLite file-backed یک میزبان، با ۲۰ درخواست warm-up و سپس ۱۰۰ درخواست معتبر در concurrency برابر ۱۰؛ ثبت p95، تعداد failure و مشخصات محیط و الزام p95 کمتر از ۲ ثانیه و failure برابر صفر در `tests/ChangeFlow.Tests/Users/Performance/CreateUserPerformanceTests.cs`
- [X] T040 [P] ایجاد ماتریس ردیابی FR-001 تا FR-018 و Acceptance Scenarioها به آزمون‌های متناظر در `specs/001-user-role-management/validation/requirements-traceability.md`
- [X] T041 اجرای سناریوهای ۱ تا ۸ `quickstart.md` و ثبت command/result واقعی بدون secret در `specs/001-user-role-management/validation/quickstart-results.md`
- [ ] T042 اجرای ارزیابی SC-001 و SC-005 با حداقل ۲۰ شرکت‌کننده، سناریوی یکسان و بدون راهنمایی؛ ثبت مشخصات نمونه، زمان هر فرد، موفقیت اولین تلاش، تشخیص خطا، نتیجه تلاش اصلاحی و اثبات حداقل `19/20` و `18/20` در `specs/001-user-role-management/validation/usability-results.md`
- [X] T043 اجرای `dotnet restore ChangeFlow.slnx`، `dotnet build ChangeFlow.slnx --no-restore` و `dotnet test ChangeFlow.slnx --no-build` و ثبت نتیجه واقعی و هر ریسک باقی‌مانده در `specs/001-user-role-management/validation/build-test-results.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 — Setup**: بدون وابستگی و نقطه شروع کار است.
- **Phase 2 — Foundational**: پس از Phase 1 و مسدودکننده تمام User Storyها است.
- **Phase 3 — US1**: پس از Foundation؛ MVP مستقل را تحویل می‌دهد.
- **Phase 4 — US2**: پس از Foundation قابل‌آغاز است، اما Taskهای implementation آن روی use case/store/endpoint ساخته‌شده در US1 تکمیل می‌شوند؛ مسیرهای تست منفی آن مستقل‌اند.
- **Phase 5 — Polish**: پس از تکمیل هر دو User Story اجرا می‌شود.

### User Story Dependency Graph

```text
Setup → Foundational → US1 (P1 / MVP)
                    └→ US2 tests
US1 implementation ──→ US2 implementation → Polish
```

### Within Each User Story

- آزمون‌ها پیش از implementation نوشته می‌شوند و ابتدا باید fail شوند.
- Value Object و Aggregate پیش از Application use case تکمیل می‌شوند.
- قراردادهای Application پیش از adapterهای Infrastructure ایجاد می‌شوند.
- مدل persistence و Migration پیش از اجرای Integration test تکمیل می‌شوند.
- use case پیش از Endpoint و mapping HTTP تکمیل می‌شود.

### Parallel Opportunities

- T002 و T003 در Phase 1 قابل‌اجرا به‌صورت موازی‌اند.
- T007 تا T009 پس از T006 و روی فایل‌های مستقل قابل‌اجرا هستند.
- آزمون‌های T011 تا T015 پس از Foundation می‌توانند موازی نوشته شوند.
- T016، T017، T019، T020 و T021 روی فایل‌های مستقل موازی‌اند؛ T018 به T016/T017 وابسته است.
- آزمون‌های T027 تا T032 پس از Foundation قابل‌نوشتن موازی‌اند.
- T039 و T040 پس از تکمیل Storyها روی فایل‌های مستقل موازی‌اند.

## Parallel Example: User Story 1

```text
Task T011: آزمون Domain در tests/ChangeFlow.Tests/Users/Domain/UserCreationTests.cs
Task T012: آزمون Application در tests/ChangeFlow.Tests/Users/Application/CreateUserSuccessTests.cs
Task T013: آزمون adapter امنیتی در tests/ChangeFlow.Tests/Users/Infrastructure/UserSecurityAdapterTests.cs
Task T014: آزمون persistence در tests/ChangeFlow.Tests/Users/Integration/CreateUserPersistenceTests.cs
Task T015: آزمون قرارداد HTTP در tests/ChangeFlow.Tests/Users/Contract/CreateUserSuccessContractTests.cs
```

## Parallel Example: User Story 2

```text
Task T027: آزمون rejectionهای Domain
Task T028: آزمون rejectionهای Application
Task T029: آزمون duplicate و concurrency روی SQLite
Task T030: آزمون rollback اتمیک
Task T031: آزمون قرارداد خطا
Task T032: آزمون عدم نشت داده حساس در Log
```

## Implementation Strategy

### MVP First — User Story 1

1. Phase 1 و Phase 2 را کامل کنید.
2. آزمون‌های T011 تا T015 را نوشته و شکست آن‌ها را تأیید کنید.
3. T016 تا T026 را به‌ترتیب وابستگی اجرا کنید.
4. User Story 1 را مستقل با Setup Code معتبر و هر سه ترکیب Role اعتبارسنجی کنید.
5. پیش از ادامه، اتمیک‌بودن و عدم افشای secret را بررسی کنید.

### Incremental Delivery

1. Setup + Foundation → Solution قابل build و harness آماده
2. US1 → ایجاد موفق User/Credential/Roles به‌عنوان MVP
3. US2 → validation، conflict، rollback و error contract کامل
4. Polish → traceability، performance، usability، Quickstart و Build/Test evidence

## Notes

- SQLite فقط Provider محلی/آموزشی Feature نخست است و نباید از مسیر Taskها به Technical Baseline سراسری تبدیل شود.
- `[P]` فقط برای Taskهای فایل‌مستقل و بدون وابستگی مستقیم درج شده است.
- تمام Taskهای User Story دارای label متناظر هستند.
- تغییر رفتار کشف‌شده هنگام اجرا ابتدا باید در Specification ثبت شود.
- هیچ Taskی مجاز به افزودن پروژه پنجم، Generic Repository یا Business Logic در Endpoint/Infrastructure نیست.


## Phase 6: Convergence

- [X] T044 CRITICAL ثبت اطلاعات تشخیصی امن برای خطاهای غیرمنتظرهٔ hashing و persistence را در مسیر `src/01.Core/Application/Users/CreateUser/CreateUserUseCase.cs` و `src/02.Infra/DataAccess/Users/Persistence/EfUserProvisioningStore.cs` تکمیل کنید. نوع خطا، مرحلهٔ عملیات و correlation identifier را بدون دادهٔ حساب یا اسرار ثبت کنید و پاسخ عمومی 500 و عدم افشای اطلاعات را در `tests/ChangeFlow.Tests/Users/Contract/CreateUserSensitiveLoggingTests.cs` بیازمایید. per Constitution VIII و R-008 (contradicts)
- [X] T045 در `src/03.Endpoints/Api/Users/CreateUser/CreateUserEndpoint.cs` فقط نام‌های دقیق Requester و Reviewer را از ورودی نقش بپذیرید. رشته‌های عددی مانند 0 و 1 و ترکیب چند نام را به خطای validation فیلد roles نگاشت کنید و عدم ذخیرهٔ کاربر یا نقش را در `tests/ChangeFlow.Tests/Users/Contract/CreateUserErrorContractTests.cs` بررسی کنید. per FR-005 و FR-008 و US2/AC3 (contradicts)
- [X] T046 در `src/03.Endpoints/Api/Users/CreateUser/CreateUserEndpoint.cs` و `src/03.Endpoints/Api/Users/CreateUser/CreateUserRequest.cs` اعتبارسنجی بدنه را با قرارداد OpenAPI هم‌راستا کنید. JSON ناقص، نوع نامعتبر فیلد و فیلد اضافی را با 400 و ProblemDetails دارای validation_failed و traceId بدون بازتاب ورودی رد کنید. پوشش قرارداد، عدم ثبت داده و حفظ رفتار Setup را در `tests/ChangeFlow.Tests/Users/Contract/CreateUserErrorContractTests.cs` اضافه کنید. per FR-012 و R-007/R-008 و قرارداد OpenAPI (partial)
