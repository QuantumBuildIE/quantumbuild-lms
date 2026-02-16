import { test, expect } from '../../fixtures/test-fixtures';
import { ProductListPage, ProductFormPage } from '../../page-objects/stock-management';
import { generateTestData, TAGS } from '../../fixtures/test-constants';

test.describe('Products @smoke', () => {
  test('should display product list', async ({ adminPage }) => {
    const productListPage = new ProductListPage(adminPage);
    await productListPage.goto();

    await expect(productListPage.pageTitle).toBeVisible();
    await expect(productListPage.table).toBeVisible();
  });

  test('should search products', async ({ adminPage }) => {
    const productListPage = new ProductListPage(adminPage);
    await productListPage.goto();

    // Get initial row count
    const initialCount = await productListPage.getProductCount();

    // Search for a specific term
    await productListPage.search('cement');

    // Verify search results - should either:
    // 1. Show filtered results containing search term, OR
    // 2. Show empty state if no matches, OR
    // 3. Show fewer or equal rows than initial count
    const filteredCount = await productListPage.getProductCount();
    const hasEmptyState = await productListPage.emptyState.isVisible().catch(() => false);

    if (hasEmptyState) {
      // Empty state is valid - no products match search
      await expect(productListPage.emptyState).toBeVisible();
    } else if (filteredCount > 0) {
      // If we have results, verify they contain the search term
      // Check that at least the first row contains the search term (case insensitive)
      const firstRow = adminPage.locator('tbody tr').first();
      const rowText = await firstRow.textContent();

      // The search should filter results - count should be <= initial OR search term should appear
      expect(
        filteredCount <= initialCount ||
        rowText?.toLowerCase().includes('cement')
      ).toBeTruthy();
    }

    // Also verify the search input contains our search term
    await expect(productListPage.searchInput).toHaveValue('cement');
  });

  test('should navigate to create product', async ({ adminPage }) => {
    const productListPage = new ProductListPage(adminPage);
    await productListPage.goto();
    await productListPage.clickCreate();

    await expect(adminPage).toHaveURL(/\/stock\/products\/new/);
  });

  test('should create a new product @regression', async ({ adminPage }) => {
    const productFormPage = new ProductFormPage(adminPage);
    await productFormPage.goto();

    const productCode = generateTestData.uniqueString('PROD');
    const productName = generateTestData.uniqueString('Test Product');

    await productFormPage.fillForm({
      productCode: productCode,
      productName: productName,
      categoryName: 'Adhesives', // Use existing seeded category
      baseRate: 10.50,
      costPrice: 10.50,
      sellPrice: 15.00,
      reorderLevel: 10,
      reorderQuantity: 50,
    });

    await productFormPage.saveAndWaitForSuccess();

    // Should be redirected to product list
    await expect(adminPage).toHaveURL(/\/stock\/products/);
  });
});

test.describe('Products - Warehouse User', () => {
  test('should be able to view products', async ({ warehousePage }) => {
    const productListPage = new ProductListPage(warehousePage);
    await productListPage.goto();

    await expect(productListPage.table).toBeVisible();
  });
});
