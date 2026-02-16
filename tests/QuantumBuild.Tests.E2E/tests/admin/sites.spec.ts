import { test, expect } from '../../fixtures/test-fixtures';
import { SiteListPage, SiteFormPage } from '../../page-objects/admin';
import { generateTestData, TAGS } from '../../fixtures/test-constants';

// TODO: Re-enable once admin module E2E tests are updated to match current UI
test.describe.skip('Sites Admin @smoke', () => {
  test.use({ storageState: 'playwright/.auth/admin.json' });

  test('should display sites list', async ({ page }) => {
    const siteListPage = new SiteListPage(page);
    await siteListPage.goto();

    await expect(siteListPage.pageTitle).toBeVisible();
    await expect(siteListPage.table).toBeVisible();
  });

  test('should search sites', async ({ page }) => {
    const siteListPage = new SiteListPage(page);
    await siteListPage.goto();

    await siteListPage.search('Dublin');
    await siteListPage.waitForPageLoad();
  });

  test('should navigate to create site', async ({ page }) => {
    const siteListPage = new SiteListPage(page);
    await siteListPage.goto();
    await siteListPage.clickCreate();

    await expect(page).toHaveURL(/\/admin\/sites\/new/);
  });
});

// TODO: Re-enable once admin module E2E tests are updated to match current UI
test.describe.skip('Sites Admin - Create Site @regression', () => {
  test.use({ storageState: 'playwright/.auth/admin.json' });

  test('should create a new site', async ({ page }) => {
    const siteFormPage = new SiteFormPage(page);
    await siteFormPage.goto();

    const siteName = generateTestData.uniqueString('Site');

    await siteFormPage.fillForm({
      name: siteName,
      address: '123 Test Street',
      city: 'Dublin',
      county: 'Dublin',
      postcode: 'D01 ABC',
      latitude: 53.3498,
      longitude: -6.2603,
      geofenceRadius: 100,
      isActive: true,
    });

    await siteFormPage.saveAndWaitForSuccess();

    // Should be redirected to site list
    await expect(page).toHaveURL(/\/admin\/sites/);
  });
});
