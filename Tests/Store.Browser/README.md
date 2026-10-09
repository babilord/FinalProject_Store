# Isolated browser tests

From the repository root, after building the solution:

```powershell
dotnet build FinalProject_Store.sln -p:NuGetAudit=false
npm ci --prefix Tests/Store.Browser --cache .npm-cache
powershell -NoProfile -ExecutionPolicy Bypass -File Tests/Store.Browser/run.ps1
```

Chrome must be installed. Playwright uses the [installed Chrome channel](https://playwright.dev/docs/browsers); it does not need a downloaded Chromium bundle. Run one browser suite at a time. Do not rebuild the endpoint while the browser runner is serving it (Windows locks the apphost).

The SQL fixture reads `STORE_BROWSER_SQL` for optional server/authentication settings and **always replaces the catalog** with `FinalProject_Store_BrowserTests`. With no override it uses the same Windows-authenticated server as this repository. Provisioning touches `master` only to check/create this dedicated catalog. An existing catalog requires the `StoreBrowserOwner=Store.Browser.v1` extended property before any migration, seed, expiration adjustment or state query is permitted. All existing application configuration files remain unchanged. Every run seeds unique emails, category and product names; data stays in the owned test catalog for failure investigation. No database is dropped or reset. The runner injects that guarded connection into the application's child process.

For image coverage, put a trusted standalone community `minio.exe` in `.runtime/minio.exe` before running. The runner starts it on loopback ports 9190/9191 with test-only credentials, local `.runtime/minio-data`, and a fresh `browser-<run ID>` bucket. It never connects to the development MinIO endpoint on port 9000. Occupied test ports cause an abort rather than reuse of another service. Without the binary, the image upload/retrieval case is explicitly skipped; all other cases still run. Docker is not required. This machine's attempted official binary downloads returned HTTP 410; supply a locally available verified binary to complete the storage check.

The runner starts a development-only app on port 5187 with the real fake-payment gateway and an expiration worker interval of five seconds. The fixture expires only a pending order in the owned test catalog; the **running application worker** performs cancellation and stock restoration. This exercises scheduling with a shortened deadline, not a real 30-minute wait. Production fake-payment restrictions remain covered by the existing regression suite.

The runner stops only the app process tree and MinIO process it starts, even after test failures. It retains SQL seed data, MinIO test files, screenshots, traces and logs. Generated data is ignored by Git. `.runtime/seed.json` can contain connection credentials if an override uses SQL authentication; do not share runtime artifacts without checking them.

To select scenarios:

```powershell
& ./Tests/Store.Browser/run.ps1 --grep 'catalog|responsive'
```

Results: `test-results/results.json`, `playwright-report/index.html`, failure traces/screenshots and `test-results/rtl-*.png`. Process logs: `.runtime/app.log`, `.runtime/app-error.log`, `.runtime/fixture-build.log`, and optional MinIO logs. `npx playwright show-report` from this directory opens the HTML report. This pass preserved its full-run JSON, HTML and customer screenshots in `.runtime/full-results.json`, `.runtime/full-playwright-report`, and `.runtime/full-screenshots` before executing the final focused rerun.

Tests exercise real MVC requests, cookies, antiforgery, SQL persistence, Persian customer/admin views and actual DOM interactions. Payment management is read-only in the app; tests inspect/filter the payment history rather than inventing mutation controls. Viewport checks use Chrome at 375, 768 and 1366 pixels. They do not establish Safari/Firefox, screen-reader, touch-device, HTTPS cookie transport or production exception-handler coverage. See [REPORT.md](REPORT.md) for executed results and outstanding checks.
