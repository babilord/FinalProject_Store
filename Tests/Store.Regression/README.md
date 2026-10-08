# Automated regression testing

Run from the repository root:

```powershell
dotnet restore FinalProject_Store.sln
dotnet build FinalProject_Store.sln --no-restore -c Release
dotnet test FinalProject_Store.sln --no-build --no-restore -c Release --logger "trx;LogFileName=regression.trx" --results-directory Tests/Store.Regression/TestResults
```

The previous console runner is now a discoverable xUnit test containing its original 32 assertions. The other tests report individually, including theory cases. No test starts the production application, loads its configuration or credentials, opens a database, runs migrations, or contacts MinIO. Each service test has its own data and ephemeral payment-protection keys.

On this machine network restore was unavailable. The successful offline restore command was:

```powershell
dotnet restore FinalProject_Store.sln --source "$env:USERPROFILE/.nuget/packages" --packages "$PWD/.test-packages"
```

## Coverage

- Password verification, malformed hashes, hashed and legacy login, email normalization, role requirements, disabled and removed accounts.
- Real ASP.NET authorization middleware using controller authorization metadata: all customer and admin order/payment actions for anonymous, Customer, Operator and Admin principals. Antiforgery rejects missing/forged tokens; mutation attributes and anonymous callback metadata are checked separately.
- Cookie validation rejects invalid/disabled users and refreshes revoked roles.
- Cart additions, quantity updates, ownership, removal, current prices, stock limits, overflow prevention, unavailable products and stock adjustment.
- Checkout validation, order ownership, totals and immutable snapshots, cart clearing, reservation duration and repeat checkout rejection.
- All 36 order status pairs, cancellation/expiration, historical item aggregation, removed products/orders, exactly-once restoration, and expiration worker startup sweeps.
- Protected fake receipts, all payment outcomes, retries, expired attempts, unknown/malformed/mismatched callbacks, conflicting duplicate callbacks, late success, and development-only gateway/simulator restrictions.
- Concurrent gateway token issuance; duplicate payment requests/callbacks and cancellation/expiration races under an explicitly serialized **mock transaction contract**.
- SQL Server EF model metadata for rowversion, expiration indexes and payment uniqueness guards (without opening a connection).

## Results and fixes (2026-10-08)

The original solution build succeeded with 53 warnings and zero errors; the console runner passed 32 checks. Before conversion, `dotnet test` discovered no tests.

With the initial new suite and corrected middleware test setup, **172 tests passed and 4 failed** out of 176. These failures reproduced:

1. Password verification accepted an empty stored digest for any password. It also threw on zero or negative PBKDF2 iteration counts. Verification now requires positive iterations and the existing format's 16-byte salt and 32-byte digest; malformed data returns false.
2. A null checkout request threw during validation. It now returns an unsuccessful result before changing stock or cart data.

Final suite: **195 passed, 0 failed, 0 skipped**. Original assertions remain included in one of those 195 tests. Release build succeeds. Logs and TRX results are ignored generated artifacts.

## Files changed

- `.gitignore`: ignore local restore cache and generated test artifacts.
- `FinalProject_Store.Application/Common/Security/PasswordHasher.cs`: reject malformed stored hashes.
- `FinalProject_Store.Application/Services/Orders/IOrderService.cs`: reject null checkout requests.
- `Tests/Store.Regression/Store.Regression.csproj`: xUnit/test SDK and endpoint project reference.
- `Tests/Store.Regression/Program.cs`: preserve original regression checks as a discoverable test.
- `Tests/Store.Regression/TestContext.cs`: async query support, cart graph synchronization and optional serialized transaction contract.
- `Tests/Store.Regression/ServiceRegressionTests.cs`: service and gateway regression cases.
- `Tests/Store.Regression/AuthorizationRegressionTests.cs`: authorization, antiforgery and simulator cases.
- `Tests/Store.Regression/CookieAndWorkerRegressionTests.cs`: cookie revalidation and worker cases.
- `Tests/Store.Regression/README.md`: commands, results, limits and manual verification checklist.

## Verification limits and remaining risks

`TestContext` is a LINQ-backed service test double. Its async adapter supports cookie/worker queries; it does **not** emulate EF query filters, relationship tracking, SQL isolation, rollback, rowversion generation, unique indexes, or deadlocks. Its opt-in semaphore serializes whole transactions to test policy interleavings, not SQL Server's implementation. Middleware tests execute authorization with synthetic identities; they do not launch MVC routing or exercise browser cookie transport. Antiforgery service checks and attribute checks do not replace a full MVC request test.

SQL LocalDB was installed, but `SqlLocalDB info MSSQLLocalDB` could not access its registry configuration. No database was opened, created, modified, or deleted. Therefore real SQL Server races and transactional rollback remain unverified. Do not interpret the mock race tests as evidence that those database behaviors pass.

In a separately provisioned disposable SQL Server database, verify with independent DbContext instances and synchronized concurrent requests:

1. Two customers check out the final stock unit: at most one succeeds; inventory stays nonnegative; a loser keeps its cart.
2. Concurrent checkouts of the same cart create at most one order. Concurrent first additions of the same cart/product handle unique-index conflicts safely.
3. Duplicate payment requests create one pending attempt; simultaneous success callbacks create one successful payment. Repeat with fresh contexts and independent requests.
4. Race cancellation/expiration against success callbacks and the worker; observe one terminal outcome and exactly one stock restoration, including removed products and multiple orders sharing a product.
5. Inject a failure after the first stock adjustment or payment update and verify rollback using a fresh context. Exercise deadlock/rowversion/unique-index conflicts and safe retry behavior.
6. Verify real global filters and migration-created constraints/indexes, plus expiration cleanup batches larger than 100 and recovery after worker restart/outage.

Manual/browser verification still required: actual login/logout and remember-me cookies over HTTPS, open-redirect rejection, MVC antiforgery forms, role revocation on the next real request, order/cart/payment pages, simulator redirects, and MinIO image access. Verify a deployed worker's 30-minute deadline and configured sweep delay using a controlled test environment. The worker polls, so stock restoration can occur after the deadline by its scan interval/backlog. Fake payments are development-only; no external payment provider is verified.

No production credentials, existing databases, migrations, Git branches, commits or remotes were changed. Existing nullable/legacy-code build warnings remain outside this stabilization scope.
