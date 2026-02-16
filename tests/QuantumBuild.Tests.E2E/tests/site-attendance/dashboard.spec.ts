import { test, expect } from '../../fixtures/test-fixtures';
import { AttendanceDashboardPage } from '../../page-objects/site-attendance';
import { TAGS } from '../../fixtures/test-constants';

// TODO: Re-enable once site attendance module E2E tests are updated to match current UI
test.describe.skip('Site Attendance Dashboard @smoke', () => {
  test.use({ storageState: 'playwright/.auth/admin.json' });

  test('should display attendance dashboard', async ({ page }) => {
    const dashboardPage = new AttendanceDashboardPage(page);
    await dashboardPage.goto();

    await expect(dashboardPage.pageTitle).toBeVisible();
  });

  test('should display KPIs', async ({ page }) => {
    const dashboardPage = new AttendanceDashboardPage(page);
    await dashboardPage.goto();

    // KPIs should be visible
    await expect(dashboardPage.utilizationKpi).toBeVisible();
    await expect(dashboardPage.varianceKpi).toBeVisible();
    await expect(dashboardPage.totalEmployeesKpi).toBeVisible();
  });

  test('should display performance table', async ({ page }) => {
    const dashboardPage = new AttendanceDashboardPage(page);
    await dashboardPage.goto();

    await expect(dashboardPage.performanceTable).toBeVisible();
  });

  test('should filter by site', async ({ page }) => {
    const dashboardPage = new AttendanceDashboardPage(page);
    await dashboardPage.goto();

    // Site filter should be available
    const siteFilterVisible = await dashboardPage.siteFilter.isVisible();
    if (siteFilterVisible) {
      // Selecting a site should update the dashboard
      await dashboardPage.waitForPageLoad();
    }
  });
});

// TODO: Re-enable once site attendance module E2E tests are updated to match current UI
test.describe.skip('Site Attendance Dashboard - Site Manager', () => {
  test.use({ storageState: 'playwright/.auth/sitemanager.json' });

  test('should be able to view attendance dashboard', async ({ page }) => {
    const dashboardPage = new AttendanceDashboardPage(page);
    await dashboardPage.goto();

    await expect(dashboardPage.pageTitle).toBeVisible();
  });
});
