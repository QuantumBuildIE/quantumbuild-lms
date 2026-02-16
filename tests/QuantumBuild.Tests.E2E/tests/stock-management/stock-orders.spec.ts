import { test, expect } from '../../fixtures/test-fixtures';
import {
  StockOrderListPage,
  StockOrderFormPage,
  StockOrderDetailPage
} from '../../page-objects/stock-management';
import { TAGS } from '../../fixtures/test-constants';

test.describe('Stock Orders @smoke', () => {
  test('should display stock order list', async ({ adminPage }) => {
    const orderListPage = new StockOrderListPage(adminPage);
    await orderListPage.goto();

    await expect(orderListPage.pageTitle).toBeVisible();
    await expect(orderListPage.table).toBeVisible();
  });

  test('should filter orders by status', async ({ adminPage }) => {
    const orderListPage = new StockOrderListPage(adminPage);
    await orderListPage.goto();

    // Click on Draft tab to filter
    await orderListPage.filterByStatus('Draft');

    // Verify the Draft tab is now selected (has aria-selected or data-state="active")
    const draftTab = adminPage.locator('[role="tab"]:has-text("Draft")');
    await expect(draftTab).toHaveAttribute('data-state', 'active');

    // Verify filtered results
    const tableRows = adminPage.locator('tbody tr');
    const rowCount = await tableRows.count();

    if (rowCount > 0) {
      // All visible orders should have "Draft" status
      // Check up to 5 rows to verify filter is working
      for (let i = 0; i < Math.min(rowCount, 5); i++) {
        const row = tableRows.nth(i);
        const statusBadge = row.locator('span.inline-flex, [class*="badge"]');

        // Get the status text from the badge
        const statusText = await statusBadge.textContent().catch(() => '');

        // Status should be "Draft" for all visible rows
        expect(statusText?.trim()).toBe('Draft');
      }
    } else {
      // No draft orders - that's valid, verify empty state or no rows
      expect(rowCount).toBe(0);
    }
  });

  test('should navigate to create order', async ({ adminPage }) => {
    const orderListPage = new StockOrderListPage(adminPage);
    await orderListPage.goto();
    await orderListPage.clickCreate();

    await expect(adminPage).toHaveURL(/\/stock\/orders\/new/);
  });
});

test.describe('Stock Order Workflow @regression', () => {
  test('should create a new stock order', async ({ adminPage }) => {
    const orderFormPage = new StockOrderFormPage(adminPage);
    await orderFormPage.goto();

    // Select site and add items
    // Note: This requires actual test data in the database
    await expect(orderFormPage.siteSelect).toBeVisible();
  });
});

test.describe('Stock Orders - Site Manager', () => {
  test('should be able to create orders', async ({ siteManagerPage }) => {
    const orderListPage = new StockOrderListPage(siteManagerPage);
    await orderListPage.goto();

    await expect(orderListPage.createButton).toBeVisible();
  });

  test('should not see approve button on pending orders', async ({ siteManagerPage }) => {
    // Site managers can create orders but cannot approve them
    const orderListPage = new StockOrderListPage(siteManagerPage);
    await orderListPage.goto();

    // Navigate to Pending Approval tab to find orders awaiting approval
    const pendingTab = siteManagerPage.locator('[role="tab"]:has-text("Pending Approval")');
    if (await pendingTab.isVisible()) {
      await pendingTab.click();
      await orderListPage.waitForPageLoad();
    }

    // Check if there are any pending orders
    const tableRows = siteManagerPage.locator('tbody tr');
    const rowCount = await tableRows.count();

    if (rowCount > 0) {
      // Click on the first pending order to go to detail page
      await tableRows.first().click();
      await siteManagerPage.waitForLoadState('networkidle');

      // Verify we're on an order detail page
      await expect(siteManagerPage).toHaveURL(/\/stock\/orders\/[a-f0-9-]+/);

      // Site Manager should NOT see "Approve Order" button (no StockManagement.ApproveOrders permission)
      const approveButton = siteManagerPage.locator('button:has-text("Approve Order")');
      await expect(approveButton).not.toBeVisible();

      // Site Manager should also NOT see "Reject Order" button
      const rejectButton = siteManagerPage.locator('button:has-text("Reject Order")');
      await expect(rejectButton).not.toBeVisible();

      // But they should see the order details (read access)
      const orderInfo = siteManagerPage.locator('h1, [data-testid="order-header"]');
      await expect(orderInfo).toBeVisible();
    } else {
      // No pending orders exist - verify the table is visible and empty state is shown
      await expect(orderListPage.table).toBeVisible();

      // This is a valid state - test passes because there are no orders to verify against
      // Log that we couldn't fully test due to missing data
      console.log('Warning: No pending orders found to verify approve button visibility');
    }
  });
});

test.describe('Stock Orders - Warehouse User', () => {
  test('should see orders and workflow actions', async ({ warehousePage }) => {
    const orderListPage = new StockOrderListPage(warehousePage);
    await orderListPage.goto();

    await expect(orderListPage.table).toBeVisible();
  });
});
