# Final development pass — 22 September 2026

The implementation described below is present in the working tree. Database migrations were **not applied**. SQL Server concurrency and the full authenticated purchase/MinIO browser regression remain to be run after applying the migration locally. Automated checks below are explicitly separated from those outstanding integration checks.

## Continuation verification — 23 September 2026

Reviewed the existing diff and retained the completed implementation. The original request was unavailable in the continuation context, so this report supplied the recoverable scope; completeness against any additional original requirements has not been established.

- Added `Tests/Store.Regression` to the solution's existing Test folder so solution builds compile the regression executable as well as the six application projects. Running the executable remains a separate command.
- Product edit save failures now clear tracked changes. A concurrent update returns the specific Persian reload-and-retry message while retaining replacement-image cleanup.
- Removed three duplicate registration imports.
- Final solution build passed with **0 errors and 53 nullable warnings** after restoring from the installed package cache. All **32 regression checks** passed again. `git diff --check` passed; Git emitted only line-ending conversion notices.
- The default restore-enabled build failed with NU1301 because this session could not establish NuGet's SSL connection for repository signature metadata. The following offline restore and build succeeded without changing project dependencies or disabling signature verification:

```powershell
dotnet restore FinalProject_Store.sln --packages C:/Users/ASUS/.nuget/packages --source C:/Users/ASUS/.nuget/packages --verbosity minimal
dotnet build FinalProject_Store.sln --no-restore --nologo --verbosity minimal
dotnet run --project Tests/Store.Regression --no-build --no-restore
```
- EF confirmed no pending model changes. The restricted Windows session could not write to Event Log on the first attempt; disabling that logging provider for the check resolved it without changing application configuration:

```powershell
dotnet ef migrations has-pending-model-changes --project FinalProject_Store.Persistence --startup-project EndPoint.Site --no-build -- --environment Development --Logging:EventLog:LogLevel:Default None
```

No database migration or live SQL/MinIO/browser integration test was performed during this continuation. The outstanding checks in section 19 remain outstanding.

## 1. Existing implementation preserved

The six-project solution keeps its existing Domain, Application, Persistence, Infrastructures, Common, and MVC endpoint boundaries. Application services still use `IDataBaseContext`; SQL Server-specific locking stays in Persistence; the expiration host stays in the MVC endpoint.

Preserved: cookie authentication, PBKDF2 hashing and legacy-login password upgrade, roles, Admin Users/Categories/Products, catalog/search/details, MinIO storage and image validation, Cart, checkout shipping validation, immutable order-item name/price snapshots, customer ownership checks, Fake/Test gateway, protected receipts, payment history/retries/reference IDs, and Persian customer templates. Existing restrictive product/order/payment relationships, decimal precision, soft-delete filters, cart uniqueness, and unique pending/successful payment indexes remain.

All old migrations remain unchanged. The pre-existing `main.css` header fix was present when work started and was not edited during this pass.

## 2. Incomplete or incorrect behavior found

- Checkout deducted inventory but orders had no reservation deadline or release path.
- Customer order cancellation, fulfillment progression, Admin Orders, and Admin Payments were missing.
- Retrying payment could grant a new attempt deadline independent of an order reservation.
- Admin product edits could overwrite inventory from a stale form or concurrent checkout.
- Authentication and Admin user AJAX mutations lacked antiforgery enforcement; logout used GET.
- Cookies did not revalidate disabled/deleted users or changed roles.
- Admin user names/emails were interpolated into executable JavaScript attributes.
- Cart quantity addition could overflow an integer.
- Some user-management exceptions exposed internal exception messages.
- Registration/category/product validation had gaps; product prices with excess precision could be rounded by the database.
- Customer order history was unbounded; error pages and password reveal behavior were inconsistent.
- Application referenced a machine-specific EF Core 7 DLL while Persistence/Endpoint used EF Core 9 packages. Endpoint also contained redundant EF Core 7 DLL references.

## 3. Implemented

Added a reservation deadline and expiration reason flag, a shared order lifecycle service, SQL order locks, product rowversion protection and an inventory check constraint, a background expiration worker, safe customer cancellation, fulfillment transitions, paginated Admin Orders/Payments and customer order history, Persian status/error messages, shared password controls, structured lifecycle/payment logging, and targeted security/validation fixes.

Application now uses the same EF Core 9.0.12 package as Persistence/Endpoint. The obsolete machine-specific references were removed. No new operational infrastructure or frontend framework was introduced.

## 4. Exact inventory lifecycle

1. Checkout validates the authenticated active user, shipping fields, database cart, product existence/removal/activity, positive price, positive quantity, and available inventory.
2. Inside a serializable transaction it creates the order and price/name snapshots, subtracts reserved quantities from available inventory, and clears the cart.
3. The order is `PendingPayment`, with `ExpiresAtUtc = UTC now + 30 minutes`.
4. A verified successful payment before the deadline changes it to `Paid`. **Payment never deducts inventory.**
5. Failed/cancelled payment attempts retain the reservation until the original deadline.
6. Explicit customer cancellation or expiration changes only a pending order to `Cancelled`, adds back stored `OrderItems.Quantity`, and cancels any remaining pending payment attempt in one transaction.
7. Expiration sets `ReservationExpired = true`; ordinary early customer cancellation leaves it false.

Example: stock 5, reserve 4 → available 1. Failure/retry/success → still 1. Unpaid cancellation/expiration → 5. Repeating cancellation/cleanup → still 5.

## 5. Exact state-transition rules

| From | To | Allowed trigger |
| --- | --- | --- |
| PendingPayment (1) | Paid (2) | Valid gateway verification before both order/attempt deadlines |
| PendingPayment (1) | Cancelled (3) | Owning active customer cancels, or reservation expires |
| Paid (2) | Processing (4) | Authorized Admin fulfillment action |
| Processing (4) | Shipped (5) | Authorized Admin fulfillment action |
| Shipped (5) | Delivered (6) | Authorized Admin fulfillment action |

All other transitions are rejected. Cancelled and Delivered are terminal. Paid/Processing/Shipped/Delivered cancellation is rejected with a Persian refund-out-of-scope message. Repeating an already-completed cancellation does not restore inventory again. Repeating an Admin transition does not advance an additional step.

Existing numeric values 1, 2, and 3 were preserved.

## 6. Payment retry and expiration

- A valid pending attempt is reused on repeat payment requests.
- A failed/cancelled attempt may be retried only while the order remains pending and unexpired.
- New attempt expiry is capped at the order's original deadline; retry never extends the reservation.
- The simulator checks owner, account activity/removal, order removal/status, and both deadlines.
- The callback locks the order, verifies the protected receipt and server-side amount, then rechecks status/deadlines and active account before a new payment outcome is accepted.
- A forged receipt cannot consume a pending attempt, and terminal attempts also require receipt verification.
- A valid replay never changes a terminal payment or inventory. After a receipt itself expires, replay can return a verification error but cannot undo the original outcome.
- If expiration wins the order lock, later success cannot mark it Paid. If valid success wins before the deadline, cleanup sees Paid and does nothing.
- A page left open past expiry may still contain an old button; the server rejects the action. Freshly rendered expired/terminal pages have no payment action.

Fake payments remain Development-only. No merchant credentials or real provider were added.

## 7. Double-decrement/restoration protection

Checkout retains serializable isolation and validates database quantities/prices. Product rowversion prevents lost updates, and `CK_Products_Inventory` rejects negative stock at the database boundary. Stale Admin edit forms are rejected using the original rowversion; a later race is also rejected by EF concurrency checking.

Payment request, callback, cancellation, expiration, and fulfillment all acquire an SQL Server `UPDLOCK, HOLDLOCK` on the same order before reading mutable order state. The order status is the release guard: only PendingPayment can restore inventory. Status, products, and pending payment cancellation commit together or roll back together. Soft-deleted products/orders still participate in automatic release, and item quantities are read without soft-delete filters for restoration.

Concurrent deadlocks/conflicts may cause an operation to return a retry message; they do not partially commit. The worker retries failed pending orders on a later sweep. Database lock behavior was reviewed, not simulated by the policy test double.

## 8. Background expiration design

`OrderExpirationWorker` scans immediately on startup and every 60 seconds by default. Each scan selects up to 100 overdue PendingPayment IDs, ordered by deadline/ID, using the new `(Status, ExpiresAtUtc)` index. Each order is processed in a fresh dependency-injection scope/context through the same lifecycle service.

No in-memory reservation registry is needed. Restarts and multiple application instances re-read database status under the order lock. Exceptions are logged and retried on later sweeps. Paid orders are excluded and rechecked under lock.

Set `Orders:ExpirationIntervalSeconds` in configuration, or for local testing:

```powershell
$env:Orders__ExpirationIntervalSeconds = "10"
```

The supported interval is clamped to 5–3600 seconds. Heavy backlog can require multiple batches; payment still becomes invalid at the deadline even if stock release is awaiting the worker.

## 9. Admin additions

- `/Admin/Orders`: 20-row pages; search order ID, customer/recipient name, email or mobile; status filter.
- `/Admin/Orders/Details/{id}`: customer ID/name/email, shipping snapshot, items, quantities, unit/line/total price snapshots, date, status, reservation deadline, successful payment reference, and link to attempts.
- Only the next permitted fulfillment action is shown; the service revalidates the transition.
- `/Admin/Payments`: 20-row pages; order filter, status filter, and search by order/reference/gateway; amount, status, gateway, created/updated/deadline timestamps.
- Tokens, receipts and credentials are not displayed; payment history is read-only. Removed historical orders/accounts remain inspectable by Admin.
- Existing Admin role authorization and layout are reused. Menu links were added.

## 10. Customer additions

Pending orders can be cancelled by their owner. Order lists/details show all six Persian statuses. Details show reservation expiry, expired/cancelled messages, attempt feedback, and payment reference through fulfillment. Payment actions appear only for a currently payable order. My Orders uses 20-row pagination.

## 11. Security and validation fixes

Global automatic antiforgery validation now covers unsafe MVC requests. Both shared layouts supply same-origin AJAX antiforgery headers; regular forms retain generated tokens. Logout is POST-only. The gateway callback remains GET with protected receipt verification; read-only error handlers ignore antiforgery so failed POSTs can display an error page.

Authenticated requests revalidate account activity/removal and refresh role claims. Existing order/cart ownership predicates were preserved; new cancellation uses the authenticated claim, and Admin fulfillment gets the actor ID from that claim. Browser-posted totals, prices, payment status and reference IDs are never accepted as purchase authority.

Admin user edit data now uses encoded data attributes instead of interpolation into executable JavaScript. User-management error responses no longer expose exception details. Registration validates email, name length and 8–128-character new passwords; existing passwords still use the existing login rules. Soft-deleted emails remain reserved by registration, matching the unique database index. Category creation enforces its 200-character mapping. Product services reject excess decimal precision/range. Checkout validates shipping fields at service level as well as MVC level. Cart addition uses a wider intermediate integer to prevent overflow.

Structured logs cover checkout failure/inventory conflict, release/expiration, payment request, invalid verification/callback, payment outcomes, and Admin transitions. New logs do not record passwords, tokens, receipts, shipping details, or credentials.

## 12. UI/layout changes and checks

Shared password buttons use `type="button"`, `aria-controls`, `aria-pressed`, visible Persian labels, normal keyboard behavior, and visible focus. Input values/names/validation attributes are preserved. Edge's native reveal icon is suppressed inside the wrapper. Login, Register and existing Admin password inputs use the same script.

Added Persian 404/403/500 handling with no production exception details. Fixed the existing Admin product-edit image preview's missing `src` URL. New tables use responsive wrappers, and new pagination/forms use existing Bootstrap styles.

The existing `header.header-main { display: flow-root; }` fix is unchanged. Headless Chrome and Edge each checked Login and Register at 1366px and 390px: all eight cases had the expected controls, unchanged password values, content below the header, and no horizontal overflow. A mobile screenshot was visually inspected. This does not substitute for the authenticated pages in section 19.

## 13. Every repository file created

- `EndPoint.Site/Areas/Admin/Controllers/OrdersController.cs`
- `EndPoint.Site/Areas/Admin/Controllers/PaymentsController.cs`
- `EndPoint.Site/Areas/Admin/Views/Orders/Details.cshtml`
- `EndPoint.Site/Areas/Admin/Views/Orders/Index.cshtml`
- `EndPoint.Site/Areas/Admin/Views/Payments/Index.cshtml`
- `EndPoint.Site/Services/OrderExpirationWorker.cs`
- `EndPoint.Site/Services/StoreCookieEvents.cs`
- `EndPoint.Site/Views/Home/Status.cshtml`
- `EndPoint.Site/wwwroot/css/store-forms.css`
- `EndPoint.Site/wwwroot/js/store-forms.js`
- `FinalProject_Store.Application/Services/Orders/AdminOrderService.cs`
- `FinalProject_Store.Application/Services/Orders/OrderLifecycleService.cs`
- `FinalProject_Store.Persistence/Migrations/20260922131335_AddOrderReservationLifecycle.Designer.cs`
- `FinalProject_Store.Persistence/Migrations/20260922131335_AddOrderReservationLifecycle.cs`
- `Tests/Store.Regression/Program.cs`
- `Tests/Store.Regression/Store.Regression.csproj`
- `Tests/Store.Regression/TestContext.cs`
- `docs/Final-Development-Report.md`

## 14. Every repository file modified

- `FinalProject_Store.sln`
- `EndPoint.Site/Areas/Admin/Controllers/ProductsController.cs`
- `EndPoint.Site/Areas/Admin/Models/Products/EditProductViewModel.cs`
- `EndPoint.Site/Areas/Admin/Views/Products/Edit.cshtml`
- `EndPoint.Site/Areas/Admin/Views/Shared/_Adminlayout.cshtml`
- `EndPoint.Site/Areas/Admin/Views/Users/Index.cshtml`
- `EndPoint.Site/Controllers/AuthenticationController.cs`
- `EndPoint.Site/Controllers/FakePaymentsController.cs`
- `EndPoint.Site/Controllers/HomeController.cs`
- `EndPoint.Site/Controllers/OrdersController.cs`
- `EndPoint.Site/EndPoint.Site.csproj`
- `EndPoint.Site/Program.cs`
- `EndPoint.Site/Views/Orders/Details.cshtml`
- `EndPoint.Site/Views/Orders/Index.cshtml`
- `EndPoint.Site/Views/Shared/Error.cshtml`
- `EndPoint.Site/Views/Shared/_Layout.cshtml`
- `FinalProject_Store.Application/FinalProject_Store.Application.csproj`
- `FinalProject_Store.Application/Interfaces/Contexts/IDataBaseContext.cs`
- `FinalProject_Store.Application/Services/Carts/ICartService.cs`
- `FinalProject_Store.Application/Services/Categories/Commands/IAddCategoryService.cs`
- `FinalProject_Store.Application/Services/Orders/IOrderService.cs`
- `FinalProject_Store.Application/Services/Payments/IPaymentService.cs`
- `FinalProject_Store.Application/Services/Products/Commands/AddProduct/AddProductService.cs`
- `FinalProject_Store.Application/Services/Products/Commands/EditProduct/IEditProductService.cs`
- `FinalProject_Store.Application/Services/Products/Queries/GetProductDetails/IGetProductDetailsService.cs`
- `FinalProject_Store.Application/Services/Users/Commands/EditUser/IEditUserService.cs`
- `FinalProject_Store.Application/Services/Users/Commands/RegisterUser/IRegisterUserService.cs`
- `FinalProject_Store.Application/Services/Users/Commands/RemoveUser/RemoveUserService.cs`
- `FinalProject_Store.Application/Services/Users/Commands/UserStatusChange/IUserStatusChangeService.cs`
- `FinalProject_Store.Domain/Entities/Orders/Order.cs`
- `FinalProject_Store.Domain/Entities/Orders/OrderStatus.cs`
- `FinalProject_Store.Domain/Entities/Products/Product.cs`
- `FinalProject_Store.Persistence/Contexts/DataBaseContext.cs`
- `FinalProject_Store.Persistence/Migrations/DataBaseContextModelSnapshot.cs`

Pre-existing working-tree change preserved, **not modified by this pass**:

- `EndPoint.Site/wwwroot/Site Template/assets/css/main.css`

Temporary build logs, migration SQL preview, smoke-test scripts, browser profiles and screenshots were created outside the repository. Test/build `bin`/`obj` outputs are ignored. No temporary package cache remains in the working tree.

## 15. Migration

`20260922131335_AddOrderReservationLifecycle`

Adds `Orders.ExpiresAtUtc`, `Orders.ReservationExpired`, `Products.RowVersion`, the nonnegative inventory constraint, and the expiration index. The generated snapshot/designer match the model.

Legacy PendingPayment orders receive 30 minutes from migration execution using `SYSUTCDATETIME()`. Their old local-time InsertTime is deliberately not converted using a guessed timezone. Existing Paid/Cancelled history is not restocked or reclassified. If historical inventory is already negative, the new constraint will reject deployment until that data is investigated; the migration does not silently rewrite inventory.

Migration SQL was generated and reviewed. No migration or database update was executed.

## 16. Exact local database-update command

From the solution directory, with your existing local connection/storage configuration available:

```powershell
dotnet ef database update --project FinalProject_Store.Persistence --startup-project EndPoint.Site --context DataBaseContext -- --environment Development
```

Stop older running copies of the website while deploying the new schema/code, then restart the site. This is required because the new code expects the new columns.

## 17. Build and verification result

```powershell
dotnet build FinalProject_Store.sln --nologo --verbosity:minimal
dotnet run --project Tests/Store.Regression
dotnet ef migrations has-pending-model-changes --project FinalProject_Store.Persistence --startup-project EndPoint.Site --no-build -- --environment Development
```

- Final continuation solution build passed: **0 errors, 53 nullable warnings**. See the continuation verification section for the NuGet SSL failure and successful cache-based restore commands.
- Regression executable passed **32 checks**, including all 36 fulfillment state pairs, ownership, failed/cancelled attempts, retry deadline capping, forged/mismatched receipts, successful callback replay, paid/cancelled/expired rejection, exact stock restoration, inactive accounts/products, checkout validation, duplicate checkout, quantity overflow, and model indexes/concurrency mapping.
- These service-policy tests use an explicitly limited in-memory query double. They do not open a database or claim to test SQL locking, query filters, rollback, or concurrent execution.
- EF reported no pending model changes.
- Chrome/Edge smoke tests passed as described above.
- HTTP checks: Persian 404 and 403, unauthenticated Admin Orders/Payments redirects, missing-antiforgery login POST rejected with 400, valid-antiforgery empty login returned a validation response.
- Production mode: an actual catalog database-connection failure and `/Home/Error` returned Persian 500 responses without SQL exceptions/stack traces. Smoke servers used an intentionally unreachable database and were stopped afterward.
- Complete tracked diff and new files reviewed; `git diff --check` passed. No old migration was changed, and no unrelated source formatting was performed.

## 18. Remaining warnings and limits

Historical nullable warnings were not broadly cleaned up; duplicate registration imports were removed during continuation. EF still warns about the pre-existing required User/UserInRole relationship combined with a user query filter. Installed `dotnet-ef` is 9.0.10 versus runtime 9.0.12; migration generation and model validation still succeeded.

NuGet initially hit sandbox SSL/cache-path failures; restoring from the installed package cache with an absolute package path resolved them. Browser smoke tests needed normal Windows access to the existing Data Protection keys; no authentication design change was made for that environment issue.

Live SQL concurrency, migration execution against existing data, authenticated purchase/Admin screens, and MinIO upload/display were not exercised against your database because schema changes were intentionally left unapplied. Existing template demonstration links/widgets and legacy Admin Users pagination presentation remain outside this focused pass. New order/payment lists are fully paginated.

## 19. Exact manual regression plan, in order

Use local Development mode with SQL Server and MinIO running. Repeat visual checks in Chrome and Edge, at desktop width and about 390px. Use two separate browser profiles for customer A and customer B, plus an Admin session.

1. Apply the migration using section 16, start the site, and run the regression executable. Confirm the worker starts without schema errors. For quicker sweeps use the interval setting in section 8; reservation duration remains 30 minutes.
2. Register customer A and B. Try empty fields, invalid email, duplicate email, short password, and mismatched confirmation. Verify valid registration/login, Remember Me, POST logout, and that a posted RoleId cannot grant Admin access.
3. On Login/Register/Admin user creation, Tab to each reveal button and press Space/Enter. Verify show/hide changes only visibility, preserves the value, does not submit, has a visible focus indicator, and has no duplicate Edge icon.
4. As Admin, list/search/create/edit/disable a test user; verify role permissions. A customer must be denied `/Admin/Orders` and `/Admin/Payments`. Disable customer B while B is signed in; the next protected request must reject the session. Re-enable and sign in again.
5. Create/edit a category; reject empty/overlong names. Create a product with stock 5 and a known price, upload a valid JPG/PNG/WebP, and verify image display on Admin edit, catalog and details. Reject invalid file types/content/oversized uploads and invalid prices/negative inventory. Verify the current image survives a failed replacement.
6. Search/filter/page the catalog. Inspect product cards/details at both widths. Add the test product, change quantity, remove/re-add it. Submit zero, negative and extremely large quantities using DevTools: none may result in an invalid cart or purchase. Try another user's cartItemId: no access/change.
7. Set quantity 4 from stock 5. On checkout reject invalid recipient/mobile/address/postcode. Attempt to post altered UserId/price/total fields: order ownership and totals must come from the session/database. Deactivate or remove a cart product before submit: checkout must reject it.
8. With an active product, submit valid checkout, including a rapid double submit. Expect one order for that cart, correct item/name/price/shipping snapshots, PendingPayment, a UTC deadline about 30 minutes ahead, available inventory 1, and an empty cart. Refreshing/reposting must not reserve again.
9. From customer B, request A's order details, pay and cancel endpoints with A's ID. Expect denial/not-found and no change to A's order, attempts or inventory.
10. Pay A's order through Fake/Test, select Failed. Expect PendingPayment, stock 1, a failed attempt in Admin history, and a retry button. Retry and select Cancelled: same order/stock, another recorded attempt, no stock release. Abandon another attempt: reservation remains only until the original deadline.
11. Retry and succeed before expiry. Expect Paid, a payment reference, stock still 1, no payment/cancel buttons, and a successful attempt. Replay the exact callback and repeat Pay: inventory and payment count/outcome must not change. Alter receipt/token: reject it. A stale failure/cancel callback must never undo success.
12. Create a separate unpaid reservation. Explicitly cancel from My Orders. Expect Cancelled and exact stock restoration. Repeat the POST and reopen old simulator/callback URLs: no extra restoration and no new success. Confirm a paid-order cancellation request returns the refund-out-of-scope message.
13. Create another unpaid reservation and wait past its displayed deadline. Before/after the next worker sweep, Pay must be rejected. After the sweep expect Cancelled, `ReservationExpired`, cancelled pending attempt, exact stock restoration, and an expiry message. Repeat sweeps and restart the application: stock stays unchanged. Repeat after soft-deleting the reserved product in Admin: stock must still be released in the database.
14. To shorten test 13 in a disposable local test dataset, manually run `UPDATE Orders SET ExpiresAtUtc = DATEADD(second, 15, SYSUTCDATETIME()) WHERE Id = <your-unpaid-test-order-id> AND Status = 1;` in your SQL client. Never change a production deadline for this test. Open the simulator before the deadline and submit success around/after it. The only valid outcomes are Paid with reserved stock retained, or Cancelled with stock restored; never Paid with stock restored.
15. Test concurrency using separate sessions: both customers checkout quantity 4 against the same stock 5 simultaneously. At most one checkout may succeed; the other must receive a conflict/stock message, and available stock cannot be negative. Repeat with different quantities sharing stock.
16. Run two website instances against the same local test DB (different ports). Let both workers expire the same unpaid order while also clicking customer Cancel or submitting a valid callback. Verify one final outcome, exact stock accounting and no duplicate successful payment. Any deadlock/conflict should roll back and be retryable; no partial restoration may remain.
17. Open an Admin product edit form, then reserve stock from another session. Submit the old edit form: it must reject the stale version. Reload before editing again. Repeat while an expiration restores stock; stale input must not overwrite it.
18. In Admin Orders, search by ID/name/email/mobile, filter each status, and verify pagination with more than 20 rows (also test page 0 and a very large page). Inspect shipping/items/totals/reference. Advance Paid → Processing → Shipped → Delivered. Attempt direct/skipped/backward/unknown transitions and duplicate POSTs: reject them. Verify each Persian status on the customer's list/details, without payment actions.
19. In Admin Payments, filter an order and each outcome, search reference/gateway, and page more than 20 attempts. Compare amount/reference/timestamps to the order and simulator outcomes. Confirm no tokens, receipts, credentials, edit/refund buttons or mutable historical fields are exposed.
20. Remove antiforgery tokens/headers from checkout, cancel, pay, fulfillment, authentication and Admin user POSTs: expect rejection and no mutation. Use a test user's name containing quotes/HTML, then open Admin user edit: it must display as data without executing code.
21. Test nonexistent routes (404), customer Admin access (403), and a deliberately unavailable database in a separate Production-mode local instance (500). Confirm Persian messages and no exception details; Development should retain diagnostics.
22. Finally review Products, Details, Cart, Checkout, My Orders, Order Details, Fake simulator, Login/Register and every Admin page at desktop/mobile widths in both browsers. Verify header flow, RTL text, readable cards, reachable forms/buttons, table scrolling, and images. Inspect history after changing a product's current name/price: old order snapshots must remain unchanged.

## 20. Intentionally deferred and Git safety

No real gateway, merchant credentials, refunds, wallet, coupons, SMS/email infrastructure, postal API, advanced tracking, reviews, recommendations, Kafka, Redis, Hangfire/Quartz, major redesign or new frontend framework was added.

No commit, push, merge, rebase, reset, history rewrite or migration deletion was performed. All requested source changes and the new migration remain in the working tree for review. Database migration application remains your explicit local step.
