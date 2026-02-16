import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Stock order list page object
 */
export class StockOrderListPage extends BasePage {
  readonly pageTitle: Locator;
  readonly createButton: Locator;
  readonly searchInput: Locator;
  readonly statusFilter: Locator;
  readonly siteFilter: Locator;
  readonly table: Locator;
  readonly pagination: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    // UI uses "New Stock Order" text in a Button wrapped Link
    this.createButton = page.locator('a:has-text("New Stock Order"), a[href="/stock/orders/new"]');
    // Search placeholder is "Search by order # or site..."
    this.searchInput = page.locator('input[placeholder="Search by order # or site..."]');
    // Status filter uses Tabs component (TabsTrigger elements)
    this.statusFilter = page.locator('[role="tablist"]');
    // Site filter doesn't exist on orders list page currently
    this.siteFilter = page.locator('[data-filter="site"], select[name="siteId"]');
    this.table = page.locator('table');
    // DataTable renders pagination with Previous/Next buttons
    this.pagination = page.locator('div:has(button:has-text("Previous"))');
  }

  /**
   * Navigate to stock orders list
   */
  async goto(): Promise<void> {
    await this.page.goto('/stock/orders');
    await this.waitForPageLoad();
  }

  /**
   * Click create new order
   */
  async clickCreate(): Promise<void> {
    await this.createButton.click();
    await this.page.waitForURL(/\/stock\/orders\/new/);
  }

  /**
   * Filter by status using tab buttons
   */
  async filterByStatus(status: string): Promise<void> {
    // Status filter uses Tabs - click the tab trigger with matching text
    const tabTrigger = this.page.locator(`[role="tab"]:has-text("${status}")`);
    await tabTrigger.click();
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
   * Get order row by reference
   */
  getOrderRow(reference: string): Locator {
    return this.page.locator(`tr:has-text("${reference}")`);
  }

  /**
   * Click on an order to view details
   */
  async clickOrder(reference: string): Promise<void> {
    await this.getOrderRow(reference).click();
  }

  /**
   * Get order status badge
   */
  getOrderStatus(reference: string): Locator {
    return this.getOrderRow(reference).locator('.badge, [data-status]');
  }

  /**
   * Edit an order
   */
  async editOrder(reference: string): Promise<void> {
    const row = this.getOrderRow(reference);
    await row.locator('a:has-text("Edit"), button:has-text("Edit")').click();
  }

  /**
   * Get order count
   */
  async getOrderCount(): Promise<number> {
    return await this.getTableRowCount();
  }
}
