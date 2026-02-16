import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Stock levels page object
 */
export class StockLevelsPage extends BasePage {
  readonly pageTitle: Locator;
  readonly searchInput: Locator;
  readonly locationFilter: Locator;
  readonly categoryFilter: Locator;
  readonly lowStockOnlyCheckbox: Locator;
  readonly table: Locator;
  readonly pagination: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.searchInput = page.locator('input[placeholder*="Search"], input[name="search"]');
    this.locationFilter = page.locator('[data-filter="location"], select[name="stockLocationId"]');
    this.categoryFilter = page.locator('[data-filter="category"], select[name="categoryId"]');
    this.lowStockOnlyCheckbox = page.locator('[name="lowStockOnly"], #lowStockOnly');
    this.table = page.locator('table');
    this.pagination = page.locator('[data-testid="pagination"], nav[aria-label="pagination"]');
  }

  /**
   * Navigate to stock levels
   */
  async goto(): Promise<void> {
    await this.page.goto('/stock/levels');
    await this.waitForPageLoad();
  }

  /**
   * Filter by location
   */
  async filterByLocation(location: string): Promise<void> {
    await this.selectOption(this.locationFilter, location);
    await this.waitForPageLoad();
  }

  /**
   * Filter by category
   */
  async filterByCategory(category: string): Promise<void> {
    await this.selectOption(this.categoryFilter, category);
    await this.waitForPageLoad();
  }

  /**
   * Show only low stock items
   */
  async showLowStockOnly(show: boolean): Promise<void> {
    if (show) {
      await this.lowStockOnlyCheckbox.check();
    } else {
      await this.lowStockOnlyCheckbox.uncheck();
    }
    await this.waitForPageLoad();
  }

  /**
   * Search products
   */
  async search(term: string): Promise<void> {
    await this.searchInput.fill(term);
    await this.page.waitForTimeout(500);
    await this.waitForPageLoad();
  }

  /**
   * Get stock level row for a product
   */
  getProductRow(productName: string): Locator {
    return this.page.locator(`tr:has-text("${productName}")`);
  }

  /**
   * Get quantity on hand for a product
   */
  async getQuantityOnHand(productName: string): Promise<string> {
    const row = this.getProductRow(productName);
    return await row.locator('[data-quantity-on-hand], td:nth-child(3)').textContent() || '0';
  }

  /**
   * Check if product is low stock (highlighted)
   */
  async isLowStock(productName: string): Promise<boolean> {
    const row = this.getProductRow(productName);
    const classes = await row.getAttribute('class');
    return classes?.includes('low-stock') || classes?.includes('warning') || false;
  }

  /**
   * Get count of low stock items
   */
  async getLowStockCount(): Promise<number> {
    return await this.page.locator('tr.low-stock, tr.warning, tr[data-low-stock="true"]').count();
  }
}
