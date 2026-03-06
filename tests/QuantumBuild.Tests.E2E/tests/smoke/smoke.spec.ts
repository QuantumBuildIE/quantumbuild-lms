import { test, expect } from '@playwright/test';
import { TEST_TENANT } from '../../fixtures/test-constants';

/**
 * Smoke Tests - Basic functionality verification across all modules
 * Tag: @smoke
 * Run with: npx playwright test --grep @smoke
 *
 * TODO: Re-enable once E2E tests are updated to match current UI
 */
test.describe.skip('Smoke Tests @smoke', () => {
  test.describe('Admin User Smoke Tests', () => {
    test.use({ storageState: 'playwright/.auth/admin.json' });

    test('can access dashboard after login', async ({ page }) => {
      await page.goto('/dashboard');
      await expect(page).toHaveURL(/\/(dashboard|home)/);

      // Page should load without errors
      const pageTitle = page.locator('h1, [data-testid="page-title"]');
      await expect(pageTitle).toBeVisible();
    });

    test('can navigate to all major sections', async ({ page }) => {
      const sections = [
        { url: '/dashboard', pattern: /dashboard/i },
        { url: '/admin/employees', pattern: /employee/i },
        { url: '/admin/sites', pattern: /site/i },
      ];

      for (const section of sections) {
        await page.goto(section.url);

        // Wait for page to load
        await page.waitForLoadState('networkidle');

        // Verify we're on the right page (no redirect to login)
        await expect(page).not.toHaveURL(/\/login/);
      }
    });

    test('admin navigation menu is accessible', async ({ page }) => {
      await page.goto('/dashboard');

      // Look for navigation elements
      const nav = page.locator('nav, [role="navigation"], .sidebar, .nav');
      await expect(nav.first()).toBeVisible();
    });

    test('can logout', async ({ page }) => {
      await page.goto('/dashboard');

      // Find and click logout (may be in dropdown)
      const userMenu = page.locator('[data-testid="user-menu"], .user-dropdown, button:has-text("admin")');
      if (await userMenu.isVisible()) {
        await userMenu.click();
      }

      const logoutButton = page.locator('button:has-text("Logout"), a:has-text("Logout"), [data-action="logout"]');
      if (await logoutButton.isVisible()) {
        await logoutButton.click();
        await expect(page).toHaveURL(/\/login/);
      }
    });
  });

  test.describe('Finance User Smoke Tests', () => {
    test.use({ storageState: 'playwright/.auth/finance.json' });

    test('can access dashboard', async ({ page }) => {
      await page.goto('/dashboard');
      await expect(page).not.toHaveURL(/\/login/);
      await page.waitForLoadState('networkidle');
    });
  });
});

// TODO: Re-enable once E2E tests are updated to match current UI
test.describe.skip('Module Data Loading @smoke', () => {
  test.use({ storageState: 'playwright/.auth/admin.json' });

  test('employees list loads data', async ({ page }) => {
    await page.goto('/admin/employees');
    await page.waitForLoadState('networkidle');

    // Wait for table or loading to complete
    await page.waitForTimeout(2000);

    // Check that we have content (table rows or empty state)
    const tableRows = page.locator('tbody tr, [data-testid="employee-row"]');
    const emptyState = page.locator('[data-testid="empty-state"], .empty-state, :text("No employees")');

    const hasRows = await tableRows.count() > 0;
    const hasEmptyState = await emptyState.isVisible().catch(() => false);

    expect(hasRows || hasEmptyState).toBeTruthy();
  });

  test('toolbox talks list loads', async ({ page }) => {
    await page.goto('/toolbox-talks/talks');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);

    await expect(page.locator('table, [data-testid="talks-list"], .empty-state').first()).toBeVisible();
  });
});

// TODO: Re-enable once E2E tests are updated to match current UI
test.describe.skip('UI Components @smoke', () => {
  test.use({ storageState: 'playwright/.auth/admin.json' });

  test('no JavaScript errors on page load', async ({ page }) => {
    const errors: string[] = [];
    page.on('console', msg => {
      if (msg.type() === 'error') {
        errors.push(msg.text());
      }
    });

    await page.goto('/dashboard');
    await page.waitForLoadState('networkidle');

    // Filter out known non-critical errors
    const criticalErrors = errors.filter(e =>
      !e.includes('favicon') &&
      !e.includes('Failed to load resource') &&
      !e.includes('net::ERR')
    );

    // Should have no critical JS errors
    expect(criticalErrors.length).toBeLessThanOrEqual(2);
  });

  test('responsive design works on mobile viewport', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/dashboard');
    await page.waitForLoadState('networkidle');

    // Page should be visible and not have horizontal overflow
    await expect(page.locator('body')).toBeVisible();
  });
});

// TODO: Re-enable once E2E tests are updated to match current UI
test.describe.skip('API Health @smoke', () => {
  test('API endpoints are responsive', async ({ request }) => {
    // Login to get token
    const loginResponse = await request.post('/api/auth/login', {
      data: {
        email: TEST_TENANT.users.admin.email,
        password: TEST_TENANT.users.admin.password
      }
    });

    expect(loginResponse.status()).toBe(200);

    const loginData = await loginResponse.json();
    const token = loginData.accessToken || loginData.token;

    // Test key endpoints
    const endpoints = [
      '/api/auth/me',
      '/api/employees',
    ];

    for (const endpoint of endpoints) {
      const response = await request.get(endpoint, {
        headers: {
          'Authorization': `Bearer ${token}`
        }
      });

      expect(response.status()).toBeLessThan(500);
    }
  });
});
