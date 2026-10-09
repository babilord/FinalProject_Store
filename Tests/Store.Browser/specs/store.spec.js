const { test, expect } = require('@playwright/test');
const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');
const seed = JSON.parse(fs.readFileSync(path.join(__dirname, '../.runtime/seed.json'), 'utf8'));
const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=', 'base64');
function fixture(command, id) {
  const output = execFileSync('dotnet', [process.env.STORE_BROWSER_FIXTURE, command, String(id)], { encoding: 'utf8' });
  return command === 'state' ? JSON.parse(output.trim().split(/\r?\n/).at(-1)) : output;
}
async function login(page, email = seed.customer, remember = false) {
  await page.goto('/Authentication/Login');
  await page.locator('#Email').fill(email);
  await page.locator('#Password').fill(seed.password);
  await page.locator('#RememberMe').setChecked(remember);
  await page.locator('#LoginButton').click();
  await expect(page).toHaveURL(/\/$/);
}
async function confirm(page) {
  await page.locator('.swal2-confirm').click();
}
async function logout(page) {
  await page.locator('.nav-account').first().hover();
  await page.locator('form[action="/Authentication/Logout"] button').first().click();
  await expect.poll(async () => (await page.context().cookies()).some(c => c.name === '.AspNetCore.Cookies')).toBe(false);
}
async function checkout(page, validate = false) {
  await page.goto(`/Products/Details/${seed.productId}`);
  await page.locator('form[action="/Cart/Add"] button').click();
  await page.goto('/Cart');
  await expect(page.locator('main')).toContainText(seed.productName);
  await page.locator('a[href="/Orders/Checkout"]').click();
  if (validate) {
    await page.locator('form[action="/Orders/Checkout"] button').click();
    await expect(page.locator('[data-valmsg-for="Input.FullName"]')).not.toBeEmpty();
    await expect(page).toHaveURL(/Checkout/);
    // Wait for any invalid POST/re-render before filling the corrected values.
    await page.reload();
  }
  const fields = { FullName: 'مشتری آزمایشی', MobileNumber: '09123456789', Province: 'تهران', City: 'تهران', PostalAddress: 'خیابان آزمایشی پلاک ۱۲', PostalCode: '1234567890' };
  for (const [key, value] of Object.entries(fields)) await page.locator(`#Input_${key}`).fill(value);
  await page.locator('form[action="/Orders/Checkout"] button').click();
  await expect(page).toHaveURL(/\/Orders\/Details\/\d+/);
  return Number(page.url().split('/').at(-1));
}

test('registration validation, duplicate email, login cookies and logout', async ({ page, context }) => {
  const email = `registered.${seed.run}@browser.test`;
  await page.goto('/Authentication/Register');
  await page.locator('button[onclick="RegisterUser()"]').click();
  await expect(page.locator('.swal2-popup')).toBeVisible();
  await confirm(page);
  for (const [key, value] of Object.entries({ FullName: 'کاربر فارسی', Email: email, Password: seed.password, RePassword: 'wrong' })) await page.locator(`#${key}`).fill(value);
  await page.locator('button[onclick="RegisterUser()"]').click();
  await expect(page.locator('.swal2-popup')).toBeVisible();
  await confirm(page);
  await page.locator('#RePassword').fill(seed.password);
  const registered = page.waitForResponse(r => r.url().endsWith('/Authentication/Register') && r.request().method() === 'POST');
  await page.locator('button[onclick="RegisterUser()"]').click();
  expect((await (await registered).json()).isSuccess).toBe(true);
  await confirm(page);
  await expect(page).toHaveURL(/Login/);
  await page.goto('/Authentication/Register');
  for (const [key, value] of Object.entries({ FullName: 'کاربر فارسی', Email: email, Password: seed.password, RePassword: seed.password })) await page.locator(`#${key}`).fill(value);
  const duplicate = page.waitForResponse(r => r.url().endsWith('/Authentication/Register') && r.request().method() === 'POST');
  await page.locator('button[onclick="RegisterUser()"]').click();
  expect((await (await duplicate).json()).isSuccess).toBe(false);
  await confirm(page);
  await page.goto('/Authentication/Login?returnUrl=https://example.org');
  await page.locator('#Email').fill(email);
  await page.locator('#Password').fill('wrong');
  await page.locator('#LoginButton').click();
  await expect(page.locator('.swal2-popup')).toBeVisible();
  await confirm(page);
  await page.locator('#Password').fill(seed.password);
  await page.locator('#RememberMe').check();
  await page.locator('#LoginButton').click();
  await expect(page).toHaveURL('http://127.0.0.1:5187/');
  expect((await context.cookies()).find(c => c.name === '.AspNetCore.Cookies').expires).toBeGreaterThan(Date.now() / 1000);
  await logout(page);
  expect((await context.cookies()).some(c => c.name === '.AspNetCore.Cookies')).toBe(false);
  await page.goto('/Orders');
  await expect(page).toHaveURL(/Authentication\/Login/);
});

for (const identity of ['anonymous', 'customer', 'operatorEmail', 'admin']) {
  test(`role access: ${identity}`, async ({ page }) => {
    if (identity !== 'anonymous') await login(page, seed[identity]);
    for (const section of ['Products', 'Categories', 'Users', 'Orders', 'Payments']) {
      const response = await page.goto(`/Admin/${section}`);
      if (identity === 'admin') expect(response.status()).toBe(200);
      else if (identity === 'anonymous') await expect(page).toHaveURL(/Authentication\/Login/);
      else { await expect(page).toHaveURL(/AccessDenied/); expect(response.status()).toBe(403); }
    }
  });
}

test('catalog browse, search, details and shared header search', async ({ page }) => {
  await page.goto('/Products');
  await page.locator('#catalogSearch').fill(seed.run);
  await page.locator('main form button[type="submit"]').click();
  await expect(page.locator('.catalog-product-title')).toContainText(seed.productName);
  await page.locator('.catalog-product-title a').click();
  await expect(page.locator('main h1')).toHaveText(seed.productName);
  const search = page.locator('.header-search-input:visible').first();
  await search.fill(seed.run);
  await search.press('Enter');
  await expect(page).toHaveURL(/Products.*searchKey=/);
  await expect(page.locator('.catalog-product-title')).toContainText(seed.productName);
  await page.locator('#catalogSearch').fill('definitely-no-result-' + seed.run);
  await page.locator('main form button[type="submit"]').click();
  await expect(page.locator('main')).toContainText('یافت نشد');
});

test('customer checkout validation, failed/cancelled payment retry, success and history', async ({ page }) => {
  await login(page);
  const before = fixture('state', seed.productId).inventory;
  const id = await checkout(page, true);
  expect(fixture('state', seed.productId).inventory).toBe(before - 1);
  for (const outcome of ['Failed', 'Cancelled', 'Succeeded']) {
    await page.locator('form[action="/Payments/Pay"] button').click();
    await expect(page).toHaveURL(/FakePayments\/Simulate/);
    await page.locator(`button[value="${outcome}"]`).click();
    await expect(page).toHaveURL(new RegExp(`/Orders/Details/${id}`));
  }
  await expect(page.locator('main')).toContainText('پرداخت شده');
  expect(fixture('state', seed.productId).orders.find(o => o.id === id).status).toBe('Paid');
  await page.goto('/Orders');
  await expect(page.locator(`a[href="/Orders/Details/${id}"]`)).toBeVisible();
});

test('customer cancellation restores stock exactly once', async ({ page }) => {
  await login(page);
  const before = fixture('state', seed.productId).inventory;
  const id = await checkout(page);
  await page.locator('form[action="/Orders/Cancel"] button').click();
  await expect(page.locator('main')).toContainText('سفارش لغو شده');
  expect(fixture('state', seed.productId).inventory).toBe(before);
  await page.reload();
  expect(fixture('state', seed.productId).inventory).toBe(before);
  expect(fixture('state', seed.productId).orders.find(o => o.id === id).status).toBe('Cancelled');
});

test('live expiration worker cancels expired order and restores inventory', async ({ page }) => {
  await login(page);
  const before = fixture('state', seed.productId).inventory;
  const id = await checkout(page);
  fixture('expire', id);
  await expect.poll(() => fixture('state', seed.productId).orders.find(o => o.id === id).expired, { timeout: 20000 }).toBe(true);
  await page.reload();
  await expect(page.locator('main')).toContainText('مهلت پرداخت تمام شد');
  expect(fixture('state', seed.productId).inventory).toBe(before);
  await expect(page.locator('form[action="/Payments/Pay"]')).toHaveCount(0);
});

test('order ownership, missing pages and real MVC antiforgery', async ({ page, context }) => {
  await login(page);
  const id = await checkout(page);
  const other = await context.browser().newContext();
  const otherPage = await other.newPage();
  await login(otherPage, seed.operatorEmail);
  expect((await otherPage.goto(`/Orders/Details/${id}`)).status()).toBe(404);
  expect((await page.goto('/Products/Details/9223372036854775807')).status()).toBe(404);
  await expect(page.locator('main h1')).toContainText('یافت نشد');
  expect((await page.goto('/missing-browser-test-page')).status()).toBe(404);
  const response = await page.request.post('/Cart/Add', { form: { productId: seed.productId, quantity: 1 } });
  expect(response.status()).toBe(400);
  await other.close();
});

test('admin category create and modal edit', async ({ page }) => {
  await login(page, seed.admin);
  await page.goto('/Admin/Categories');
  const name = 'دسته مرورگر ' + seed.run;
  await page.locator('#categoryName').fill(name);
  await page.locator('form[action="/Admin/Categories/Create"] button').click();
  const row = page.locator('tr').filter({ hasText: name });
  await expect(row).toBeVisible();
  await row.locator('button').click();
  await page.locator('#editCategoryName').fill(name + ' ویرایش');
  await page.locator('button[onclick="EditCategory()"]').click();
  await confirm(page);
  await expect(page.locator('tr').filter({ hasText: name + ' ویرایش' })).toBeVisible();
});

test('admin product image upload, MinIO byte retrieval, edit, deactivate and delete', async ({ page }) => {
  test.skip(process.env.STORE_BROWSER_MINIO !== '1', 'Isolated MinIO server unavailable');
  await login(page, seed.admin);
  await page.goto('/Admin/Products/Create');
  const name = 'تصویر مرورگر ' + seed.run;
  for (const [id, value] of Object.entries({ Name: name, Brand: 'MinIO', Price: '23000', Inventory: '7', Description: 'توضیح محصول با تصویر' })) await page.locator(`#${id}`).fill(value);
  await page.locator('#CategoryId').selectOption(String(seed.categoryId));
  await page.locator('#Image').setInputFiles({ name: 'image.png', mimeType: 'image/png', buffer: png });
  await page.locator('form button[type="submit"]').click();
  await page.goto('/Admin/Products?searchKey=' + encodeURIComponent(seed.run));
  const row = page.locator('tr').filter({ hasText: name });
  await expect(row).toBeVisible();
  const edit = row.locator('a[href*="/Admin/Products/Edit"]');
  const id = Number((await edit.getAttribute('href')).split('/').at(-1));
  const image = await page.request.get(`/Products/Image/${id}`);
  expect(image.status()).toBe(200);
  expect(image.headers()['content-type']).toContain('image/png');
  expect(await image.body()).toEqual(png);
  await page.goto(`/Products/Details/${id}`);
  await expect(page.locator('.detail-product-image')).toBeVisible();
  expect(await page.locator('.detail-product-image').evaluate(el => el.complete && el.naturalWidth > 0)).toBe(true);
  await page.goto(`/Admin/Products/Edit/${id}`);
  await page.locator('#Name').fill(name + ' ویرایش');
  await page.locator('form button[type="submit"]').click();
  await confirm(page);
  await expect(page.locator('tr').filter({ hasText: name + ' ویرایش' })).toBeVisible();
  const changed = page.locator('tr').filter({ hasText: name + ' ویرایش' });
  await changed.locator('button[onclick^="changeStatus"]').click();
  await confirm(page); await confirm(page);
  expect((await page.goto(`/Products/Details/${id}`)).status()).toBe(404);
  await page.goto('/Admin/Products?searchKey=' + encodeURIComponent(name));
  await page.locator('tr').filter({ hasText: name }).locator('button[onclick^="deleteProduct"]').click();
  await confirm(page); await confirm(page);
  await expect(page.locator('tr').filter({ hasText: name })).toHaveCount(0);
});

test('admin product validation and management without an image', async ({ page }) => {
  await login(page, seed.admin);
  await page.goto('/Admin/Products/Create');
  expect(await page.locator('#Price').evaluate(el => el.validity.rangeUnderflow)).toBe(true);
  await page.locator('#Price').fill('1');
  await page.locator('form button[type="submit"]').click();
  await expect(page.locator('[data-valmsg-for="Name"]')).toHaveClass(/field-validation-error/);
  await page.reload();
  const name = 'مدیریت محصول ' + seed.run;
  for (const [id, value] of Object.entries({ Name: name, Brand: 'Browser', Price: '18000', Inventory: '4', Description: 'محصول بدون تصویر' })) await page.locator(`#${id}`).fill(value);
  await page.locator('#CategoryId').selectOption(String(seed.categoryId));
  await page.locator('#Image').setInputFiles({ name: 'wrong.png', mimeType: 'image/png', buffer: Buffer.from('not an image') });
  await page.locator('form button[type="submit"]').click();
  await expect(page.locator('.validation-summary-errors')).toContainText('مطابقت ندارد');
  await page.locator('#Image').setInputFiles([]);
  await page.locator('form button[type="submit"]').click();
  await page.goto('/Admin/Products?searchKey=' + encodeURIComponent(name));
  const row = page.locator('tr').filter({ hasText: name });
  await expect(row).toBeVisible();
  await row.locator('a[href*="/Admin/Products/Edit"]').click();
  const id = Number(page.url().split('/').at(-1));
  await page.locator('#Price').fill('19000');
  await page.locator('form button[type="submit"]').click();
  await confirm(page); // Success notification rendered after edit redirect.
  await expect(page.locator('tr').filter({ hasText: name })).toContainText('19,000');
  await page.locator('tr').filter({ hasText: name }).locator('button[onclick^="changeStatus"]').click();
  await confirm(page); await confirm(page);
  expect((await page.goto(`/Products/Details/${id}`)).status()).toBe(404);
  await page.goto('/Admin/Products?searchKey=' + encodeURIComponent(name));
  await page.locator('tr').filter({ hasText: name }).locator('button[onclick^="deleteProduct"]').click();
  await confirm(page); await confirm(page);
  await expect(page.locator('tr').filter({ hasText: name })).toHaveCount(0);
});

test('admin user create, edit, disable, enable and delete', async ({ page }) => {
  await login(page, seed.admin);
  const email = 'managed.' + seed.run + '@browser.test';
  await page.goto('/Admin/Users/Create');
  for (const [id, value] of Object.entries({ fullname: 'کاربر مدیریت', email, Password: seed.password, RePassword: seed.password })) await page.locator(`#${id}`).fill(value);
  await page.locator('#RoleId').selectOption('3');
  await page.locator('[onclick="Registeruser()"]').click();
  await confirm(page); await confirm(page);
  await page.goto('/Admin/Users?searchkey=' + encodeURIComponent(email));
  const row = page.locator('tr').filter({ hasText: email });
  await expect(row).toBeVisible();
  await row.locator('button[onclick^="ShowModalEdituser"]').click();
  await page.locator('#Edit_FullName').fill('کاربر ویرایش شده');
  await page.locator('button[onclick="EditUser()"]').click();
  await expect(page.locator('tr').filter({ hasText: email })).toContainText('کاربر ویرایش شده');
  for (let i = 0; i < 2; i++) {
    await Promise.all([page.waitForEvent('load'), page.locator('tr').filter({ hasText: email }).locator('button[onclick^="UserStatusChange"]').click()]);
  }
  await page.locator('tr').filter({ hasText: email }).locator('button[onclick^="DeleteUser"]').click();
  await confirm(page); await confirm(page);
  await expect(page.locator('tr').filter({ hasText: email })).toHaveCount(0);
});

test('admin user pagination shows actual totals and preserves search', async ({ page }) => {
  await login(page, seed.admin);
  await page.goto('/Admin/Users?searchKey=' + seed.paginationKey);
  await expect(page.locator('#DataTables_Table_0 tbody tr')).toHaveCount(20);
  await expect(page.locator('#DataTables_Table_0_info')).toContainText('21');
  await page.getByRole('link', { name: 'بعدی', exact: true }).click();
  expect(new URL(page.url()).searchParams.get('searchKey')).toBe(seed.paginationKey);
  await expect(page.locator('#DataTables_Table_0 tbody tr')).toHaveCount(1);
  await expect(page.locator('#DataTables_Table_0_info')).toContainText('21');
  await page.getByRole('link', { name: 'قبلی', exact: true }).click();
  await expect(page.locator('#DataTables_Table_0 tbody tr')).toHaveCount(20);
});

test('admin paid order transitions and payment history filtering', async ({ page }) => {
  await login(page);
  const id = await checkout(page);
  await page.locator('form[action="/Payments/Pay"] button').click();
  await page.locator('button[value="Succeeded"]').click();
  await loginAsAdmin(page);
  await page.goto(`/Admin/Orders/Details/${id}`);
  for (const target of ['Processing', 'Shipped', 'Delivered']) {
    await page.locator(`form:has(input[name="target"][value="${target}"]) button`).click();
    expect(fixture('state', seed.productId).orders.find(o => o.id === id).status).toBe(target);
  }
  await page.locator(`section a[href="/Admin/Payments?orderId=${id}"]`).click();
  await expect(page.locator('table')).toContainText('FAKE-');
});
async function loginAsAdmin(page) {
  await page.goto('/');
  await logout(page);
  await login(page, seed.admin);
}

for (const width of [375, 768, 1366]) {
  test(`Persian RTL and responsive pages at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    for (const route of ['/Authentication/Login', '/Authentication/Register', '/Products', `/Products/Details/${seed.productId}`, '/missing-browser-test-page']) {
      await page.goto(route);
      await expect(page.locator('.P-loader')).toBeHidden();
      await expect(page.locator('html')).toHaveAttribute('lang', 'fa');
      await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
      const overflow = await page.evaluate(() => ({ scroll: document.documentElement.scrollWidth, elements: [...document.querySelectorAll('body *')].filter(el => { const r = el.getBoundingClientRect(); return r.width && r.right > innerWidth + 1; }).slice(0, 15).map(el => ({ tag: el.tagName, cls: el.className, right: el.getBoundingClientRect().right, width: el.getBoundingClientRect().width })) }));
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `${route} ${JSON.stringify(overflow)}`).toBe(true);
    }
    await login(page);
    for (const route of ['/Cart', '/Orders']) {
      await page.goto(route);
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), route).toBe(true);
    }
    await page.screenshot({ path: `test-results/rtl-${width}.png`, fullPage: true });
    expect(errors).toEqual([]);
  });
}

test('admin Persian language and responsive management pages', async ({ page }) => {
  await login(page, seed.admin);
  for (const width of [375, 768, 1366]) {
    await page.setViewportSize({ width, height: 900 });
    for (const route of ['/Admin/Products', '/Admin/Products/Create', '/Admin/Categories', '/Admin/Users', '/Admin/Orders', '/Admin/Payments']) {
      await page.goto(route);
      await expect(page.locator('html')).toHaveAttribute('lang', 'fa');
      await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
      const overflow = await page.evaluate(() => [...document.querySelectorAll('.main-panel *')].map(el => ({ tag: el.tagName, cls: el.className, left: el.getBoundingClientRect().left, right: el.getBoundingClientRect().right })).filter(el => el.right > innerWidth + 1 || el.left < 0).slice(0, 15));
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `${route} at ${width} ${JSON.stringify(overflow)}`).toBe(true);
    }
  }
});
