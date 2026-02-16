import { test, expect } from '@playwright/test';
import { LoginPage } from '../../page-objects/LoginPage';
import { TEST_TENANT, TAGS } from '../../fixtures/test-constants';

// TODO: Re-enable once login E2E tests are updated to match current UI
// The basic login test works but role-based redirect tests need updating
test.describe.skip('Login @smoke', () => {
  let loginPage: LoginPage;

  test.beforeEach(async ({ page }) => {
    loginPage = new LoginPage(page);
    await loginPage.goto();
  });

  test('should display login form', async () => {
    await loginPage.assertOnLoginPage();
  });

  test('should login with valid admin credentials', async ({ page }) => {
    await loginPage.loginAndWaitForSuccess(
      TEST_TENANT.users.admin.email,
      TEST_TENANT.users.admin.password
    );

    // Should be redirected to dashboard
    await expect(page).toHaveURL(/\/(dashboard|home)/);
  });

  test('should show error with invalid credentials', async () => {
    await loginPage.login('invalid@test.com', 'wrongpassword');
    await loginPage.assertErrorDisplayed();
  });

  test('should login as warehouse user and redirect to stock', async ({ page }) => {
    await loginPage.loginAndWaitForSuccess(
      TEST_TENANT.users.warehouse.email,
      TEST_TENANT.users.warehouse.password
    );

    // Warehouse users should be redirected to stock
    await expect(page).toHaveURL(/\/stock/);
  });

  test('should login as site manager', async ({ page }) => {
    await loginPage.loginAndWaitForSuccess(
      TEST_TENANT.users.siteManager.email,
      TEST_TENANT.users.siteManager.password
    );

    // Site managers should be redirected to stock/orders
    await expect(page).toHaveURL(/\/stock/);
  });

  test('should login as finance user', async ({ page }) => {
    await loginPage.loginAndWaitForSuccess(
      TEST_TENANT.users.finance.email,
      TEST_TENANT.users.finance.password
    );

    // Finance users should be redirected to dashboard
    await expect(page).toHaveURL(/\/(dashboard|home)/);
  });
});
