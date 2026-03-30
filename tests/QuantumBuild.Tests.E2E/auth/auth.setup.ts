import { test as setup } from '@playwright/test';
import { TEST_TENANT } from '../fixtures/test-constants';

// Next.js dev server compiles pages on first visit — allow extra time
setup.setTimeout(90000);

const STORAGE_DIR = 'playwright/.auth';

/**
 * Helper: wait for post-login redirect and fail fast if DPA page appears.
 */
async function waitForAuthRedirect(page: import('@playwright/test').Page, urlPattern: RegExp) {
  // Next.js dev server may need to compile the target page on first hit — allow extra time
  await page.waitForURL(
    /\/(dashboard|admin|toolbox-talks|dpa-acceptance)/,
    { timeout: 60000 }
  );
  if (page.url().includes('dpa-acceptance')) {
    throw new Error(
      'Redirected to DPA acceptance page — ensure TestTenantSeeder seeds a DpaAcceptance record'
    );
  }
  // Now verify we actually matched the expected pattern
  await page.waitForURL(urlPattern, { timeout: 60000 });
}

/**
 * Helper: fill login form and submit.
 * Waits for the email input to be visible and enabled before interacting,
 * handling Next.js compilation delay.
 */
async function loginAs(page: import('@playwright/test').Page, email: string, password: string) {
  await page.goto('/login');
  // Wait for the form to be rendered and interactive (handles Next.js compilation)
  await page.waitForSelector('#email:not([disabled])', { timeout: 30000 });
  await page.fill('#email', email);
  await page.fill('#password', password);
  await page.click('button[type="submit"]');
}

/**
 * Authenticate as admin user and save storage state
 */
setup('authenticate as admin', async ({ page }) => {
  await loginAs(page, TEST_TENANT.users.admin.email, TEST_TENANT.users.admin.password);

  // Wait for redirect to authenticated area (admin with employeeId may land on /toolbox-talks)
  await waitForAuthRedirect(page, /\/(dashboard|admin|toolbox-talks)/);

  // Save storage state
  await page.context().storageState({ path: `${STORAGE_DIR}/admin.json` });
});

/**
 * Authenticate as supervisor and save storage state
 */
setup('authenticate as supervisor', async ({ page }) => {
  await loginAs(page, TEST_TENANT.users.supervisor.email, TEST_TENANT.users.supervisor.password);

  // Supervisor lands on toolbox talks (employee portal with team management)
  await waitForAuthRedirect(page, /\/(toolbox-talks|dashboard)/);

  await page.context().storageState({ path: `${STORAGE_DIR}/supervisor.json` });
});

/**
 * Authenticate as operator and save storage state
 */
setup('authenticate as operator', async ({ page }) => {
  await loginAs(page, TEST_TENANT.users.operator.email, TEST_TENANT.users.operator.password);

  // Operator lands on toolbox talks (employee portal)
  await waitForAuthRedirect(page, /\/(toolbox-talks|dashboard)/);

  await page.context().storageState({ path: `${STORAGE_DIR}/operator.json` });
});
