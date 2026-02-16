import { Page, Locator, expect } from '@playwright/test';
import { TIMEOUTS } from '../fixtures/test-constants';

/**
 * Base page object with common functionality shared across all pages
 */
export abstract class BasePage {
  constructor(protected page: Page) {}

  /**
   * Toast notification locators
   */
  protected get toastSuccess(): Locator {
    return this.page.locator('[data-sonner-toast][data-type="success"], .toast-success, [role="status"]');
  }

  protected get toastError(): Locator {
    return this.page.locator('[data-sonner-toast][data-type="error"], .toast-error, [role="alert"]');
  }

  protected get toastWarning(): Locator {
    return this.page.locator('[data-sonner-toast][data-type="warning"], .toast-warning');
  }

  /**
   * Loading state locators
   */
  protected get loadingSpinner(): Locator {
    return this.page.locator('.loading, .spinner, [data-loading="true"], .animate-spin');
  }

  protected get skeleton(): Locator {
    return this.page.locator('.skeleton, [data-skeleton="true"]');
  }

  /**
   * Dialog/Modal locators
   */
  protected get dialog(): Locator {
    return this.page.locator('[role="dialog"], .dialog, .modal');
  }

  protected get dialogConfirmButton(): Locator {
    return this.dialog.locator('button:has-text("Confirm"), button:has-text("Yes"), button:has-text("Delete")');
  }

  protected get dialogCancelButton(): Locator {
    return this.dialog.locator('button:has-text("Cancel"), button:has-text("No")');
  }

  /**
   * Navigation helpers
   */
  async waitForPageLoad(): Promise<void> {
    await this.page.waitForLoadState('networkidle');
  }

  async waitForNavigation(): Promise<void> {
    await this.page.waitForLoadState('load');
  }

  async waitForLoadingToComplete(): Promise<void> {
    await this.loadingSpinner.waitFor({ state: 'hidden', timeout: TIMEOUTS.medium });
  }

  async waitForSkeletonsToHide(): Promise<void> {
    await this.skeleton.first().waitFor({ state: 'hidden', timeout: TIMEOUTS.medium });
  }

  /**
   * Toast notification helpers
   */
  async waitForToastSuccess(message?: string): Promise<void> {
    await expect(this.toastSuccess).toBeVisible({ timeout: TIMEOUTS.medium });
    if (message) {
      await expect(this.toastSuccess).toContainText(message);
    }
  }

  async waitForToastError(message?: string): Promise<void> {
    await expect(this.toastError).toBeVisible({ timeout: TIMEOUTS.medium });
    if (message) {
      await expect(this.toastError).toContainText(message);
    }
  }

  async dismissToast(): Promise<void> {
    const toast = this.page.locator('[data-sonner-toast]').first();
    if (await toast.isVisible()) {
      await toast.locator('button[data-close], .close-button').click();
    }
  }

  /**
   * Dialog helpers
   */
  async confirmDialog(): Promise<void> {
    await this.dialogConfirmButton.click();
    await this.dialog.waitFor({ state: 'hidden', timeout: TIMEOUTS.short });
  }

  async cancelDialog(): Promise<void> {
    await this.dialogCancelButton.click();
    await this.dialog.waitFor({ state: 'hidden', timeout: TIMEOUTS.short });
  }

  async waitForDialogToOpen(): Promise<void> {
    await expect(this.dialog).toBeVisible({ timeout: TIMEOUTS.short });
  }

  /**
   * Form helpers
   */
  async fillAndTab(locator: Locator, value: string): Promise<void> {
    await locator.fill(value);
    await locator.press('Tab');
  }

  async selectOption(locator: Locator, value: string): Promise<void> {
    // Handle both native select and shadcn/ui Select component
    const tagName = await locator.evaluate((el) => el.tagName.toLowerCase());
    if (tagName === 'select') {
      await locator.selectOption(value);
    } else {
      // shadcn/ui Select - click to open, then click option
      await locator.click();
      await this.page.locator(`[role="option"]:has-text("${value}")`).click();
    }
  }

  async clearAndFill(locator: Locator, value: string): Promise<void> {
    await locator.clear();
    await locator.fill(value);
  }

  /**
   * Table helpers
   */
  getTableRow(text: string): Locator {
    return this.page.locator(`tr:has-text("${text}")`);
  }

  getTableCell(row: Locator, columnIndex: number): Locator {
    return row.locator(`td:nth-child(${columnIndex + 1})`);
  }

  async getTableRowCount(): Promise<number> {
    return await this.page.locator('tbody tr').count();
  }

  /**
   * Click and navigation helpers
   */
  async clickAndWaitForNavigation(locator: Locator): Promise<void> {
    await Promise.all([
      this.page.waitForURL(/.*/, { timeout: TIMEOUTS.navigation }),
      locator.click(),
    ]);
  }

  async clickAndWaitForResponse(locator: Locator, urlPattern: string | RegExp): Promise<void> {
    await Promise.all([
      this.page.waitForResponse(urlPattern, { timeout: TIMEOUTS.api }),
      locator.click(),
    ]);
  }

  /**
   * Screenshot and debugging helpers
   */
  async takeScreenshot(name: string): Promise<void> {
    await this.page.screenshot({ path: `screenshots/${name}.png`, fullPage: true });
  }

  /**
   * Assertions
   */
  async assertPageTitle(title: string | RegExp): Promise<void> {
    await expect(this.page).toHaveTitle(title);
  }

  async assertUrl(url: string | RegExp): Promise<void> {
    await expect(this.page).toHaveURL(url);
  }

  async assertVisible(locator: Locator): Promise<void> {
    await expect(locator).toBeVisible({ timeout: TIMEOUTS.medium });
  }

  async assertNotVisible(locator: Locator): Promise<void> {
    await expect(locator).not.toBeVisible({ timeout: TIMEOUTS.short });
  }

  async assertText(locator: Locator, text: string | RegExp): Promise<void> {
    await expect(locator).toContainText(text);
  }

  async assertValue(locator: Locator, value: string): Promise<void> {
    await expect(locator).toHaveValue(value);
  }
}
