import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Attendance summaries page object
 */
export class AttendanceSummariesPage extends BasePage {
  readonly pageTitle: Locator;
  readonly employeeFilter: Locator;
  readonly siteFilter: Locator;
  readonly startDateFilter: Locator;
  readonly endDateFilter: Locator;
  readonly statusFilter: Locator;
  readonly table: Locator;
  readonly pagination: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.employeeFilter = page.locator('[data-filter="employee"], select[name="employeeId"]');
    this.siteFilter = page.locator('[data-filter="site"], select[name="siteId"]');
    this.startDateFilter = page.locator('[data-filter="startDate"], input[name="startDate"]');
    this.endDateFilter = page.locator('[data-filter="endDate"], input[name="endDate"]');
    this.statusFilter = page.locator('[data-filter="status"], select[name="status"]');
    this.table = page.locator('table');
    this.pagination = page.locator('[data-testid="pagination"], nav[aria-label="pagination"]');
  }

  /**
   * Navigate to attendance summaries
   */
  async goto(): Promise<void> {
    await this.page.goto('/site-attendance/summaries');
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
   * Filter by date range
   */
  async filterByDateRange(startDate: string, endDate: string): Promise<void> {
    await this.startDateFilter.fill(startDate);
    await this.endDateFilter.fill(endDate);
    await this.waitForPageLoad();
  }

  /**
   * Filter by status
   */
  async filterByStatus(status: string): Promise<void> {
    await this.selectOption(this.statusFilter, status);
    await this.waitForPageLoad();
  }

  /**
   * Get summary row
   */
  getSummaryRow(employeeName: string, date: string): Locator {
    return this.page.locator(`tr:has-text("${employeeName}"):has-text("${date}")`);
  }

  /**
   * Get summary count
   */
  async getSummaryCount(): Promise<number> {
    return await this.getTableRowCount();
  }

  /**
   * Get employee's time on site for a specific date
   */
  async getTimeOnSite(employeeName: string, date: string): Promise<string> {
    const row = this.getSummaryRow(employeeName, date);
    return await row.locator('[data-time-on-site], td:nth-child(4)').textContent() || '0';
  }

  /**
   * Get employee's utilization for a specific date
   */
  async getUtilization(employeeName: string, date: string): Promise<string> {
    const row = this.getSummaryRow(employeeName, date);
    return await row.locator('[data-utilization], td:nth-child(5)').textContent() || '0';
  }

  /**
   * Get employee's status badge for a specific date
   */
  getStatusBadge(employeeName: string, date: string): Locator {
    return this.getSummaryRow(employeeName, date).locator('.badge, [data-status]');
  }
}
