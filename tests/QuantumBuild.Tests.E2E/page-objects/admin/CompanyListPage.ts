import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Company list page object
 */
export class CompanyListPage extends BasePage {
  readonly pageTitle: Locator;
  readonly createButton: Locator;
  readonly searchInput: Locator;
  readonly table: Locator;
  readonly pagination: Locator;
  readonly emptyState: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.createButton = page.locator('a:has-text("New Company"), button:has-text("Create"), a[href*="/companies/new"]');
    this.searchInput = page.locator('input[placeholder*="Search"], input[name="search"]');
    this.table = page.locator('table');
    this.pagination = page.locator('[data-testid="pagination"], nav[aria-label="pagination"]');
    this.emptyState = page.locator('[data-testid="empty-state"], .empty-state');
  }

  /**
   * Navigate to companies list
   */
  async goto(): Promise<void> {
    await this.page.goto('/admin/companies');
    await this.waitForPageLoad();
  }

  /**
   * Click create new company
   */
  async clickCreate(): Promise<void> {
    await this.createButton.click();
    await this.page.waitForURL(/\/admin\/companies\/new/);
  }

  /**
   * Search companies
   */
  async search(term: string): Promise<void> {
    await this.searchInput.fill(term);
    await this.page.waitForTimeout(500);
    await this.waitForPageLoad();
  }

  /**
   * Get company row by name
   */
  getCompanyRow(name: string): Locator {
    return this.page.locator(`tr:has-text("${name}")`);
  }

  /**
   * Click on a company to view details
   */
  async clickCompany(name: string): Promise<void> {
    await this.getCompanyRow(name).click();
  }

  /**
   * Edit a company
   */
  async editCompany(name: string): Promise<void> {
    const row = this.getCompanyRow(name);
    await row.locator('a:has-text("Edit"), button:has-text("Edit")').click();
  }

  /**
   * Delete a company
   */
  async deleteCompany(name: string): Promise<void> {
    const row = this.getCompanyRow(name);
    await row.locator('button:has-text("Delete")').click();
    await this.confirmDialog();
  }

  /**
   * Get company count
   */
  async getCompanyCount(): Promise<number> {
    return await this.getTableRowCount();
  }
}
