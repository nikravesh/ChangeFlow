# ماتریس ردیابی نیازمندی‌ها — تعریف کاربر و نقش اولیه

**Feature**: `001-user-role-management`  
**تاریخ بازبینی**: 2026-11-15  
**دامنه**: FR-001 تا FR-018، سناریوهای پذیرش US1/US2 و SC-002 تا SC-004

## Functional Requirements

| نیازمندی | شاهد خودکار اصلی | سطح |
|---|---|---|
| FR-001 | `CreateUserSuccessContractTests.Post_users_returns_only_approved_201_projection_and_reuses_setup_code` | HTTP Contract |
| FR-002 | `CreateUserRejectionTests.Missing_fields_and_unknown_role_are_collected_before_mutation` | Application |
| FR-003 | `UserCreationTests.User_creation_trims_name_assigns_stable_id_and_deduplicates_roles` و `CreateUserSuccessTests.Valid_command_returns_public_projection_and_persists_same_user` | Domain/Application |
| FR-004 | `UserCreationTests.Login_identifier_is_trimmed_and_normalized_invariantly` و `CreateUserConflictTests.Serial_case_insensitive_duplicate_returns_conflict` | Domain/Integration |
| FR-005 | `UserCreationTests.Role_contract_contains_only_the_two_approved_values` و `UserRejectionTests.Unknown_role_is_rejected` | Domain |
| FR-006 | `CreateUserSuccessTests.Valid_command_returns_public_projection_and_persists_same_user` برای سه ترکیب Role | Application |
| FR-007 | `UserRejectionTests` و `CreateUserRejectionTests.Missing_fields_and_unknown_role_are_collected_before_mutation` | Domain/Application |
| FR-008 | `CreateUserConflictTests.Serial_case_insensitive_duplicate_returns_conflict` و `CreateUserErrorContractTests.Validation_failure_returns_400_problem_details` | Integration/HTTP |
| FR-009 | `CreateUserPersistenceTests.Store_commits_user_credential_and_roles_atomically_without_plaintext` و `CreateUserAtomicityTests.Failure_while_writing_a_dependent_row_rolls_back_every_table` | Integration |
| FR-010 | `CreateUserConflictTests.Concurrent_equivalent_identifiers_produce_one_created_and_one_conflict` | Integration |
| FR-011 | `CreateUserSuccessContractTests.Post_users_returns_only_approved_201_projection_and_reuses_setup_code` | HTTP Contract |
| FR-012 | `CreateUserErrorContractTests` برای پاسخ‌های `400/401/409/500` | HTTP Contract |
| FR-013 | `CreateUserRejectionTests.Invalid_setup_code_is_rejected_before_hashing_or_persistence` و تست `401` قرارداد | Application/HTTP |
| FR-014 | `CreateUserSensitiveLoggingTests.Create_user_logs_outcome_without_authorization_body_or_secrets` و تست‌های قرارداد موفق/خطا | HTTP/Logging |
| FR-015 | `CreateUserPersistenceTests.Store_commits_user_credential_and_roles_atomically_without_plaintext` | Integration |
| FR-016 | `UserSecurityAdapterTests.Password_hasher_returns_salted_versioned_hash_instead_of_plaintext` و تست‌های response/logging | Infrastructure/HTTP |
| FR-017 | `CreateUserRejectionTests.All_password_rule_failures_are_returned_without_mutation` و `Each_password_policy_rule_is_reported` | Application |
| FR-018 | `UserSecurityAdapterTests.Setup_code_is_reusable_until_rotation_then_old_value_is_rejected` و تست reuse در قرارداد موفق | Infrastructure/HTTP |

## Acceptance Scenarios

| سناریو | شاهد خودکار |
|---|---|
| US1-1 — ایجاد Requester | `CreateUserSuccessTests` و `CreateUserSuccessContractTests` |
| US1-2 — ایجاد هر دو Role | `CreateUserSuccessTests` و `CreateUserPersistenceTests` |
| US1-3 — ثبت اتمیک User و Roleها | `CreateUserPersistenceTests` و `CreateUserAtomicityTests` |
| US1-4 — ثبت Credential بدون افشای password | `UserSecurityAdapterTests`، `CreateUserPersistenceTests` و تست قرارداد موفق |
| US2-1 — شناسه تکراری | `CreateUserConflictTests.Serial_case_insensitive_duplicate_returns_conflict` |
| US2-2 — فیلد الزامی ناقص | `UserRejectionTests` و `CreateUserRejectionTests` |
| US2-3 — Role ناشناخته | `UserRejectionTests.Unknown_role_is_rejected` و تست قرارداد validation |
| US2-4 — Setup Code غایب/نامعتبر | تست Application و `Missing_and_invalid_setup_code_have_identical_401_contract` |
| US2-5 — password مغایر سیاست | تست‌های مستقل تمام قواعد `CreateUserRejectionTests` |
| US2-6 — استفاده مجدد Setup Code | `CreateUserSuccessContractTests` و تست rotation adapter |

## Success Criteria

| معیار | وضعیت شاهد |
|---|---|
| SC-001 | نیازمند مطالعه واقعی حداقل ۲۰ شرکت‌کننده؛ در `usability-results.md` باز است. |
| SC-002 | `CreateUserPerformanceTests.Valid_create_requests_meet_the_recorded_release_latency_target`؛ نتیجه واقعی در `build-test-results.md`. |
| SC-003 | rejectionهای Application، HTTP و rollback دیتابیس؛ تماماً خودکار. |
| SC-004 | `CreateUserConflictTests.Concurrent_equivalent_identifiers_produce_one_created_and_one_conflict`. |
| SC-005 | نیازمند مطالعه واقعی همان شرکت‌کنندگان؛ در `usability-results.md` باز است. |
