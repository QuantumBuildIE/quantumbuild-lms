import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Site list page object
 */
export class SiteListPage extends BasePage {
  readonly pageTitle: Locator;
  readonly createButton: Locator;
  readonly searchInput: Locator;
  readonly table: Locator;
  readonly pagination: Locator;
  readonly emptyState: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.createButton = page.locator('a:has-text("New Site"), button:has-text("Create"), a[href*="/sites/new"]');
    this.searchInput = page.locator('input[placeholder*="Search"], input[name="search"]');
    this.table = page.locator('table');
    this.pagination = page.locator('[data-testid="pagination"], nav[aria-label="pagination"]');
    this.emptyState = page.locator('[data-testid="empty-state"], .empty-state');
  }

  /**
   * Navigate to sites list
   */
  async goto(): Promise<void> {
    await this.page.goto('/admin/sites');
    await this.waitForPageLoad();
  }

  /**
   * Click create new site
   */
  async clickCreate(): Promise<void> {
    await this.createButton.click();
    await this.page.waitForURL(/\/admin\/sites\/new/);
  }

  /**
   * Search sites
   */
  async search(term: string): Promise<void> {
    await this.searchInput.fill(term);
    await this.page.waitForTimeout(500);
    await this.waitForPageLoad();
  }

  /**
   * Get site row by name
   */
  getSiteRow(name: string): Locator {
    return this.page.locator(`tr:has-text("${name}")`);
  }

  /**
   * Edit a site
   */
  async editSite(name: string): Promise<void> {
    const row = this.getSiteRow(name);
    await row.locator('a:has-text("Edit"), button:has-text("Edit")').click();
  }

  /**
   * Delete a site
   */
  async deleteSite(name: string): Promise<void> {
    const row = this.getSiteRow(name);
    await row.locator('button:has-text("Delete")').click();
    await this.confirmDialog();
  }

  /**
   * Get site count
   */
  async getSiteCount(): Promise<number> {
    return await this.getTableRowCount();
  }
}
