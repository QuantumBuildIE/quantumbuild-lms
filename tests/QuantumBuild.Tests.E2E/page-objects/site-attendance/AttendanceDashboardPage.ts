import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Site Attendance dashboard page object
 */
export class AttendanceDashboardPage extends BasePage {
  readonly pageTitle: Locator;
  readonly siteFilter: Locator;
  readonly dateRangeFilter: Locator;
  readonly utilizationKpi: Locator;
  readonly varianceKpi: Locator;
  readonly totalEmployeesKpi: Locator;
  readonly presentTodayKpi: Locator;
  readonly performanceTable: Locator;
  readonly refreshButton: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.siteFilter = page.locator('[data-filter="site"], select[name="siteId"]');
    this.dateRangeFilter = page.locator('[data-filter="dateRange"], select[name="dateRange"]');
    this.utilizationKpi = page.locator('[data-kpi="utilization"], [data-testid="utilization-kpi"]');
    this.varianceKpi = page.locator('[data-kpi="variance"], [data-testid="variance-kpi"]');
    this.totalEmployeesKpi = page.locator('[data-kpi="total-employees"], [data-testid="total-employees-kpi"]');
    this.presentTodayKpi = page.locator('[data-kpi="present-today"], [data-testid="present-today-kpi"]');
    this.performanceTable = page.locator('[data-testid="performance-table"], table');
    this.refreshButton = page.locator('button:has-text("Refresh")');
  }

  /**
   * Navigate to the attendance dashboard
   */
  async goto(): Promise<void> {
    await this.page.goto('/site-attendance');
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
  async filterByDateRange(range: string): Promise<void> {
    await this.selectOption(this.dateRangeFilter, range);
    await this.waitForPageLoad();
  }

  /**
   * Get utilization percentage
   */
  async getUtilization(): Promise<string> {
    return await this.utilizationKpi.textContent() || '0';
  }

  /**
   * Get variance hours
   */
  async getVariance(): Promise<string> {
    return await this.varianceKpi.textContent() || '0';
  }

  /**
   * Get total employees count
   */
  async getTotalEmployees(): Promise<string> {
    return await this.totalEmployeesKpi.textContent() || '0';
  }

  /**
   * Get present today count
   */
  async getPresentToday(): Promise<string> {
    return await this.presentTodayKpi.textContent() || '0';
  }

  /**
   * Get employee performance row
   */
  getEmployeeRow(name: string): Locator {
    return this.performanceTable.locator(`tr:has-text("${name}")`);
  }

  /**
   * Get employee status badge
   */
  getEmployeeStatus(name: string): Locator {
    return this.getEmployeeRow(name).locator('.badge, [data-status]');
  }

  /**
   * Check if employee is shown in performance table
   */
  async isEmployeeInTable(name: string): Promise<boolean> {
    return await this.getEmployeeRow(name).isVisible();
  }

  /**
   * Get employee count in performance table
   */
  async getEmployeeCount(): Promise<number> {
    return await this.performanceTable.locator('tbody tr').count();
  }

  /**
   * Refresh the dashboard data
   */
  async refresh(): Promise<void> {
    await this.refreshButton.click();
    await this.waitForPageLoad();
  }

  /**
   * Assert KPI value
   */
  async assertUtilization(expected: string | RegExp): Promise<void> {
    await expect(this.utilizationKpi).toContainText(expected);
  }
}
