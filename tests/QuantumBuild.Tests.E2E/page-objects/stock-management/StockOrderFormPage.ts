import { Page, Locator } from '@playwright/test';
import { BasePage } from '../BasePage';

/**
 * Stock order form page object (create/edit)
 *
 * Form uses custom SearchSelect components:
 * - SiteSearchSelect for siteId
 * - LocationSearchSelect for sourceLocationId
 * - ProductSearchSelect for line items
 */
export class StockOrderFormPage extends BasePage {
  readonly pageTitle: Locator;
  readonly siteSelect: Locator;
  readonly sourceLocationSelect: Locator;
  readonly requiredDateButton: Locator;
  readonly notesInput: Locator;
  readonly addLineButton: Locator;
  readonly lineItems: Locator;
  readonly saveButton: Locator;
  readonly submitButton: Locator;
  readonly cancelButton: Locator;

  constructor(page: Page) {
    super(page);
    this.pageTitle = page.locator('h1');
    // Form uses custom SearchSelect components - look for combobox/popover trigger buttons
    this.siteSelect = page.locator('button:has-text("Select a site"), [role="combobox"]:near(:text("Site"))').first();
    this.sourceLocationSelect = page.locator('button:has-text("Select a location"), [role="combobox"]:near(:text("Source Location"))').first();
    // Date picker uses calendar popover
    this.requiredDateButton = page.locator('button:has([class*="CalendarIcon"]), button:near(:text("Required Date"))');
    this.notesInput = page.locator('textarea[name="notes"], textarea');
    // Add line item button
    this.addLineButton = page.locator('button:has-text("Add Item"), button:has([class*="PlusIcon"])');
    // Line items are in a table
    this.lineItems = page.locator('table tbody tr');
    // Submit button text is "Create Order" for new, "Update Order" for edit
    this.saveButton = page.locator('button[type="submit"]:has-text("Create Order"), button[type="submit"]:has-text("Update Order"), button[type="submit"]:has-text("Save")');
    this.submitButton = page.locator('button:has-text("Submit"), button:has-text("Submit for Approval")');
    this.cancelButton = page.locator('button:has-text("Cancel")');
  }

  /**
   * Navigate to create new order
   */
  async goto(): Promise<void> {
    await this.page.goto('/stock/orders/new');
    await this.waitForPageLoad();
  }

  /**
   * Navigate to edit order
   */
  async gotoEdit(id: string): Promise<void> {
    await this.page.goto(`/stock/orders/${id}/edit`);
    await this.waitForPageLoad();
  }

  /**
   * Select site
   */
  async selectSite(site: string): Promise<void> {
    await this.selectOption(this.siteSelect, site);
  }

  /**
   * Set required by date
   */
  async setRequiredByDate(date: string): Promise<void> {
    await this.requiredByDateInput.fill(date);
  }

  /**
   * Add notes
   */
  async addNotes(notes: string): Promise<void> {
    await this.notesInput.fill(notes);
  }

  /**
   * Add a line item
   */
  async addLine(data: { productId: string; quantity: number }): Promise<void> {
    await this.addLineButton.click();
    const lineCount = await this.lineItems.count();
    const lineIndex = lineCount - 1;

    await this.selectOption(
      this.page.locator(`[name="lines.${lineIndex}.productId"], [data-line="${lineIndex}"] [name="productId"]`),
      data.productId
    );
    await this.page.locator(`[name="lines.${lineIndex}.quantity"], [data-line="${lineIndex}"] [name="quantity"]`).fill(data.quantity.toString());
  }

  /**
   * Remove a line item
   */
  async removeLine(index: number): Promise<void> {
    await this.page.locator(`[data-line="${index}"] button:has-text("Remove"), [data-line="${index}"] [data-action="remove"]`).click();
  }

  /**
   * Update line quantity
   */
  async updateLineQuantity(index: number, quantity: number): Promise<void> {
    await this.page.locator(`[name="lines.${index}.quantity"], [data-line="${index}"] [name="quantity"]`).fill(quantity.toString());
  }

  /**
   * Save as draft
   */
  async saveDraft(): Promise<void> {
    await this.saveButton.click();
  }

  /**
   * Submit the order
   */
  async submit(): Promise<void> {
    await this.submitButton.click();
  }

  /**
   * Save and wait for success
   */
  async saveDraftAndWaitForSuccess(): Promise<void> {
    await this.saveDraft();
    await this.waitForToastSuccess();
  }

  /**
   * Submit and wait for success
   */
  async submitAndWaitForSuccess(): Promise<void> {
    await this.submit();
    await this.waitForToastSuccess();
  }

  /**
   * Get line item count
   */
  async getLineCount(): Promise<number> {
    return await this.lineItems.count();
  }

  /**
   * Cancel form
   */
  async cancel(): Promise<void> {
    await this.cancelButton.click();
  }
}
