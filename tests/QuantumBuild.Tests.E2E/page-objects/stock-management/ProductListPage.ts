import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Product list page object
 */
export class ProductListPage extends BasePage {
  readonly pageTitle: Locator;
  readonly createButton: Locator;
  readonly searchInput: Locator;
  readonly categoryFilter: Locator;
  readonly supplierFilter: Locator;
  readonly table: Locator;
  readonly pagination: Locator;
  readonly emptyState: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    // UI uses "Add Product" text in a Button wrapped Link
    this.createButton = page.locator('a:has-text("Add Product"), a[href="/stock/products/new"]');
    // Search input has placeholder "Search products..."
    this.searchInput = page.locator('input[placeholder="Search products..."]');
    // Note: Category/Supplier filters don't exist on products list page currently
    this.categoryFilter = page.locator('[data-filter="category"], select[name="categoryId"]');
    this.supplierFilter = page.locator('[data-filter="supplier"], select[name="supplierId"]');
    this.table = page.locator('table');
    // DataTable renders pagination with Previous/Next buttons
    this.pagination = page.locator('div:has(button:has-text("Previous"))');
    this.emptyState = page.locator('td:has-text("No products found")');
  }

  /**
   * Navigate to products list
   */
  async goto(): Promise<void> {
    await this.page.goto('/stock/products');
    await this.waitForPageLoad();
  }

  /**
   * Click create new product
   */
  async clickCreate(): Promise<void> {
    await this.createButton.click();
    await this.page.waitForURL(/\/stock\/products\/new/);
  }

  /**
   * Search for products
   */
  async search(term: string): Promise<void> {
    await this.searchInput.fill(term);
    await this.page.waitForTimeout(500);
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
   * Get product row by name or SKU
   */
  getProductRow(nameOrSku: string): Locator {
    return this.page.locator(`tr:has-text("${nameOrSku}")`);
  }

  /**
   * Click on a product to view/edit
   */
  async clickProduct(nameOrSku: string): Promise<void> {
    await this.getProductRow(nameOrSku).click();
  }

  /**
   * Edit a product
   */
  async editProduct(nameOrSku: string): Promise<void> {
    const row = this.getProductRow(nameOrSku);
    await row.locator('a:has-text("Edit"), button:has-text("Edit")').click();
  }

  /**
   * Delete a product
   */
  async deleteProduct(nameOrSku: string): Promise<void> {
    const row = this.getProductRow(nameOrSku);
    await row.locator('button:has-text("Delete")').click();
    await this.confirmDialog();
  }

  /**
   * Get product count from table
   */
  async getProductCount(): Promise<number> {
    return await this.getTableRowCount();
  }

  /**
   * Go to next page
   */
  async nextPage(): Promise<void> {
    await this.pagination.locator('button:has-text("Next"), [data-action="next"]').click();
    await this.waitForPageLoad();
  }

  /**
   * Go to previous page
   */
  async previousPage(): Promise<void> {
    await this.pagination.locator('button:has-text("Previous"), [data-action="previous"]').click();
    await this.waitForPageLoad();
  }
}
