import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Attendance events list page object
 */
export class AttendanceEventsPage extends BasePage {
  readonly pageTitle: Locator;
  readonly employeeFilter: Locator;
  readonly siteFilter: Locator;
  readonly dateFilter: Locator;
  readonly eventTypeFilter: Locator;
  readonly table: Locator;
  readonly pagination: Locator;
  readonly exportButton: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.employeeFilter = page.locator('[data-filter="employee"], select[name="employeeId"]');
    this.siteFilter = page.locator('[data-filter="site"], select[name="siteId"]');
    this.dateFilter = page.locator('[data-filter="date"], input[name="date"]');
    this.eventTypeFilter = page.locator('[data-filter="eventType"], select[name="eventType"]');
    this.table = page.locator('table');
    this.pagination = page.locator('[data-testid="pagination"], nav[aria-label="pagination"]');
    this.exportButton = page.locator('button:has-text("Export"), button:has-text("Download")');
  }

  /**
   * Navigate to attendance events
   */
  async goto(): Promise<void> {
    await this.page.goto('/site-attendance/events');
    await this.waitForPageLoad();
  }

  /**
   * Filter by employee
   */
  async filterByEmployee(employee: string): Promise<void> {
    await this.selectOption(this.employeeFilter, employee);
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
   * Filter by date
   */
  async filterByDate(date: string): Promise<void> {
    await this.dateFilter.fill(date);
    await this.waitForPageLoad();
  }

  /**
   * Filter by event type
   */
  async filterByEventType(type: 'Enter' | 'Exit' | 'All'): Promise<void> {
    await this.selectOption(this.eventTypeFilter, type);
    await this.waitForPageLoad();
  }

  /**
   * Get event row by employee and timestamp
   */
  getEventRow(employeeName: string): Locator {
    return this.page.locator(`tr:has-text("${employeeName}")`);
  }

  /**
   * Get event count
   */
  async getEventCount(): Promise<number> {
    return await this.getTableRowCount();
  }

  /**
   * Export events
   */
  async export(): Promise<void> {
    await this.exportButton.click();
  }
}
