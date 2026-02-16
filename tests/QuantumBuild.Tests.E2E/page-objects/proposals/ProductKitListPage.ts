import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Product kit list page object
 */
export class ProductKitListPage extends BasePage {
  readonly pageTitle: Locator;
  readonly createButton: Locator;
  readonly searchInput: Locator;
  readonly table: Locator;
  readonly emptyState: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.createButton = page.locator('a:has-text("New Kit"), button:has-text("Create"), a[href*="/kits/new"]');
    this.searchInput = page.locator('input[placeholder*="Search"], input[name="search"]');
    this.table = page.locator('table');
    this.emptyState = page.locator('[data-testid="empty-state"], .empty-state');
  }

  /**
   * Navigate to product kits list
   */
  async goto(): Promise<void> {
    await this.page.goto('/proposals/kits');
    await this.waitForPageLoad();
  }

  /**
   * Click create new kit
   */
  async clickCreate(): Promise<void> {
    await this.createButton.click();
    await this.page.waitForURL(/\/proposals\/kits\/new/);
  }

  /**
   * Search kits
   */
  async search(term: string): Promise<void> {
    await this.searchInput.fill(term);
    await this.page.waitForTimeout(500);
    await this.waitForPageLoad();
  }

  /**
   * Get kit row by name
   */
  getKitRow(name: string): Locator {
    return this.page.locator(`tr:has-text("${name}")`);
  }

  /**
   * Edit a kit
   */
  async editKit(name: string): Promise<void> {
    const row = this.getKitRow(name);
    await row.locator('a:has-text("Edit"), button:has-text("Edit")').click();
  }

  /**
   * Delete a kit
   */
  async deleteKit(name: string): Promise<void> {
    const row = this.getKitRow(name);
    await row.locator('button:has-text("Delete")').click();
    await this.confirmDialog();
  }

  /**
   * Get kit count
   */
  async getKitCount(): Promise<number> {
    return await this.getTableRowCount();
  }
}
