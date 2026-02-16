import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Stock order detail page object
 */
export class StockOrderDetailPage extends BasePage {
  readonly pageTitle: Locator;
  readonly orderReference: Locator;
  readonly statusBadge: Locator;
  readonly siteName: Locator;
  readonly requestedBy: Locator;
  readonly requiredByDate: Locator;
  readonly lineItemsTable: Locator;
  readonly editButton: Locator;
  readonly submitButton: Locator;
  readonly approveButton: Locator;
  readonly rejectButton: Locator;
  readonly cancelButton: Locator;
  readonly readyButton: Locator;
  readonly collectButton: Locator;
  readonly printButton: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    this.orderReference = page.locator('[data-testid="order-reference"], .order-reference');
    this.statusBadge = page.locator('[data-testid="status-badge"], .badge');
    this.siteName = page.locator('[data-testid="site-name"], .site-name');
    this.requestedBy = page.locator('[data-testid="requested-by"], .requested-by');
    this.requiredByDate = page.locator('[data-testid="required-by"], .required-by');
    this.lineItemsTable = page.locator('table, [data-testid="line-items"]');
    this.editButton = page.locator('button:has-text("Edit"), a:has-text("Edit")');
    this.submitButton = page.locator('button:has-text("Submit"), button:has-text("Submit for Approval")');
    this.approveButton = page.locator('button:has-text("Approve")');
    this.rejectButton = page.locator('button:has-text("Reject")');
    this.cancelButton = page.locator('button:has-text("Cancel Order")');
    this.readyButton = page.locator('button:has-text("Ready"), button:has-text("Mark Ready")');
    this.collectButton = page.locator('button:has-text("Collect"), button:has-text("Mark Collected")');
    this.printButton = page.locator('button:has-text("Print"), a:has-text("Print")');
  }

  /**
   * Navigate to order detail page
   */
  async goto(id: string): Promise<void> {
    await this.page.goto(`/stock/orders/${id}`);
    await this.waitForPageLoad();
  }

  /**
   * Get the current order status
   */
  async getStatus(): Promise<string> {
    return await this.statusBadge.textContent() || '';
  }

  /**
   * Submit the order for approval
   */
  async submit(): Promise<void> {
    await this.submitButton.click();
    await this.waitForToastSuccess();
  }

  /**
   * Approve the order
   */
  async approve(): Promise<void> {
    await this.approveButton.click();
    await this.waitForToastSuccess();
  }

  /**
   * Reject the order
   */
  async reject(reason?: string): Promise<void> {
    await this.rejectButton.click();
    if (reason) {
      const reasonInput = this.page.locator('[name="rejectionReason"], #rejectionReason, textarea');
      if (await reasonInput.isVisible()) {
        await reasonInput.fill(reason);
      }
    }
    await this.confirmDialog();
    await this.waitForToastSuccess();
  }

  /**
   * Cancel the order
   */
  async cancel(reason?: string): Promise<void> {
    await this.cancelButton.click();
    if (reason) {
      const reasonInput = this.page.locator('[name="cancellationReason"], #cancellationReason, textarea');
      if (await reasonInput.isVisible()) {
        await reasonInput.fill(reason);
      }
    }
    await this.confirmDialog();
    await this.waitForToastSuccess();
  }

  /**
   * Mark order as ready for collection
   */
  async markReady(): Promise<void> {
    await this.readyButton.click();
    await this.waitForToastSuccess();
  }

  /**
   * Mark order as collected
   */
  async markCollected(): Promise<void> {
    await this.collectButton.click();
    await this.waitForToastSuccess();
  }

  /**
   * Print the order docket
   */
  async print(): Promise<void> {
    await this.printButton.click();
    await this.page.waitForURL(/\/print/);
  }

  /**
   * Edit the order
   */
  async edit(): Promise<void> {
    await this.editButton.click();
    await this.page.waitForURL(/\/edit/);
  }

  /**
   * Get line items count
   */
  async getLineItemsCount(): Promise<number> {
    return await this.lineItemsTable.locator('tbody tr').count();
  }

  /**
   * Assert the order status
   */
  async assertStatus(status: string): Promise<void> {
    await expect(this.statusBadge).toContainText(status);
  }

  /**
   * Check if action button is visible
   */
  async isActionVisible(action: 'submit' | 'approve' | 'reject' | 'cancel' | 'ready' | 'collect'): Promise<boolean> {
    const buttonMap = {
      submit: this.submitButton,
      approve: this.approveButton,
      reject: this.rejectButton,
      cancel: this.cancelButton,
      ready: this.readyButton,
      collect: this.collectButton,
    };
    return await buttonMap[action].isVisible();
  }
}
