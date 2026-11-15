# نتایج Build، Test و Performance

**تاریخ اجرا**: 2026-11-15  
**Feature**: `001-user-role-management`

## Performance Acceptance — SC-002

فرمان‌ها:

```powershell
dotnet build ChangeFlow.slnx -c Release --no-restore
dotnet test tests/ChangeFlow.Tests/ChangeFlow.Tests.csproj -c Release --no-build --filter "Category=Performance" --logger "console;verbosity=detailed"
```

محیط ثبت‌شده:

```text
OS: Microsoft Windows 10.0.26300
Framework: .NET 11.0.0
Architecture: X64
Logical processors: 32
API and SQLite file-backed database: same host
Warm-up requests: 20
Measured requests: 100
Concurrency: 10
```

نتیجه واقعی:

```text
p95: 516.89 ms
Failures: 0
Required maximum p95: 2000 ms
Result: PASS
```

## Restore / Build / Test — T043

```powershell
dotnet restore ChangeFlow.slnx
```

نتیجه: موفق؛ تمام پروژه‌ها up-to-date بودند.

```powershell
dotnet build ChangeFlow.slnx --no-restore
```

نتیجه واقعی:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

```powershell
dotnet test ChangeFlow.slnx --no-build
```

نتیجه واقعی:

```text
Passed! - Failed: 0, Passed: 41, Skipped: 0, Total: 41
Duration: 6 s
```

## ریسک و کار باقی‌مانده

- SC-001 و SC-005 هنوز با شرکت‌کننده انسانی اجرا نشده‌اند؛ T042 باز است و فرم اجرای مطالعه در `validation/usability-results.md` قرار دارد.
- نتیجه SC-002 متعلق به محیط ثبت‌شده فوق است و تضمین performance برای میزبان یا Provider دیتابیس دیگری محسوب نمی‌شود.
- Provider سراسری Production همچنان طبق Technical Baseline تصمیم باز است؛ SQLite فقط Provider محلی/آموزشی این Feature است.
