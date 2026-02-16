import { test as setup, expect } from '@playwright/test';
import { TEST_TENANT } from '../fixtures/test-constants';

const STORAGE_DIR = 'playwright/.auth';

/**
 * Authenticate as admin user and save storage state
 */
setup('authenticate as admin', async ({ page }) => {
  await page.goto('/login');
  await page.fill('[name="email"], #email, input[type="email"]', TEST_TENANT.users.admin.email);
  await page.fill('[name="password"], #password, input[type="password"]', TEST_TENANT.users.admin.password);
  await page.click('button[type="submit"]');

  // Wait for redirect to authenticated area
  await page.waitForURL(/\/(dashboard|home|stock|admin)/, { timeout: 15000 });

  // Verify we're logged in
  await expect(page.locator('body')).not.toContainText('Login');

  // Save storage state
  await page.context().storageState({ path: `${STORAGE_DIR}/admin.json` });
});

/**
 * Authenticate as warehouse user and save storage state
 */
setup('authenticate as warehouse', async ({ page }) => {
  await page.goto('/login');
  await page.fill('[name="email"], #email, input[type="email"]', TEST_TENANT.users.warehouse.email);
  await page.fill('[name="password"], #password, input[type="password"]', TEST_TENANT.users.warehouse.password);
  await page.click('button[type="submit"]');

  // Wait for redirect to stock area (warehouse home page)
  await page.waitForURL(/\/(stock|dashboard)/, { timeout: 15000 });

  await page.context().storageState({ path: `${STORAGE_DIR}/warehouse.json` });
});

/**
 * Authenticate as site manager and save storage state
 */
setup('authenticate as site manager', async ({ page }) => {
  await page.goto('/login');
  await page.fill('[name="email"], #email, input[type="email"]', TEST_TENANT.users.siteManager.email);
  await page.fill('[name="password"], #password, input[type="password"]', TEST_TENANT.users.siteManager.password);
  await page.click('button[type="submit"]');

  // Wait for redirect to stock orders (site manager home page)
  await page.waitForURL(/\/(stock|dashboard|site-attendance)/, { timeout: 15000 });

  await page.context().storageState({ path: `${STORAGE_DIR}/sitemanager.json` });
});

/**
 * Authenticate as office staff and save storage state
 */
setup('authenticate as office staff', async ({ page }) => {
  await page.goto('/login');
  await page.fill('[name="email"], #email, input[type="email"]', TEST_TENANT.users.officeStaff.email);
  await page.fill('[name="password"], #password, input[type="password"]', TEST_TENANT.users.officeStaff.password);
  await page.click('button[type="submit"]');

  await page.waitForURL(/\/(dashboard|proposals|stock)/, { timeout: 15000 });

  await page.context().storageState({ path: `${STORAGE_DIR}/officestaff.json` });
});

/**
 * Authenticate as finance user and save storage state
 */
setup('authenticate as finance', async ({ page }) => {
  await page.goto('/login');
  await page.fill('[name="email"], #email, input[type="email"]', TEST_TENANT.users.finance.email);
  await page.fill('[name="password"], #password, input[type="password"]', TEST_TENANT.users.finance.password);
  await page.click('button[type="submit"]');

  await page.waitForURL(/\/(dashboard|stock|proposals)/, { timeout: 15000 });

  await page.context().storageState({ path: `${STORAGE_DIR}/finance.json` });
});
