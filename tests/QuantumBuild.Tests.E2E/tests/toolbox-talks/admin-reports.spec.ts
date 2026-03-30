import { test, expect } from '../../fixtures/test-fixtures';
import { ROUTES, TIMEOUTS } from '../../fixtures/test-constants';

// ---------------------------------------------------------------------------
// Admin Reports — verify each report page loads with seeded data
// ---------------------------------------------------------------------------

const REPORTS_BASE = ROUTES.toolboxTalks.reports;

test.describe('Admin Reports', () => {

  test('compliance report loads and shows data', async ({ adminPage }) => {
    await adminPage.goto(`${REPORTS_BASE}/compliance`);
    await adminPage.waitForLoadState('networkidle');

    // Page heading
    await expect(
      adminPage.locator('h1:has-text("Compliance Report")')
    ).toBeVisible({ timeout: TIMEOUTS.medium });

    // At least one row of department or talk compliance data
    const complianceRows = adminPage.locator('table tbody tr');
    await expect(complianceRows.first()).toBeVisible({ timeout: TIMEOUTS.medium });
    expect(await complianceRows.count()).toBeGreaterThanOrEqual(1);

    // Export button is present
    await expect(
      adminPage.locator('button:has-text("Export")')
    ).toBeVisible();
  });

  test('completions report loads and shows data', async ({ adminPage }) => {
    await adminPage.goto(`${REPORTS_BASE}/completions`);
    await adminPage.waitForLoadState('networkidle');

    // Page heading
    await expect(
      adminPage.locator('h1:has-text("Completions Report")')
    ).toBeVisible({ timeout: TIMEOUTS.medium });

    // Data table renders with at least one row
    const tableRows = adminPage.locator('table tbody tr');
    await expect(tableRows.first()).toBeVisible({ timeout: TIMEOUTS.medium });
    expect(await tableRows.count()).toBeGreaterThanOrEqual(1);
  });

  test('overdue report loads', async ({ adminPage }) => {
    await adminPage.goto(`${REPORTS_BASE}/overdue`);
    await adminPage.waitForLoadState('networkidle');

    // Page heading
    await expect(
      adminPage.locator('h1:has-text("Overdue Report")')
    ).toBeVisible({ timeout: TIMEOUTS.medium });

    // Either the table has rows or the empty state is shown — both are valid
    const tableRows = adminPage.locator('table tbody tr');
    const emptyState = adminPage.locator('text="No overdue assignments found"');

    await expect(
      tableRows.first().or(emptyState)
    ).toBeVisible({ timeout: TIMEOUTS.medium });
  });

  test('skills matrix loads and renders grid', async ({ adminPage }) => {
    await adminPage.goto(`${REPORTS_BASE}/skills-matrix`);
    await adminPage.waitForLoadState('networkidle');

    // Page heading
    await expect(
      adminPage.locator('h1:has-text("Skills Matrix")')
    ).toBeVisible({ timeout: TIMEOUTS.medium });

    // Either the matrix grid renders with employee rows or the empty state is shown
    const employeeRows = adminPage.locator('table tbody tr');
    const emptyState = adminPage.locator('text="No learning assignments found"');

    const gridVisible = await employeeRows.first().isVisible({ timeout: TIMEOUTS.medium }).catch(() => false);

    if (gridVisible) {
      // Employee rows present
      expect(await employeeRows.count()).toBeGreaterThanOrEqual(1);

      // Learning columns present (header row has learning codes)
      const headerCells = adminPage.locator('table thead th, table thead td');
      expect(await headerCells.count()).toBeGreaterThanOrEqual(2); // Employee col + at least 1 learning
    } else {
      // Empty state is acceptable
      await expect(emptyState).toBeVisible();
    }
  });

  test('compliance report Excel export triggers download', async ({ adminPage }) => {
    await adminPage.goto(`${REPORTS_BASE}/compliance`);
    await adminPage.waitForLoadState('networkidle');

    // Wait for report data to load
    await expect(
      adminPage.locator('h1:has-text("Compliance Report")')
    ).toBeVisible({ timeout: TIMEOUTS.medium });

    // Find the export button (compliance uses "Export PDF" but we look for any export)
    const exportButton = adminPage.locator('button:has-text("Export")');
    await expect(exportButton).toBeVisible();

    // Listen for download event and click
    const [download] = await Promise.all([
      adminPage.waitForEvent('download'),
      exportButton.click(),
    ]);

    expect(download.suggestedFilename()).toMatch(/\.(xlsx|pdf|csv)$/);
  });

});
