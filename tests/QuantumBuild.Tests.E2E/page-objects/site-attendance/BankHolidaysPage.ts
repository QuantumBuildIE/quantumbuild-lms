import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Bank holidays management page object
 */
export class BankHolidaysPage extends BasePage {
  readonly pageTitle: Locator;
  readonly createButton: Locator;
  readonly yearFilter: Locator;
  readonly table: Locator;
  readonly emptyState: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.createButton = page.locator('button:has-text("Add Holiday"), button:has-text("Create")');
    this.yearFilter = page.locator('[data-filter="year"], select[name="year"]');
    this.table = page.locator('table');
    this.emptyState = page.locator('[data-testid="empty-state"], .empty-state');
  }

  /**
   * Navigate to bank holidays page
   */
  async goto(): Promise<void> {
    await this.page.goto('/site-attendance/bank-holidays');
    await this.waitForPageLoad();
  }

  /**
   * Click create new holiday
   */
  async clickCreate(): Promise<void> {
    await this.createButton.click();
    await this.waitForDialogToOpen();
  }

  /**
   * Filter by year
   */
  async filterByYear(year: string): Promise<void> {
    await this.selectOption(this.yearFilter, year);
    await this.waitForPageLoad();
  }

  /**
   * Get holiday row by name
   */
  getHolidayRow(name: string): Locator {
    return this.page.locator(`tr:has-text("${name}")`);
  }

  /**
   * Create a new bank holiday
   */
  async createHoliday(data: { date: string; name: string }): Promise<void> {
    await this.clickCreate();

    await this.page.locator('[name="date"], #date').fill(data.date);
    await this.page.locator('[name="name"], #name').fill(data.name);

    await this.page.locator('button:has-text("Save"), button[type="submit"]').click();
    await this.waitForToastSuccess();
  }

  /**
   * Edit a holiday
   */
  async editHoliday(name: string, newData: { date?: string; name?: string }): Promise<void> {
    const row = this.getHolidayRow(name);
    await row.locator('button:has-text("Edit")').click();
    await this.waitForDialogToOpen();

    if (newData.date) {
      await this.page.locator('[name="date"], #date').fill(newData.date);
    }
    if (newData.name) {
      await this.page.locator('[name="name"], #name').fill(newData.name);
    }

    await this.page.locator('button:has-text("Save"), button[type="submit"]').click();
    await this.waitForToastSuccess();
  }

  /**
   * Delete a holiday
   */
  async deleteHoliday(name: string): Promise<void> {
    const row = this.getHolidayRow(name);
    await row.locator('button:has-text("Delete")').click();
    await this.confirmDialog();
    await this.waitForToastSuccess();
  }

  /**
   * Get holiday count
   */
  async getHolidayCount(): Promise<number> {
    return await this.getTableRowCount();
  }
}
