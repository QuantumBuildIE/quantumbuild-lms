import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Employee list page object
 */
export class EmployeeListPage extends BasePage {
  readonly pageTitle: Locator;
  readonly createButton: Locator;
  readonly searchInput: Locator;
  readonly siteFilter: Locator;
  readonly table: Locator;
  readonly pagination: Locator;
  readonly emptyState: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.createButton = page.locator('a:has-text("New Employee"), button:has-text("Create"), a[href*="/employees/new"]');
    this.searchInput = page.locator('input[placeholder*="Search"], input[name="search"]');
    this.siteFilter = page.locator('[data-filter="site"], select[name="siteId"]');
    this.table = page.locator('table');
    this.pagination = page.locator('[data-testid="pagination"], nav[aria-label="pagination"]');
    this.emptyState = page.locator('[data-testid="empty-state"], .empty-state');
  }

  /**
   * Navigate to employees list
   */
  async goto(): Promise<void> {
    await this.page.goto('/admin/employees');
    await this.waitForPageLoad();
  }

  /**
   * Click create new employee
   */
  async clickCreate(): Promise<void> {
    await this.createButton.click();
    await this.page.waitForURL(/\/admin\/employees\/new/);
  }

  /**
   * Search employees
   */
  async search(term: string): Promise<void> {
    await this.searchInput.fill(term);
    await this.page.waitForTimeout(500);
    await this.waitForPageLoad();
  }

  /**
   * Filter by site
   */
  async filterBySite(site: string): Promise<void> {
    await this.selectOption(this.siteFilter, site);
    await this.waitForPageLoad();
  }

  /**
   * Get employee row by name
   */
  getEmployeeRow(name: string): Locator {
    return this.page.locator(`tr:has-text("${name}")`);
  }

  /**
   * Edit an employee
   */
  async editEmployee(name: string): Promise<void> {
    const row = this.getEmployeeRow(name);
    await row.locator('a:has-text("Edit"), button:has-text("Edit")').click();
  }

  /**
   * Delete an employee
   */
  async deleteEmployee(name: string): Promise<void> {
    const row = this.getEmployeeRow(name);
    await row.locator('button:has-text("Delete")').click();
    await this.confirmDialog();
  }

  /**
   * Get employee count
   */
  async getEmployeeCount(): Promise<number> {
    return await this.getTableRowCount();
  }
}
