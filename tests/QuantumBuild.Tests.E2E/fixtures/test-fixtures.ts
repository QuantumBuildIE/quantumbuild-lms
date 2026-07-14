import { test as base, Page, BrowserContext } from '@playwright/test';
import { LoginPage } from '../page-objects/LoginPage';
import { DashboardPage } from '../page-objects/DashboardPage';
import { TEST_TENANT } from './test-constants';

/**
 * Custom test fixtures extending Playwright's base test
 */
type CustomFixtures = {
  loginPage: LoginPage;
  dashboardPage: DashboardPage;
  adminContext: BrowserContext;
  supervisorContext: BrowserContext;
  operatorContext: BrowserContext;
  adminPage: Page;
  supervisorPage: Page;
  operatorPage: Page;
};

export const test = base.extend<CustomFixtures>({
  /**
   * Login page object fixture
   */
  loginPage: async ({ page }, use) => {
    await use(new LoginPage(page));
  },

  /**
   * Dashboard page object fixture
   */
  dashboardPage: async ({ page }, use) => {
    await use(new DashboardPage(page));
  },

  /**
   * Browser context authenticated as admin
   */
  adminContext: async ({ browser }, use) => {
    const context = await browser.newContext({
      storageState: 'playwright/.auth/admin.json',
    });
    await use(context);
    await context.close();
  },

  /**
   * Page authenticated as admin
   */
  adminPage: async ({ adminContext }, use) => {
    const page = await adminContext.newPage();
    await use(page);
    await page.close();
  },

  /**
   * Browser context authenticated as supervisor
   */
  supervisorContext: async ({ browser }, use) => {
    const context = await browser.newContext({
      storageState: 'playwright/.auth/supervisor.json',
    });
    await use(context);
    await context.close();
  },

  /**
   * Browser context authenticated as operator
   */
  operatorContext: async ({ browser }, use) => {
    const context = await browser.newContext({
      storageState: 'playwright/.auth/operator.json',
    });
    await use(context);
    await context.close();
  },

  /**
   * Page authenticated as supervisor
   */
  supervisorPage: async ({ supervisorContext }, use) => {
    const page = await supervisorContext.newPage();
    await use(page);
    await page.close();
  },

  /**
   * Page authenticated as operator
   */
  operatorPage: async ({ operatorContext }, use) => {
    const page = await operatorContext.newPage();
    await use(page);
    await page.close();
  },
});

export { expect } from '@playwright/test';
