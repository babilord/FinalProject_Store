End-to-end testing and UI stabilization — 2026-10-09

Work is on `feature/e2e-ui-stabilization`, created from freshly fetched `origin/main` at `2176a57`. No commit, push, merge or reset was performed. The existing development/production database and MinIO data were not used for tests or changed.

The full Chrome browser run completed with **19 passed, 0 failed, 1 skipped** out of 20 cases (4.2 minutes). Counts refer to test cases, not individual assertions or every URL visited within a case. The MinIO case is the skipped test. The final user-list markup cleanup was verified with **3 affected cases passed, 0 failed, 0 skipped** (29.7 seconds); these repeat existing cases and are not added to the unique scenario total.

| Final solution verification | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Existing regression suite | 195 | 0 | 0 |
| Existing real SQL integration suite | 7 | 0 | 0 |
| **Existing automated tests total** | **202** | **0** | **0** |

`dotnet build FinalProject_Store.sln -p:NuGetAudit=false --nologo` succeeded with **0 errors, 0 warnings** in the final incremental build. The baseline compilation emitted 53 existing warnings; this pass does not claim to have fixed those legacy warnings. The browser fixture was also compiled and executed successfully. `dotnet test FinalProject_Store.sln --no-build --no-restore --nologo --logger trx --results-directory Tests/TestResults/E2EFinal` passed both projects. Their TRX files confirm 195 and 7 executed cases. Logs: `Tests/build-e2e-final.log` and `Tests/full-tests-e2e-final.log`. `git diff --check` passed.

An earlier attempt to build solution tests while the browser app was serving hit a Windows apphost file lock. The app was stopped and the final build/test commands above succeeded. That earlier incomplete invocation is not counted as a passing full suite.

| Executed browser coverage | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Registration, validation, duplicate email, failed/successful login, remember-me expiry, open-redirect rejection and logout | 1 | 0 | 0 |
| Anonymous, Customer, Operator and Admin access across all five admin areas | 4 | 0 | 0 |
| Catalog browsing, search, shared header search, empty results and details | 1 | 0 | 0 |
| Cart addition, checkout validation, fake payment failure/cancellation/retry/success and history | 1 | 0 | 0 |
| Customer cancellation with inventory restoration | 1 | 0 | 0 |
| Live worker expiration with inventory restoration and payment denial | 1 | 0 | 0 |
| Cross-user order access, real MVC antiforgery rejection and 400/404 pages | 1 | 0 | 0 |
| Admin category create and modal edit | 1 | 0 | 0 |
| Admin product validation, bad image signature rejection, create/edit/deactivate/delete without storage | 1 | 0 | 0 |
| Admin user create/edit/disable/enable/delete | 1 | 0 | 0 |
| Admin user paging and preserved search | 1 | 0 | 0 |
| Admin order Processing/Shipped/Delivered transitions and order-filtered payment history | 1 | 0 | 0 |
| Customer Persian RTL/geometry checks and no page JavaScript errors at 375/768/1366 px | 3 | 0 | 0 |
| Six admin management pages at all three viewport widths, with Persian language and RTL metadata | 1 | 0 | 0 |
| Real MinIO image upload, retrieval bytes/content type and browser decoding | 0 | 0 | 1 |
| **Total** | **19** | **0** | **1** |

The full-run JSON and HTML report are preserved in `.runtime/full-results.json` and `.runtime/full-playwright-report/index.html`; customer screenshots are in `.runtime/full-screenshots`. Final affected-case artifacts remain in `test-results` and `playwright-report`. Full and focused run logs are `Tests/browser-final.log` and `Tests/browser-final-affected.log`.

The first browser run reported **8 passed, 8 failed, 1 skipped**. Browser traces identified real shared-search, missing-plugin and tablet-overflow defects, along with synchronization/selector mistakes in the new tests. Subsequent focused runs verified fixes, admin product management, invalid image rejection, live expiration and stock restoration, admin transitions, and responsive layouts. New user pagination coverage uses 21 uniquely seeded accounts to verify actual page boundaries and search preservation.

Changes:

- Both desktop and mobile header search forms now submit `searchKey` to the product catalog, with a Persian accessible name and maximum query length.
- Optional `lightGallery` initialization checks for both the element and plugin, eliminating the shared JavaScript exception on pages without that gallery.
- Catalog filter buttons wrap instead of extending 24 pixels outside the tablet viewport.
- Category create/search controls wrap and stack on phones; user search controls wrap and the user table scrolls within its container.
- User paging replaces the hard-coded 57-row count and dummy links with real counts and previous/next links preserving search. Queries order by user ID; controller page values are at least one. The inert row-size selector and demo instructions were replaced, the actions column received a heading, and table/paging markup was balanced.
- Admin pages declare Persian language and RTL direction and use their actual title. Registration labels reference inputs, with email direction/type and appropriate autocomplete hints.
- Added a Playwright suite, locked npm dependencies, guarded SQL seed/state/expiration fixture, process-owning PowerShell runner, traces/screenshots and repeatable instructions. Application settings and migrations were not edited.

The browser app uses **only** `FinalProject_Store_BrowserTests`, guarded by `StoreBrowserOwner=Store.Browser.v1`. Every run creates unique accounts, category and product data. Existing SQL integration tests independently enforce their own `FinalProject_Store_IntegrationTests` catalog and ownership marker. Provisioning creates these dedicated catalogs through `master`; it never migrates or seeds the application's configured development catalog. Owned test data remains available for diagnosis rather than being dropped or reset.

MinIO verification is blocked on this machine: Docker has no available engine and attempted official community/archived binary URLs returned HTTP 410. No stand-in storage or development bucket was used. The image test is explicitly skipped unless an isolated standalone MinIO binary is available. When enabled, it uses loopback ports 9190/9191, `.runtime/minio-data`, test-only credentials and a fresh per-run bucket, checking uploaded PNG response bytes, content type and browser decoding. Invalid image signature rejection runs without MinIO and is verified separately.

The real hosted expiration worker runs with a five-second interval. A guarded fixture changes a pending test order's deadline to the past, then browser/SQL checks verify worker cancellation, no further payment action, and stock restoration. This does not claim a real 30-minute wall-clock wait. The development fake gateway was exercised; no external payment provider was contacted. Admin payment management is intentionally read-only in this application.

Remaining manual/environment checks:

1. Supply a verified standalone community MinIO binary and rerun the skipped upload/retrieval scenario. Also inspect replacement-image cleanup, missing-object fallback, upload-size boundaries and persisted objects after a restart.
2. HTTPS authentication/cookie transport, browser restart persistence for remember-me, and production reverse-proxy deployment behavior. The executed browser server uses loopback HTTP.
3. Firefox, Safari, real touch devices, keyboard navigation and screen-reader review. Chrome geometry checks and screenshots do not establish these.
4. Production exception handling with a controlled 500 error; development browser coverage verifies actual 400, 403 and 404 behavior. Production fake-gateway denial remains covered by the existing automated regression suite, not a production browser launch.
5. A real reservation deadline wait, worker restart/backlog behavior and deployment timing; controlled worker expiration and SQL concurrency tests establish the narrower executed behavior.
6. Visual review of Persian typography/content and legacy template/demo links outside the tested store-management and customer routes.

Repeatable commands and artifact locations are in [README.md](README.md). Logs, runtime seed data, screenshots and traces are ignored by Git. Do not share seed/trace artifacts blindly when using SQL-authentication overrides, because the runtime connection and browser requests may contain test credentials.
