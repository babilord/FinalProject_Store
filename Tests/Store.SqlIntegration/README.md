# Real SQL Server integration tests

Run from the repository root with a Windows identity permitted to create databases on the configured SQL Server:

```powershell
dotnet test FinalProject_Store.sln -p:NuGetAudit=false --logger trx --results-directory Tests/TestResults
```

To run only this suite:

```powershell
dotnet test Tests/Store.SqlIntegration/Store.SqlIntegration.csproj -p:NuGetAudit=false --logger "trx;LogFileName=sql-integration.trx" --results-directory Tests/TestResults
```

The fixture reads the server/authentication settings from `EndPoint.Site/appsettings.json` and forcibly replaces the catalog with `FinalProject_Store_IntegrationTests` before opening any application connection. An optional `STORE_SQL_INTEGRATION_CONNECTION` environment variable supplies alternative test server credentials; its catalog is also forcibly replaced. Application configuration is never edited. Database creation uses `master`, and migrations execute only in the dedicated test catalog. An existing database must have the fixture's `StoreIntegrationOwner=Store.SqlIntegration.v1` extended property before any migration is allowed.

Each test seeds unique customer emails and its own category/product/cart. Independent connections and contexts execute competing requests. Checkout uses a barrier immediately before both saves so both transactions have read the last stock unit; a deadlock/concurrency loser must fail safely and retain its cart. Other concurrent tests start four requests together and use the application's SQL order locks. Rollback injects an exception after `SaveChanges` has actually written to SQL Server but before the service commits, then verifies stock, orders, order items and cart using a fresh context.

Cleanup deletes only each scenario's recorded IDs and their dependent rows, inside a transaction. The owned test database and migrated schema remain for reuse. No truncation, database drop, or writes to an existing application database occur. Run one instance of this suite at a time; database provisioning is not coordinated across simultaneous test processes.

Seven discoverable cases cover last-unit checkout competition, transaction rollback, concurrent expiration with exactly-once restoration, customer cancellation, concurrent duplicate callbacks, concurrent payment requests/retries (including expired attempts), and valid/invalid order transitions with fresh-context persistence checks. The payment gateway is the real development fake gateway with ephemeral protected receipts; no external provider is contacted.

## Verified results: 2026-10-09

- Server: `DESKTOP-B71VTVJ`, default `MSSQLSERVER` service, Windows authentication as `DESKTOP-B71VTVJ\ASUS`.
- Initial sandbox SQL probe failed with `SSL Provider: No credentials are available in the security package`. The same configured server/authentication succeeded outside the sandbox, with database creation permission. No SQL Server or application configuration changes were needed.
- Initial sandbox regression build encountered `NETSDK1064` for missing analyzer packages. Restore/build outside the sandbox resolved dependency access without changing package versions.
- SQL integration suite: **7 passed, 0 failed, 0 skipped**.
- Existing regression suite: **195 passed, 0 failed, 0 skipped** (including the original 32 checks in one test).
- All 11 existing migrations applied only to the new test database. Post-run counts for Users, Products, Categories, Carts, CartItems, Orders, OrderItems and Payments were all **0**. Seeded Roles and migration history remain.
- No reproducible application bugs found in these scenarios. Existing application source/configuration was unchanged.

TRX artifacts are generated under `Tests/TestResults` and ignored by Git. These tests verify real SQL persistence and transactional contention for the listed cases; they do not verify browser flows, an external payment provider, or worker scheduling.
