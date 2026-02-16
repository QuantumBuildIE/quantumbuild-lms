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
  warehouseContext: BrowserContext;
  siteManagerContext: BrowserContext;
  financeContext: BrowserContext;
  adminPage: Page;
  warehousePage: Page;
  siteManagerPage: Page;
  financePage: Page;
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
   * Browser context authenticated as warehouse user
   */
  warehouseContext: async ({ browser }, use) => {
    const context = await browser.newContext({
      storageState: 'playwright/.auth/warehouse.json',
    });
    await use(context);
    await context.close();
  },

  /**
   * Browser context authenticated as site manager
   */
  siteManagerContext: async ({ browser }, use) => {
    const context = await browser.newContext({
      storageState: 'playwright/.auth/sitemanager.json',
    });
    await use(context);
    await context.close();
  },

  /**
   * Browser context authenticated as finance user
   */
  financeContext: async ({ browser }, use) => {
    const context = await browser.newContext({
      storageState: 'playwright/.auth/finance.json',
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
   * Page authenticated as warehouse user
   */
  warehousePage: async ({ warehouseContext }, use) => {
    const page = await warehouseContext.newPage();
    await use(page);
    await page.close();
  },

  /**
   * Page authenticated as site manager
   */
  siteManagerPage: async ({ siteManagerContext }, use) => {
    const page = await siteManagerContext.newPage();
    await use(page);
    await page.close();
  },

  /**
   * Page authenticated as finance user
   */
  financePage: async ({ financeContext }, use) => {
    const page = await financeContext.newPage();
    await use(page);
    await page.close();
  },
});

export { expect } from '@playwright/test';
